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
    /// Directory name for storing today's memory entries.
    /// Located at: {root}/today/
    /// </summary>
    public const string Today = "today";

    /// <summary>
    /// Directory name for storing this week's memory entries.
    /// Located at: {root}/thisweek/
    /// </summary>
    public const string ThisWeek = "thisweek";
}
