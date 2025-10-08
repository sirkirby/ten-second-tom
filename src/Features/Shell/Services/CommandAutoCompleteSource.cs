using TenSecondTom.Features.Shell.Models;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Adapter that provides autocomplete suggestions for shell commands.
/// Integrates IAutocompleteEngine with Spectre.Console TextPrompt functionality.
/// </summary>
internal sealed class CommandAutoCompleteSource
{
    private readonly IAutocompleteEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandAutoCompleteSource"/> class.
    /// </summary>
    /// <param name="engine">The autocomplete engine to delegate to.</param>
    public CommandAutoCompleteSource(IAutocompleteEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Gets autocomplete suggestions for the given input text.
    /// </summary>
    /// <param name="text">The current input text to complete.</param>
    /// <returns>Collection of matching command suggestions with help text.</returns>
    public IEnumerable<string> GetSuggestions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Enumerable.Empty<string>();
        }

        // Get suggestions from the engine
        var suggestions = _engine.GetSuggestions(text);

        // Format as "command - help text" for display
        return suggestions.Select(s => $"{s.CommandName} - {s.HelpText}");
    }

    /// <summary>
    /// Gets just the command names for autocomplete completion.
    /// </summary>
    /// <param name="text">The current input text to complete.</param>
    /// <returns>Collection of matching command names only.</returns>
    public IEnumerable<string> GetCommandNames(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Enumerable.Empty<string>();
        }

        // Get suggestions from the engine
        var suggestions = _engine.GetSuggestions(text);

        // Return just the command names
        return suggestions.Select(s => s.CommandName);
    }
}
