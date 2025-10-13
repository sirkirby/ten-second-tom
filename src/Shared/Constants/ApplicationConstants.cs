namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Defines application-wide constants for branding and versioning.
/// Centralizing these values eliminates magic strings and ensures
/// consistency across the application.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API for use across features")]
public static class ApplicationConstants
{
    /// <summary>
    /// The application name used in branding and display.
    /// </summary>
    public const string ApplicationName = "Ten Second Tom";

    /// <summary>
    /// The application name with version prefix used in version information display.
    /// </summary>
    public const string ApplicationNameWithVersionPrefix = "Ten Second Tom v";
}
