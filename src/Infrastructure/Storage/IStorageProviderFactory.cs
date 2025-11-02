using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Factory for creating and discovering storage provider implementations.
/// Implementations use assembly scanning to automatically discover all IStorageProvider implementations.
/// </summary>
public interface IStorageProviderFactory
{
    /// <summary>
    /// Creates a storage provider instance based on the specified provider ID.
    /// </summary>
    /// <param name="providerId">The provider ID from configuration (e.g., "default", "obsidian").</param>
    /// <returns>Result containing the provider instance or error message.</returns>
    Result<IStorageProvider> CreateProvider(string providerId);

    /// <summary>
    /// Gets metadata for all discovered storage providers.
    /// Used for displaying available providers in setup wizards.
    /// </summary>
    /// <returns>A read-only list of provider metadata.</returns>
    IReadOnlyList<StorageProviderMetadata> GetAvailableProviders();
}
