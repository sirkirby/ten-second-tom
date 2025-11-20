namespace TenSecondTom.Shared.Models;

/// <summary>
/// Optional configuration settings
/// </summary>
public sealed record OptionalConfiguration
{
    /// <summary>
    /// Gets the logging level
    /// </summary>
    public Microsoft.Extensions.Logging.LogLevel LogLevel { get; init; } = Microsoft.Extensions.Logging.LogLevel.Information;

    /// <summary>
    /// Gets the number of days to retain memories
    /// </summary>
    public int RetentionDays { get; init; } = 30;

    /// <summary>
    /// Gets whether telemetry is enabled
    /// </summary>
    public bool EnableTelemetry { get; init; }
}
