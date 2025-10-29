namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Metadata about a storage provider.
/// Used for discovery and display in setup wizards without fully instantiating the provider.
/// </summary>
/// <param name="ProviderId">The unique provider identifier (e.g., "default", "obsidian").</param>
/// <param name="DisplayName">The user-friendly display name (e.g., "Default File System").</param>
/// <param name="Description">A description of the provider's capabilities and use cases.</param>
public sealed record StorageProviderMetadata(
    string ProviderId,
    string DisplayName,
    string Description);
