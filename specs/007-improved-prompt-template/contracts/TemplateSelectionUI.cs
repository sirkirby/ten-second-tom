/// <summary>
/// Service for presenting template selection UI to users via Spectre.Console.
/// </summary>
/// <remarks>
/// Responsible for:
/// - Displaying available templates in an interactive selection prompt
/// - Handling auto-selection when only one template is available
/// - Formatting template information for display
/// - Handling user cancellation
///
/// Uses Spectre.Console's SelectionPrompt for arrow-key navigation.
/// </remarks>
public interface ITemplateSelectionUI
{
    /// <summary>
    /// Prompts the user to select a template from the available options.
    /// </summary>
    /// <param name="availableTemplates">
    /// List of templates to choose from, already filtered by type.
    /// Must contain at least one template.
    /// </param>
    /// <param name="commandName">
    /// Name of the command requesting template selection (e.g., "today", "thisweek").
    /// Used in the prompt title for context.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the operation.
    /// </param>
    /// <returns>
    /// The template ID selected by the user.
    /// Returns null if user cancels (Ctrl+C).
    /// Automatically returns the only template if availableTemplates.Count == 1.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if availableTemplates is empty.
    /// </exception>
    /// <remarks>
    /// Display format:
    ///
    /// Select a template for [commandName]:
    ///   > Daily Summary - Default template for daily journal entries [Default]
    ///     Daily Standup - Focused template for standup meetings
    ///     Daily Reflections - Detailed template for end-of-day reflections
    ///
    /// Default templates are marked with [Default] suffix.
    /// Templates are displayed as: {Title} - {Description} [Default?]
    ///
    /// Auto-selection behavior:
    /// If only one template is available, it is selected automatically without
    /// prompting the user, and a message is displayed:
    ///   "Using template: {Title}"
    /// </remarks>
    Task<string?> SelectTemplateAsync(
        IReadOnlyList<TemplateListItem> availableTemplates,
        string commandName,
        CancellationToken cancellationToken = default);
}
