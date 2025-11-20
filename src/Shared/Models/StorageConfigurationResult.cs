namespace TenSecondTom.Shared.Models;

/// <summary>
/// Result containing storage configuration and root directory.
/// </summary>
public sealed record StorageConfigurationResult
{
    public required string RootDirectory { get; init; }
    public required StorageSettings Storage { get; init; }
}
