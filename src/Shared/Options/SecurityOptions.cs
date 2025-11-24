namespace TenSecondTom.Shared.Options;

/// <summary>
/// Security configuration options for notification system.
/// Maps to the "TenSecondTom:Security" configuration section.
/// </summary>
/// <remarks>
/// Configuration example (appsettings.json or user secrets):
/// <code>
/// {
///   "TenSecondTom": {
///     "Security": {
///       "NotificationSecret": "your-secret-key-here",
///       "MaxTokenAgeSeconds": 300
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Security__NotificationSecret
/// - TenSecondTom__Security__MaxTokenAgeSeconds
///
/// IMPORTANT: NotificationSecret should be stored in user secrets or environment variables,
/// never committed to source control.
/// </remarks>
public sealed class SecurityOptions
{
    /// <summary>
    /// Configuration section path for Security options.
    /// </summary>
    public const string SectionPath = "TenSecondTom:Security";

    /// <summary>
    /// Gets or sets the secret key used for signing notification callback tokens.
    /// This value is used to generate HMAC signatures that prevent tampering with notification actions.
    /// OPTIONAL: If not set, interactive notifications will be disabled (graceful degradation).
    /// SECURITY: Store in user secrets or environment variables, never in source control.
    /// Recommended: Generate a random 32+ character string.
    /// </summary>
    public string? NotificationSecret { get; init; }

    /// <summary>
    /// Gets or sets the maximum age in seconds for notification tokens.
    /// Tokens older than this value will be rejected even if their signature is valid.
    /// This prevents replay attacks and limits the window for malicious use of captured tokens.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int MaxTokenAgeSeconds { get; init; } = 300;
}
