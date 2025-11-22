namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed directory name constants used throughout the application.
/// These constants ensure consistency in directory structure across features.
/// </summary>
public static class DirectoryNames
{
    /// <summary>
    /// Default application root directory name.
    /// Used when no custom directory is configured.
    /// </summary>
    public const string ApplicationRoot = "ten-second-tom";

    /// <summary>
    /// Directory name for storing prompt templates.
    /// Located at: {root}/templates/
    /// </summary>
    public const string Templates = "templates";

    /// <summary>
    /// Legacy directory name for storing daily memory entries.
    /// Located at: {root}/today/. Kept for backwards compatibility with
    /// historical storage layouts; new entries are written to <see cref="Note"/>.
    /// </summary>
    public const string Today = "today";

    /// <summary>
    /// Legacy directory name for storing weekly memory entries.
    /// Located at: {root}/thisweek/. Kept for backwards compatibility with
    /// historical storage layouts; new entries are written to <see cref="Note"/>.
    /// </summary>
    public const string ThisWeek = "thisweek";

    /// <summary>
    /// Directory name for storing audio recordings and transcripts.
    /// Located at: {root}/recording/
    /// </summary>
    public const string Recording = "recording";

    /// <summary>
    /// Directory name for storing quick note entries.
    /// Located at: {root}/note/
    /// </summary>
    public const string Note = "note";
}
