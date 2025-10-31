using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Defines the contract for pluggable storage providers.
/// Extends IMemoryStorageProvider with provider metadata and lifecycle management.
/// </summary>
/// <remarks>
/// Storage providers are discovered via assembly scanning at startup.
/// Each provider must supply unique metadata (ProviderId, DisplayName, Description)
/// and implement configuration validation and initialization.
///
/// Implementations should:
/// 1. Have a unique ProviderId that doesn't conflict with other providers
/// 2. Inject IOptions&lt;StorageOptions&gt; to access configuration
/// 3. Validate configuration in ValidateConfigurationAsync
/// 4. Initialize directory structures in InitializeAsync
/// 5. Implement all IMemoryStorageProvider methods for data operations
/// </remarks>
public interface IStorageProvider : IMemoryStorageProvider
{
    /// <summary>
    /// Gets the unique identifier for this storage provider (e.g., "default", "obsidian").
    /// Used in configuration to select the active provider.
    /// </summary>
    /// <remarks>
    /// Must be lowercase and URL-friendly. Use constants from <see cref="TenSecondTom.Shared.Constants.StorageProviderIds"/>.
    /// </remarks>
    string ProviderId { get; }

    /// <summary>
    /// Gets the display name shown to users (e.g., "Default File System", "Obsidian Vault").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets a description of the provider's capabilities and use cases.
    /// Shown during setup wizard to help users choose a provider.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initializes the storage provider (creates directories, validates paths, etc.).
    /// Called once after provider is selected and before first use.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result indicating success or failure with error details.</returns>
    Task<Result> InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates that the current configuration is compatible with this provider.
    /// Called during setup/config commands to provide early feedback.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing validation messages or errors.</returns>
    Task<Result<string>> ValidateConfigurationAsync(CancellationToken cancellationToken);
}
