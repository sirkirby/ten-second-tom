namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Actions that can be performed on configuration
/// </summary>
public enum ConfigAction
{
    /// <summary>
    /// Display current configuration
    /// </summary>
    Show,

    /// <summary>
    /// Set a configuration value
    /// </summary>
    Set,

    /// <summary>
    /// Reset configuration to defaults
    /// </summary>
    Reset,

    /// <summary>
    /// Validate current configuration
    /// </summary>
    Validate
}
