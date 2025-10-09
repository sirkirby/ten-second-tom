using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup.Commands;

/// <summary>
/// Command to initiate or re-run the guided setup wizard
/// Implements IRequest pattern for command handling
/// </summary>
public sealed record SetupCommand
{
    /// <summary>
    /// Gets whether to force setup even if configuration exists
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Gets whether to run in non-interactive mode (use defaults, no prompts)
    /// </summary>
    public bool NonInteractive { get; init; }

    /// <summary>
    /// Gets the existing configuration to use as defaults
    /// </summary>
    public ConfigurationSettings? ExistingConfiguration { get; init; }
}
