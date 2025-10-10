using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Commands;

/// <summary>
/// Command to view or modify individual configuration settings
/// Hybrid command/query for configuration management
/// </summary>
public sealed record ConfigCommand
{
    /// <summary>
    /// Gets the action to perform (Show, Set, Reset, Validate)
    /// </summary>
    public ConfigAction Action { get; init; } = ConfigAction.Show;

    /// <summary>
    /// Gets the setting name to modify (required for Set action)
    /// Valid names: llm-provider, api-key, memory-directory, ssh-key-path, log-level, retention-days
    /// </summary>
    public string? SettingName { get; init; }

    /// <summary>
    /// Gets the new value for the setting (required for Set action)
    /// </summary>
    public string? SettingValue { get; init; }

    /// <summary>
    /// Gets whether to display last 4 characters of secrets (for Show action)
    /// </summary>
    public bool ShowSecrets { get; init; }
}
