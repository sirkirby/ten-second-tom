using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Storage;

/// <summary>
/// Configures storage provider and paths interactively.
/// This use case handles all storage configuration logic within the Storage feature slice.
/// </summary>
public static class ConfigureStorage
{
    /// <summary>
    /// Command to configure storage provider and paths.
    /// </summary>
    public sealed record Command : IRequest<Result<StorageConfigurationResult>>
    {
        /// <summary>
        /// Gets the existing root directory (from prior configuration or default).
        /// </summary>
        public string? ExistingRootDirectory { get; init; }

        /// <summary>
        /// Gets the existing storage configuration to use as defaults.
        /// </summary>
        public StorageSettings? ExistingStorage { get; init; }

        /// <summary>
        /// Gets whether to force reconfiguration even if already configured.
        /// When true, always runs interactive prompts (user explicitly wants to change config).
        /// When false, skips if already configured (idempotent behavior for setup wizard).
        /// </summary>
        public bool Force { get; init; }

        /// <summary>
        /// Optional override for root directory (non-interactive configuration).
        /// </summary>
        public string? RootDirectoryOverride { get; init; }

        /// <summary>
        /// Optional override for storage provider ID (non-interactive configuration).
        /// </summary>
        public string? ProviderIdOverride { get; init; }

        /// <summary>
        /// Optional override for provider path (non-interactive configuration).
        /// </summary>
        public string? ProviderPathOverride { get; init; }

        /// <summary>
        /// Optional override for memory subdirectory (non-interactive configuration).
        /// </summary>
        public string? MemorySubdirectoryOverride { get; init; }
    }

