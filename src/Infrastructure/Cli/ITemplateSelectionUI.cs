using TenSecondTom.Infrastructure.Prompts;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Interface for handling template selection UI interactions.
/// </summary>
public interface ITemplateSelectionUI
{
    /// <summary>
    /// Displays a template selection UI and returns the selected template ID.
    /// </summary>
    /// <param name="templates">List of available templates to display.</param>
    /// <param name="commandContext">Context for the command (e.g., "daily summary", "weekly review").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// The ID of the selected template, or null if selection was cancelled or no templates available.
    /// </returns>
    Task<string?> SelectTemplateAsync(
        IReadOnlyList<TemplateInfo> templates,
        string commandContext,
        CancellationToken cancellationToken = default);
}