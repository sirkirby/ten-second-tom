namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Constants for data retention policies and auto-purge settings.
/// </summary>
public static class DataRetentionConstants
{
    /// <summary>
    /// Default data retention policy.
    /// "Indefinite" means data is retained forever unless manually deleted.
    /// </summary>
    public const string DefaultPolicy = "Indefinite";

    /// <summary>
    /// Default auto-purge enabled setting.
    /// False means automatic purging is disabled by default.
    /// </summary>
    public const bool DefaultAutoPurgeEnabled = false;
}
