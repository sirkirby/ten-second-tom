using System.CommandLine;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Interface for feature slices to provide config subcommands.
/// Features implementing this interface will have their subcommands automatically discovered
/// and registered via assembly scanning, following VSA principles.
/// </summary>
public interface IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds a config subcommand for this feature slice.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured subcommand, or null if this feature doesn't provide a config subcommand.</returns>
    Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption);
}

