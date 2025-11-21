namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Defines standard command names used throughout the application.
/// Centralizing these values eliminates magic strings and ensures
/// consistency across handlers, models, and tests.
/// </summary>
public static class CommandNames
{
    /// <summary>
    /// Command name for capturing today's reflection entry.
    /// </summary>
    public const string Today = "today";

    /// <summary>
    /// Command name for generating / storing a weekly review.
    /// </summary>
    public const string ThisWeek = "thisweek";

    /// <summary>
    /// Command name for searching memory entries.
    /// </summary>
    public const string Search = "search";

    /// <summary>
    /// Command name for authenticating / creating a session.
    /// </summary>
    public const string Login = "login";

    /// <summary>
    /// Command name for logging out / ending a session.
    /// </summary>
    public const string Logout = "logout";

    /// <summary>
    /// Command name for generating output from recordings using LLM templates.
    /// </summary>
    public const string Generate = "generate";

    /// <summary>
    /// Command name for recording audio with transcription.
    /// </summary>
    public const string Record = "record";

    /// <summary>
    /// Command name for capturing quick notes.
    /// </summary>
    public const string Note = "note";

    /// <summary>
    /// Gets all valid command names.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Today,
        ThisWeek,
        Search,
        Login,
        Logout,
        Generate,
        Record,
        Note
    ];
}
