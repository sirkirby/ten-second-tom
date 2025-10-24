using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for saving generated outputs to the filesystem.
/// Provides operations to build output file paths, check for existing outputs, and save generated content.
/// </summary>
public interface IOutputStorageService
{
    /// <summary>
    /// Saves generated output to the recording directory.
    /// Uses the format: M-D-Y_TemplateName_Increment.md (e.g., "10-21-2025_daily-summary_1.md").
    /// Overwrites if file already exists for the same recording and template combination.
    /// </summary>
    /// <param name="output">The generated output to save, including content and metadata.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing the full path to the saved output file on success,
    /// or an error message if the save operation fails.
    /// </returns>
    Task<Result<string>> SaveOutputAsync(
        GeneratedOutput output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if output file already exists for a recording and template combination.
    /// </summary>
    /// <param name="recordingBaseName">The base name of the recording (e.g., "10-21-2025_1").</param>
    /// <param name="templateId">The template ID (e.g., "daily-summary").</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if the output file exists, false otherwise.</returns>
    Task<bool> OutputExistsAsync(
        string recordingBaseName,
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the output file path for a recording and template combination.
    /// Uses the format: M-D-Y_TemplateName_Increment.md
    /// </summary>
    /// <param name="recordingBaseName">The base name of the recording (e.g., "10-21-2025_1").</param>
    /// <param name="templateId">The template ID (e.g., "daily-summary").</param>
    /// <returns>The full path to where the output file should be saved.</returns>
    string BuildOutputFilePath(string recordingBaseName, string templateId);
}
