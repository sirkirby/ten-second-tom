using System.CommandLine;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Interface for feature slices to provide root-level commands.
/// Features implementing this interface will have their commands automatically discovered
/// and registered via assembly scanning, following VSA principles.
/// </summary>
/// <remarks>
/// This pattern eliminates tight coupling between CommandRegistry and feature implementations.
/// Features remain independent and can be added/removed without modifying infrastructure code.
///
/// Example implementation:
/// <code>
/// public sealed class MyFeatureCommandBuilder : ICommandBuilder
/// {
///     public int Priority => 10; // Controls ordering in help menu
///
///     public Command? BuildCommand(IServiceProvider serviceProvider, Option&lt;bool&gt; jsonOutputOption)
///     {
///         var command = new Command("myfeature", "Description of my feature");
///         command.Options.Add(jsonOutputOption);
///         command.SetHandler(async () => { /* implementation */ });
///         return command;
///     }
/// }
/// </code>
/// </remarks>
public interface ICommandBuilder
{
    /// <summary>
    /// Gets the priority for command ordering in help menu and discovery.
    /// Lower values appear first. Use multiples of 10 to allow insertion between commands.
    /// </summary>
    /// <remarks>
    /// Suggested ranges:
    /// - 10-30: Primary commands (today, thisweek, record)
    /// - 40-60: Secondary commands (generate, search)
    /// - 70-90: Management commands (setup, config, auth)
    /// - 100+: Utility commands (shell, help, version)
    /// </remarks>
    int Priority { get; }

    /// <summary>
    /// Builds a root-level command for this feature slice.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured command, or null if this feature doesn't provide a command.</returns>
    Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption);
}
