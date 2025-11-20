namespace TenSecondTom.Shared.Models;

/// <summary>
/// Result of installing default templates.
/// Used for template installation operations across the application.
/// </summary>
public sealed record TemplateInstallationResult
{
    /// <summary>
    /// Number of templates successfully installed.
    /// </summary>
    public required int TemplatesInstalled { get; init; }

    /// <summary>
    /// Number of templates skipped because they already existed.
    /// Only applicable when OverwriteExisting = false.
    /// </summary>
    public required int TemplatesSkipped { get; init; }

    /// <summary>
    /// Number of templates that failed to install.
    /// </summary>
    public required int TemplatesFailed { get; init; }

    /// <summary>
    /// List of template IDs that were successfully installed.
    /// Example: ["daily-summary", "weekly-review"]
    /// </summary>
    public required IReadOnlyList<string> InstalledTemplateIds { get; init; }
}
