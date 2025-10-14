using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Shared.TextEditing.Services;

/// <summary>
/// Service for interactive multi-line text editing in the console.
/// </summary>
public interface IInteractiveTextEditor
{
    /// <summary>
    /// Start an interactive editing session with optional initial content.
    /// </summary>
    /// <param name="initialContent">Pre-filled content to edit (null for new entry)</param>
    /// <param name="configuration">Editor configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing edited content and outcome</returns>
    Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
}
