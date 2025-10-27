namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides constants for the setup wizard UI to ensure consistency and avoid hardcoded strings.
/// These constants are user-facing display strings.
/// </summary>
public static class SetupWizardConstants
{
    /// <summary>
    /// Display suffixes and formatting for LLM provider choices in the setup wizard.
    /// Uses LlmProviders constants as the source of truth for provider names.
    /// </summary>
    public static class ProviderDisplayNames
    {
        /// <summary>
        /// Gets the display name for a provider (proper case).
        /// Converts lowercase provider names from LlmProviders to display-friendly format.
        /// </summary>
        public static string GetDisplayName(string providerName)
        {
            return providerName.ToLowerInvariant() switch
            {
                LlmProviders.OpenAI => "OpenAI",
                LlmProviders.Anthropic => "Anthropic",
                _ => providerName // Fallback to original if unknown
            };
        }

        /// <summary>
        /// Gets all provider display names (includes Anthropic).
        /// For backward compatibility and internal use only.
        /// </summary>
        public static string[] GetDisplayNames()
        {
            return LlmProviders.All
                .Select(GetDisplayName)
                .ToArray();
        }

        /// <summary>
        /// Gets display names for providers available in the setup wizard.
        /// Currently only OpenAI is available (Anthropic code exists but is not selectable).
        /// </summary>
        public static string[] GetAvailableDisplayNames()
        {
            // Only OpenAI is available for configuration
            // Anthropic code remains but is not selectable in setup wizard
            return [GetDisplayName(LlmProviders.OpenAI)];
        }
    }

    /// <summary>
    /// Display choices for logging levels in the setup wizard.
    /// </summary>
    public static class LogLevelDisplayNames
    {
        /// <summary>
        /// Debug level display choice.
        /// </summary>
        public const string Debug = "Debug (verbose)";

        /// <summary>
        /// Information level display choice.
        /// </summary>
        public const string Information = "Information (recommended)";

        /// <summary>
        /// Warning level display choice.
        /// </summary>
        public const string Warning = "Warning (quiet)";

        /// <summary>
        /// Error level display choice.
        /// </summary>
        public const string Error = "Error (silent)";
    }

    /// <summary>
    /// Keywords and values for retention policy configuration.
    /// </summary>
    public static class RetentionKeywords
    {
        /// <summary>
        /// Keyword for unlimited retention.
        /// </summary>
        public const string Unlimited = "unlimited";

        /// <summary>
        /// Alternative keyword for unlimited retention.
        /// </summary>
        public const string Forever = "forever";

        /// <summary>
        /// Zero value treated as unlimited.
        /// </summary>
        public const string Zero = "0";

        /// <summary>
        /// Display string for unlimited retention in summary.
        /// </summary>
        public const string UnlimitedDisplay = "Unlimited (never delete)";
    }

    /// <summary>
    /// Common display strings used throughout the setup wizard.
    /// </summary>
    public static class DisplayStrings
    {
        /// <summary>
        /// Display string when a value is not set.
        /// </summary>
        public const string NotSet = "Not set";

        /// <summary>
        /// Display string for days unit in retention policy.
        /// </summary>
        public const string Days = "days";
    }

    /// <summary>
    /// Timeout values specific to the setup wizard flow.
    /// These control how long the wizard waits for various setup steps.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>
        /// Timeout for SSH key detection step (in seconds).
        /// Maximum time to wait for SSH agent detection and key discovery.
        /// </summary>
        public const int SshKeyDetectionSeconds = 5;

        /// <summary>
        /// Timeout for API validation step (in seconds).
        /// Maximum time to wait when validating LLM provider API keys.
        /// </summary>
        public const int ApiValidationSeconds = 10;

        /// <summary>
        /// Total setup wizard timeout (in seconds).
        /// Maximum time allowed for the entire setup process.
        /// </summary>
        public const int TotalSetupSeconds = 120;
    }
}
