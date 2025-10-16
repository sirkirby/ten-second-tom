using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates.Commands;

/// <summary>
/// Marker interface for request/response pattern.
/// Indicates this command returns a specific response type.
/// </summary>
public interface IRequest<out TResponse>
{
}

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
public sealed class InstallDefaultTemplatesCommand : IRequest<Result<InstallDefaultTemplatesResult>>
{
    /// <summary>
    /// The directory where templates should be installed.
    /// Typically {MemoryDirectory}/templates/
    /// Must be an absolute path.
    /// Directory will be created if it doesn't exist.
    /// </summary>
    public required string TargetDirectory { get; init; }

    /// <summary>
    /// If true, overwrites existing template files.
    /// If false (default), skips files that already exist.
    /// Default: false (preserve user customizations)
    /// </summary>
    public bool OverwriteExisting { get; init; }
}

/// <summary>
/// Result of installing default templates.
/// </summary>
public sealed class InstallDefaultTemplatesResult
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
