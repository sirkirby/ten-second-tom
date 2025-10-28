using Microsoft.Extensions.Logging;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for optional application-level settings.
/// Maps to the "TenSecondTom:Optional" configuration section.
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;AppOptions&gt; or IOptionsSnapshot&lt;AppOptions&gt; in services.
///
/// All properties in this class are optional and have sensible defaults.
/// These settings control application-wide behavior like logging and telemetry.
///
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Optional": {
///       "LogLevel": "Information",
///       "RetentionDays": 90,
///       "EnableTelemetry": false
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Optional__LogLevel
/// - TenSecondTom__Optional__RetentionDays
/// - TenSecondTom__Optional__EnableTelemetry
/// </remarks>
public sealed class AppOptions
{
    /// <summary>
    /// The configuration section name for optional application settings.
    /// </summary>
    public const string SectionName = "TenSecondTom:Optional";

    /// <summary>
    /// Gets or sets the minimum log level for application logging.
    /// </summary>
    /// <remarks>
    /// Controls the verbosity of log output.
    /// Valid values: Trace, Debug, Information, Warning, Error, Critical, None.
    /// Default: <see cref="LogLevel.Information"/>.
    ///
    /// Recommended levels:
    /// - Development: Debug or Trace
    /// - Production: Information or Warning
    /// - Troubleshooting: Debug or Trace
    /// </remarks>
    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the number of days to retain application data.
    /// </summary>
    /// <remarks>
    /// This setting provides a default retention period for various application data types.
    /// Individual features may override this with feature-specific retention policies.
    /// Default: 90 days.
    /// Valid range: 1 to 3650 days (10 years).
    /// </remarks>
    public int RetentionDays { get; init; } = 90;

    /// <summary>
    /// Gets or sets a value indicating whether telemetry collection is enabled.
    /// </summary>
    /// <remarks>
    /// When enabled, the application may collect anonymous usage telemetry.
    /// Telemetry helps improve the application but is completely optional.
    /// Default: false (telemetry disabled).
    ///
    /// Note: No telemetry is currently implemented. This is a placeholder for future functionality.
    /// </remarks>
    public bool EnableTelemetry { get; init; }
}
