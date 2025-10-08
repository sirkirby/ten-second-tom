namespace TenSecondTom.Features.Shell.Models;

/// <summary>
/// Represents a single autocomplete suggestion for display.
/// Ranked by match score for presentation order.
/// </summary>
public sealed record AutocompleteSuggestion
{
    /// <summary>
    /// The command being suggested (e.g., "/today").
    /// </summary>
    public required string CommandName { get; init; }

    /// <summary>
    /// Brief description to show alongside the suggestion.
    /// </summary>
    public required string HelpText { get; init; }

    /// <summary>
    /// Relevance score for ranking (0-100, higher is better match).
    /// </summary>
    public required int MatchScore { get; init; }

    /// <summary>
    /// Validates the suggestion constraints.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(CommandName) &&
        !string.IsNullOrWhiteSpace(HelpText) &&
        MatchScore is >= 0 and <= 100;

    /// <summary>
    /// Formats the suggestion for display in autocomplete list.
    /// </summary>
    public override string ToString() => $"{CommandName} - {HelpText}";
}
