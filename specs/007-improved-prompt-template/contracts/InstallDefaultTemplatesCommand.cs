/// <summary>
/// Command to install default prompt templates to the filesystem.
/// </summary>
/// <remarks>
/// Used during:
/// - Initial setup (new users)
/// - Configuration migration (existing users)
/// - Self-healing when templates directory is missing
///
/// This command is idempotent - can be run multiple times safely.
/// Existing templates are NOT overwritten to preserve user customizations.
/// </remarks>
/// <param name="TargetDirectory">
/// The directory where templates should be installed.
/// Typically {MemoryDirectory}/templates/
/// Must be an absolute path.
/// Directory will be created if it doesn't exist.
/// </param>
/// <param name="OverwriteExisting">
/// If true, overwrites existing template files.
/// If false (default), skips files that already exist.
/// Default: false (preserve user customizations)
/// </param>
public sealed record InstallDefaultTemplatesCommand(
    string TargetDirectory,
    bool OverwriteExisting = false
) : IRequest<Result<InstallDefaultTemplatesResult>>;

/// <summary>
/// Result of installing default templates.
/// </summary>
/// <param name="TemplatesInstalled">
/// Number of templates successfully installed.
/// </param>
/// <param name="TemplatesSkipped">
/// Number of templates skipped because they already existed.
/// Only applicable when OverwriteExisting = false.
/// </param>
/// <param name="TemplatesFailed">
/// Number of templates that failed to install.
/// </param>
/// <param name="InstalledTemplateIds">
/// List of template IDs that were successfully installed.
/// Example: ["daily-summary", "weekly-review"]
/// </param>
public sealed record InstallDefaultTemplatesResult(
    int TemplatesInstalled,
    int TemplatesSkipped,
    int TemplatesFailed,
    IReadOnlyList<string> InstalledTemplateIds
);
