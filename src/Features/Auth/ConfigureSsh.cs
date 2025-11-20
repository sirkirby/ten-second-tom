using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Configures SSH authentication settings interactively.
/// This use case handles all SSH configuration logic within the Auth feature slice.
/// </summary>
public static class ConfigureSsh
{
    /// <summary>
    /// Command to configure SSH authentication settings.
    /// </summary>
    public sealed record Command : IRequest<Result<SshConfiguration>>
    {
        /// <summary>
        /// Gets the timeout for SSH key detection.
        /// </summary>
        public TimeSpan DetectionTimeout { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets whether to force reconfiguration even if already configured.
        /// When true, always runs interactive prompts (user explicitly wants to change config).
        /// When false, skips if already configured (idempotent behavior for setup wizard).
        /// </summary>
        public bool Force { get; init; }
    }

    /// <summary>
    /// Validator for ConfigureSsh command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DetectionTimeout)
                .GreaterThan(TimeSpan.Zero)
                .WithMessage("Detection timeout must be positive");
        }
    }

    /// <summary>
    /// Handler for ConfigureSsh command (auto-discovered by MediatR).
    /// Orchestrates interactive SSH key selection and configuration.
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ISetupWizardUI setupWizard,
        ISshKeyDetectorFactory sshKeyDetectorFactory,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<SshConfiguration>>
    {
        public async Task<Result<SshConfiguration>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting interactive SSH configuration");

            // Load current SSH configuration
            var loadResult = await sectionStore.ReadSectionAsync<AuthOptions>(
                AuthOptions.SectionPath,
                cancellationToken).ConfigureAwait(false);

            var currentSshConfig = loadResult.Value;

            // Smart handler: If already configured AND not forced, skip interactive prompts (idempotent)
            // When Force=true (direct user invocation), always run interactive prompts
            // When Force=false (setup wizard), skip if already configured
            if (!request.Force && currentSshConfig != null && currentSshConfig.IsConfigured())
            {
                logger.LogInformation("SSH already configured and not forced, skipping interactive setup");
                setupWizard.ShowSuccess($"✓ SSH already configured: {currentSshConfig.KeyDisplayName ?? "SSH Key"}");

                // Return existing configuration
                var existingConfig = new SshConfiguration
                {
                    KeyPath = currentSshConfig.KeyPath,
                    KeySource = currentSshConfig.KeySource ?? SshKeySource.FileSystem,
                    AgentSocketPath = currentSshConfig.AgentSocketPath,
                    KeyDisplayName = currentSshConfig.KeyDisplayName
                };

                return Result<SshConfiguration>.Success(existingConfig);
            }

            // Step 1: SSH Key Detection and Selection
            setupWizard.ShowStepHeader(1, 2, "SSH Key Configuration");
            setupWizard.ShowStatus("Detecting SSH keys...");

            var sshDetectionResult = await sshKeyDetectorFactory.DetectKeysAsync(
                request.DetectionTimeout,
                cancellationToken);

            // Note: We can't reconstruct the full SshKeyInfo from stored config,
            // so we pass null and let the user re-select (the display name will still show current config)
            var selectedSshKey = await setupWizard.PromptForSshKeyAsync(
                sshDetectionResult.DetectedKeys,
                null,
                cancellationToken);

            if (selectedSshKey == null)
            {
                logger.LogInformation("SSH configuration cancelled by user");
                return Result<SshConfiguration>.Failure("SSH configuration cancelled. No changes were made.");
            }

            // Determine agent socket path
            var agentSocketPath = selectedSshKey.Source switch
            {
                SshKeySource.SystemAgent or SshKeySource.OnePasswordAgent or SshKeySource.SecretiveAgent
                    => GetAgentSocketPath(selectedSshKey.Source, currentSshConfig?.AgentSocketPath),
                _ => null
            };

            // Create updated SSH configuration
            var updatedSshOptions = new AuthOptions
            {
                KeyPath = selectedSshKey.FilePath,
                KeySource = selectedSshKey.Source,
                AgentSocketPath = agentSocketPath,
                KeyDisplayName = selectedSshKey.DisplayName
            };

            // Step 2: Save Configuration
            setupWizard.ShowStepHeader(2, 2, "Saving Configuration");
            setupWizard.ShowStatus("Saving SSH configuration...");

            var saveResult = await sectionStore.WriteSectionAsync(
                AuthOptions.SectionPath,
                updatedSshOptions,
                cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<SshConfiguration>.Failure($"Failed to save configuration: {saveResult.Error}. Changes were not applied. Try again or check file permissions.");
            }

            logger.LogInformation(
                "SSH configuration updated successfully: KeySource={KeySource}, DisplayName={DisplayName}",
                selectedSshKey.Source,
                selectedSshKey.DisplayName);

            // Display success message
            setupWizard.ShowSuccess($"✓ SSH configuration updated: {selectedSshKey.DisplayName}");

            // Return the configuration as SshConfiguration model for compatibility
            var resultConfig = new SshConfiguration
            {
                KeyPath = selectedSshKey.FilePath,
                KeySource = selectedSshKey.Source,
                AgentSocketPath = agentSocketPath,
                KeyDisplayName = selectedSshKey.DisplayName
            };

            return Result<SshConfiguration>.Success(resultConfig);
        }

        /// <summary>
        /// Determines the SSH agent socket path to persist, preferring any previously stored value.
        /// </summary>
        private static string? GetAgentSocketPath(SshKeySource keySource, string? existingAgentSocketPath)
        {
            // If we have an existing agent socket path, prefer it (user may have customized)
            if (!string.IsNullOrWhiteSpace(existingAgentSocketPath))
            {
                return existingAgentSocketPath;
            }

            // Otherwise, get the default path for the agent type
            var provider = keySource switch
            {
                SshKeySource.SystemAgent => SshAgentProvider.System,
                SshKeySource.OnePasswordAgent => SshAgentProvider.OnePassword,
                SshKeySource.SecretiveAgent => SshAgentProvider.Secretive,
                _ => SshAgentProvider.System
            };

            return SshAgentProviderResolver.GetSocketPath(provider);
        }
    }
}
