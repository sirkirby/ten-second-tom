namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Summary information displayed to user before saving setup configuration.
/// This is a lightweight DTO used by the UI to display configuration summary.
/// </summary>
public sealed record SetupSummary(
    string SshKeyDisplay,
    string LlmProvider,
    string ApiKey,
    string RootDirectory,
    string LogLevel,
    int RetentionDays
);
