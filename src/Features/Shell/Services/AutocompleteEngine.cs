using TenSecondTom.Features.Shell.Models;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Provides command autocomplete suggestions based on user input.
/// </summary>
public interface IAutocompleteEngine
{
    /// <summary>
    /// Gets ranked autocomplete suggestions for the given input prefix.
    /// </summary>
    /// <param name="input">The partial command input (e.g., "/to").</param>
    /// <returns>Up to 10 suggestions ranked by match score.</returns>
    IReadOnlyList<AutocompleteSuggestion> GetSuggestions(string input);
}

/// <summary>
/// Implements autocomplete logic with match scoring and ranking.
/// </summary>
public sealed class AutocompleteEngine : IAutocompleteEngine
{
    private readonly CommandMetadata[] _commands;
    private readonly Dictionary<string, CommandMetadata> _aliasMap;

    public AutocompleteEngine()
    {
        _commands = CommandMetadata.CommandCatalog;
        _aliasMap = BuildAliasMap(_commands);
    }

    /// <inheritdoc/>
    public IReadOnlyList<AutocompleteSuggestion> GetSuggestions(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        // Return empty for invalid input
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
            return Array.Empty<AutocompleteSuggestion>();

        var suggestions = new List<AutocompleteSuggestion>();

        // Score and collect matches from main commands
        foreach (var command in _commands)
        {
            int score = CalculateMatchScore(input, command.Name);
            if (score > 0)
            {
                suggestions.Add(new AutocompleteSuggestion
                {
                    CommandName = command.Name,
                    HelpText = command.HelpText,
                    MatchScore = score
                });
            }
        }

        // Score and collect matches from aliases
        foreach (var (alias, command) in _aliasMap)
        {
            int score = CalculateMatchScore(input, alias);
            if (score > 0 && !suggestions.Any(s => s.CommandName == alias))
            {
                suggestions.Add(new AutocompleteSuggestion
                {
                    CommandName = alias,
                    HelpText = command.HelpText,
                    MatchScore = score
                });
            }
        }

        // Return top 10, ranked by score (descending)
        return suggestions
            .OrderByDescending(s => s.MatchScore)
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Calculates match score for a command against user input.
    /// Scoring:
    /// - Exact match: 100
    /// - Exact prefix (case-sensitive): 100 - (commandLength - inputLength)
    /// - Case-insensitive prefix: 90 - (commandLength - inputLength)
    /// - Substring match: 50 - position_index
    /// - No match: 0
    /// </summary>
    private static int CalculateMatchScore(string input, string commandName)
    {
        // Exact match
        if (string.Equals(input, commandName, StringComparison.Ordinal))
            return 100;

        // Exact prefix match (case-sensitive)
        if (commandName.StartsWith(input, StringComparison.Ordinal))
            return 100 - (commandName.Length - input.Length);

        // Case-insensitive prefix match
        if (commandName.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            return 90 - (commandName.Length - input.Length);

        // Substring match
        int position = commandName.IndexOf(input, StringComparison.OrdinalIgnoreCase);
        if (position > 0)
            return Math.Max(50 - position, 1);

        return 0;
    }

    /// <summary>
    /// Builds a dictionary mapping aliases to their corresponding command metadata.
    /// </summary>
    private static Dictionary<string, CommandMetadata> BuildAliasMap(CommandMetadata[] commands)
    {
        var map = new Dictionary<string, CommandMetadata>();

        foreach (var command in commands)
        {
            if (command.Aliases != null)
            {
                foreach (var alias in command.Aliases)
                {
                    map[alias] = command;
                }
            }
        }

        return map;
    }
}
