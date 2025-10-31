namespace TenSecondTom.Features.Search.Models;

/// <summary>
/// DTO for search command JSON output data.
/// </summary>
public sealed class SearchResultData
{
    /// <summary>
    /// Gets or sets the search query text.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Gets or sets the optional start date filter (ISO 8601 format).
    /// </summary>
    public string? FromDate { get; init; }

    /// <summary>
    /// Gets or sets the optional end date filter (ISO 8601 format).
    /// </summary>
    public string? ToDate { get; init; }

    /// <summary>
    /// Gets or sets the total number of results found.
    /// </summary>
    public required int ResultCount { get; init; }

    /// <summary>
    /// Gets or sets the list of search results.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO for JSON serialization")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "DTO for JSON serialization")]
    public required List<SearchResultEntry> Results { get; init; }
}

/// <summary>
/// DTO for individual search result entry in JSON output.
/// </summary>
public sealed class SearchResultEntry
{
    /// <summary>
    /// Gets or sets the entry number.
    /// </summary>
    public required int EntryNumber { get; init; }

    /// <summary>
    /// Gets or sets the command type ("today" or "thisweek").
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets or sets the timestamp in ISO 8601 format.
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the file path where the entry is stored.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets or sets the user's input text.
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// Gets or sets the LLM-generated response.
    /// </summary>
    public required string LlmResponse { get; init; }
}
