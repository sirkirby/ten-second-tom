namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Defines well-known environment names recognized by the application.
/// Using constants prevents subtle casing / spelling issues.
/// </summary>
public static class EnvironmentNames
{
    /// <summary>
    /// Development environment.
    /// </summary>
    public const string Development = "Development";

    /// <summary>
    /// Production environment (default when none specified).
    /// </summary>
    public const string Production = "Production";

    /// <summary>
    /// Staging / pre-production environment.
    /// </summary>
    public const string Staging = "Staging";

    /// <summary>
    /// All known environment names.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Development,
        Production,
        Staging
    ];
}