    /// <summary>
    /// Validator for ConfigureStorage command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // No validation needed - command parameters are optional defaults
        }
    }

    /// <summary>
    /// Handler for ConfigureStorage command (auto-discovered by MediatR).
    /// Orchestrates interactive storage provider selection and path configuration.
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ISetupWizardUI setupWizard,
        IStorageProviderFactory storageProviderFactory,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<StorageConfigurationResult>>
    {
        public async Task<Result<StorageConfigurationResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting interactive storage configuration");

            // Load current storage configuration
            var loadResult = await sectionStore.ReadSectionAsync<StorageSettings>(
                "TenSecondTom:Storage",
                cancellationToken).ConfigureAwait(false);

            var currentStorageConfig = loadResult.Value ?? new StorageSettings();
            var effectiveStorage = request.ExistingStorage ?? currentStorageConfig;
            var rootDirectoryDefault = request.ExistingRootDirectory ?? GetDefaultRootDirectory();

            if (HasCommandLineOverrides(request))
            {
                return await ApplyCommandLineOverridesAsync(
                    request,
                    effectiveStorage,
                    rootDirectoryDefault,
                    cancellationToken).ConfigureAwait(false);
            }

            // Smart handler: If already configured AND not forced, skip interactive prompts (idempotent)
            // When Force=true (direct user invocation), always run interactive prompts
            // When Force=false (setup wizard), skip if already configured
            if (!request.Force && currentStorageConfig.IsConfigured())
            {
                logger.LogInformation("Storage already configured and not forced, skipping interactive setup");

                // Use existing root directory or default
                var existingRootDir = request.ExistingRootDirectory
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        DirectoryNames.ApplicationRoot);

                setupWizard.ShowSuccess($"✓ Storage already configured: {currentStorageConfig.ProviderId} provider");

                // Return existing configuration
                var existingResult = new StorageConfigurationResult
                {
                    RootDirectory = existingRootDir,
                    Storage = currentStorageConfig
                };

                return Result<StorageConfigurationResult>.Success(existingResult);
            }

            // Step 1: Storage Provider Selection
            setupWizard.ShowStepHeader(1, 3, "Storage Provider Selection");
            var availableProviders = storageProviderFactory.GetAvailableProviders();
            var selectedStorageProvider = await setupWizard.PromptForStorageProviderAsync(
                availableProviders,
                effectiveStorage?.ProviderId,
                cancellationToken);

            if (selectedStorageProvider == null)
            {
                setupWizard.ShowWarning("No storage provider selected. Defaulting to 'default' provider.");
                selectedStorageProvider = availableProviders.FirstOrDefault(p =>
                    p.ProviderId.Equals(StorageProviderIds.Default, StringComparison.OrdinalIgnoreCase));

                if (selectedStorageProvider == null)
                {
                    setupWizard.ShowError("Setup cannot continue without a storage provider.");
                    return Result<StorageConfigurationResult>.Failure("Setup cancelled: No storage provider available.");
                }
            }

            // Step 2: Application Root Directory Configuration
            setupWizard.ShowStepHeader(2, 3, "Application Root Directory");
            setupWizard.ShowStatus("This is where config.json and templates/ will be stored.");
            var rootDirectory = await setupWizard.PromptForRootDirectoryAsync(
                rootDirectoryDefault,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    DirectoryNames.ApplicationRoot);
            }

            var selectedProviderMetadata = selectedStorageProvider!;

            // Step 3: Storage Provider Configuration (provider-specific)
            setupWizard.ShowStepHeader(3, 3, "Storage Path Configuration");
            string? providerPath = effectiveStorage?.ProviderPath;
            string? memorySubdirectory = effectiveStorage?.MemorySubdirectory;

            if (selectedProviderMetadata.ProviderId.Equals(StorageProviderIds.Obsidian, StringComparison.OrdinalIgnoreCase))
            {
                // Obsidian-specific configuration
                setupWizard.ShowStatus("This is where your memory entries (today, thisweek, recordings) will be stored.");
                providerPath = await setupWizard.PromptForObsidianVaultPathAsync(
                    effectiveStorage?.ProviderPath,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(providerPath))
                {
                    setupWizard.ShowError("Setup cannot continue without a valid Obsidian vault path.");
                    return Result<StorageConfigurationResult>.Failure("Setup cancelled: No vault path provided. Run 'tom setup' again.");
                }

                // Obsidian Subdirectory (optional)
                setupWizard.ShowStatus("Optionally create a subdirectory under the vault for Ten Second Tom entries.");
                memorySubdirectory = await setupWizard.PromptForSubdirectoryAsync(
                    "Subdirectory name (leave empty for vault root):",
                    effectiveStorage?.MemorySubdirectory,
                    cancellationToken);
            }
            else
            {
                // Default provider uses RootDirectory for both config and storage
                setupWizard.ShowStatus("Memory entries will be stored in the application root directory.");
                // providerPath stays null - provider will use RootDirectory
            }

            // Create updated storage configuration
            var updatedStorageConfig = (effectiveStorage ?? new StorageSettings()) with
            {
                ProviderId = selectedProviderMetadata.ProviderId,
                ProviderPath = providerPath,
                MemorySubdirectory = memorySubdirectory
            };

            // Save storage configuration and root directory atomically
            var sections = new Dictionary<string, object>
            {
                ["TenSecondTom:RootDirectory"] = rootDirectory,
                ["TenSecondTom:Storage"] = updatedStorageConfig
            };

            var saveResult = await sectionStore.WriteMultipleSectionsAsync(
                sections,
                cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<StorageConfigurationResult>.Failure($"Failed to save configuration: {saveResult.Error}");
            }

            logger.LogInformation(
                "Storage configuration updated successfully: ProviderId={ProviderId}, RootDirectory={RootDirectory}",
                selectedProviderMetadata.ProviderId,
                rootDirectory);

            // Display success message
            setupWizard.ShowSuccess($"✓ Storage configuration updated: {selectedProviderMetadata.ProviderId} provider");

            // Return the configuration result
            var result = new StorageConfigurationResult
            {
                RootDirectory = rootDirectory,
                Storage = updatedStorageConfig
            };

            return Result<StorageConfigurationResult>.Success(result);
        }
        private static bool HasCommandLineOverrides(Command request) =>
            !string.IsNullOrWhiteSpace(request.RootDirectoryOverride) ||
            !string.IsNullOrWhiteSpace(request.ProviderIdOverride) ||
            !string.IsNullOrWhiteSpace(request.ProviderPathOverride) ||
            !string.IsNullOrWhiteSpace(request.MemorySubdirectoryOverride);

        private async Task<Result<StorageConfigurationResult>> ApplyCommandLineOverridesAsync(
            Command request,
            StorageSettings existingStorage,
            string existingRootDirectory,
            CancellationToken cancellationToken)
        {
            var rootDirectory = request.RootDirectoryOverride?.Trim();
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = existingRootDirectory;
            }

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = GetDefaultRootDirectory();
            }

            var providerId = (request.ProviderIdOverride ?? existingStorage.ProviderId ?? StorageProviderIds.Default).Trim();
            var availableProviders = storageProviderFactory.GetAvailableProviders();
            var providerMetadata = availableProviders.FirstOrDefault(p =>
                p.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));

            if (providerMetadata is null)
            {
                return Result<StorageConfigurationResult>.Failure(
                    $"Unknown storage provider '{providerId}'. Valid providers: {string.Join(", ", availableProviders.Select(p => p.ProviderId))}");
            }

            var providerPath = request.ProviderPathOverride ?? existingStorage.ProviderPath;
            var memorySubdirectory = request.MemorySubdirectoryOverride ?? existingStorage.MemorySubdirectory;

            if (providerId.Equals(StorageProviderIds.Obsidian, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(providerPath))
            {
                return Result<StorageConfigurationResult>.Failure(
                    "Obsidian provider requires --provider-path to be specified.");
            }

            var updatedStorageConfig = (existingStorage ?? new StorageSettings()) with
            {
                ProviderId = providerId,
                ProviderPath = providerPath,
                MemorySubdirectory = memorySubdirectory
            };

            var sections = new Dictionary<string, object>
            {
                ["TenSecondTom:RootDirectory"] = rootDirectory,
                ["TenSecondTom:Storage"] = updatedStorageConfig
            };

            var saveResult = await sectionStore.WriteMultipleSectionsAsync(sections, cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<StorageConfigurationResult>.Failure($"Failed to save storage configuration: {saveResult.Error}");
            }

            setupWizard.ShowSuccess("✓ Storage configuration updated");
            setupWizard.ShowStatus($"  • Provider: {providerId}");
            setupWizard.ShowStatus($"  • Root directory: {rootDirectory}");
            if (!string.IsNullOrWhiteSpace(providerPath))
            {
                setupWizard.ShowStatus($"  • Provider path: {providerPath}");
            }
            if (!string.IsNullOrWhiteSpace(memorySubdirectory))
            {
                setupWizard.ShowStatus($"  • Memory subdirectory: {memorySubdirectory}");
            }

            var result = new StorageConfigurationResult
            {
                RootDirectory = rootDirectory,
                Storage = updatedStorageConfig
            };

            return Result<StorageConfigurationResult>.Success(result);
        }

        private static string GetDefaultRootDirectory() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DirectoryNames.ApplicationRoot);
    }
}
