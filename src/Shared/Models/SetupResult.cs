namespace TenSecondTom.Shared.Models;

/// <summary>
/// Result of running the setup wizard.
/// Contains the success message and path to configuration file.
/// </summary>
public sealed record SetupResult(
    string Message,
    string ConfigPath
);
