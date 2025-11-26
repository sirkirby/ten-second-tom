using System.CommandLine;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Builds the 'config transcribe' subcommand for the /config command.
/// Auto-discovered via assembly scanning of IConfigSubcommandBuilder implementations.
/// This provides an alias for 'tom transcribe config'.
/// </summary>
public sealed class TranscribeConfigCommandBuilder : IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'transcribe' subcommand for /config.
    /// </summary>
    public Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        // Delegate to the shared builder with the command named 'transcribe' for /config transcribe
        return TranscribeConfigSubcommandBuilder.BuildConfigCommandWithName("transcribe", serviceProvider, jsonOutputOption);
    }
}
