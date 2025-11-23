namespace TenSecondTom.Shared.Options;

/// <summary>
/// Notification system configuration options.
/// Maps to the "TenSecondTom:Notifications" configuration section.
/// </summary>
/// <remarks>
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Notifications": {
///       "Enabled": true,
///       "DefaultTimeoutSeconds": 30,
///       "DefaultPriority": "Normal",
///       "SilentFallback": true
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Notifications__Enabled
/// - TenSecondTom__Notifications__DefaultTimeoutSeconds
/// - TenSecondTom__Notifications__DefaultPriority
/// - TenSecondTom__Notifications__SilentFallback
/// </remarks>
public sealed class NotificationOptions
{
    /// <summary>
    /// Configuration section path for Notification options.
    /// </summary>
    public const string SectionPath = "TenSecondTom:Notifications";

    /// <summary>
    /// Gets or sets a value indicating whether notifications are enabled.
    /// When false, all notification requests are silently ignored.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the default timeout in seconds for notifications.
    /// This value is used when a notification doesn't specify its own timeout.
    /// Set to 0 for no timeout (notification remains until user interaction).
    /// Default: 30 seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Gets or sets the default priority level for notifications.
    /// This value is used when a notification doesn't specify its own priority.
    /// Valid values: "Low", "Normal", "High", "Critical".
    /// Default: Normal.
    /// </summary>
    public Models.NotificationPriority DefaultPriority { get; init; } = Models.NotificationPriority.Normal;

    /// <summary>
    /// Gets or sets a value indicating whether to silently fall back when notifications fail.
    /// When true, notification errors are logged but don't cause feature failures.
    /// When false, notification errors propagate to the caller.
    /// Default: true.
    /// </summary>
    public bool SilentFallback { get; init; } = true;
}
