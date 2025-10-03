namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Provides context about output formatting preferences for CLI commands.
/// </summary>
public sealed class OutputContext
{
    /// <summary>
    /// Gets a value indicating whether JSON output format is enabled.
    /// </summary>
    public bool JsonOutputEnabled { get; init; }

    /// <summary>
    /// Gets the shared instance with default settings (JSON disabled).
    /// </summary>
    public static OutputContext Default { get; } = new OutputContext { JsonOutputEnabled = false };
}
