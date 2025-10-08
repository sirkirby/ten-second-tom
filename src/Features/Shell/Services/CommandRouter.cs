using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using TenSecondTom.Features.Shell.Models;
using ShellCommandResult = TenSecondTom.Features.Shell.Models.CommandResult;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Routes shell commands to their appropriate handlers.
/// </summary>
public interface ICommandRouter
{
    /// <summary>
    /// Routes a command string to the appropriate handler.
    /// </summary>
    /// <param name="commandLine">The complete command line (e.g., "/today" or "/search query").</param>
    /// <param name="cancellationToken">Cancellation token for interrupting execution.</param>
    /// <returns>Result of the command execution.</returns>
    Task<ShellCommandResult> RouteAsync(string commandLine, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements command routing with System.CommandLine integration.
/// </summary>
public sealed class CommandRouter : ICommandRouter
{
    private readonly RootCommand _rootCommand;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(
        IServiceProvider serviceProvider,
        ILogger<CommandRouter> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Build the root command (will be used for parsing only in shell mode)
        _rootCommand = Infrastructure.Cli.CommandRegistry.BuildRootCommand(serviceProvider);
    }

    /// <inheritdoc/>
    public async Task<ShellCommandResult> RouteAsync(string commandLine, CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return ShellCommandResult.Failure("Command cannot be empty");
        }

        // Commands must start with '/'
        if (!commandLine.StartsWith('/'))
        {
            return ShellCommandResult.Failure("Commands must start with '/' (e.g., /help, /today)");
        }

        try
        {
            // Remove leading slash for System.CommandLine parsing
            string normalizedCommand = commandLine[1..].Trim();
            
            // Check for empty command after slash
            if (string.IsNullOrWhiteSpace(normalizedCommand))
            {
                return ShellCommandResult.Failure("Command cannot be empty. Type /help for available commands.");
            }
            
            // Special handling for /quit and /exit - these should terminate the shell
            if (normalizedCommand.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                normalizedCommand.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return ShellCommandResult.Success("Exiting shell...");
            }

            // Parse the command
            ParseResult parseResult = _rootCommand.Parse(normalizedCommand);

            // Check for parse errors
            if (parseResult.Errors.Any())
            {
                var errorMessages = string.Join(", ", parseResult.Errors.Select(e => e.Message));
                _logger.LogWarning("Command parse failed: {Command}. Errors: {Errors}", commandLine, errorMessages);
                
                // Check if this is an unknown command
                if (parseResult.Errors.Any(e => e.Message.Contains("Unrecognized", StringComparison.OrdinalIgnoreCase)))
                {
                    return ShellCommandResult.Failure($"Unknown command: {commandLine}. Type /help for available commands.");
                }
                
                return ShellCommandResult.Failure($"Invalid command: {errorMessages}");
            }

            // Execute the command
            int exitCode = await parseResult.InvokeAsync().ConfigureAwait(false);

            if (exitCode == 0)
            {
                return ShellCommandResult.Success();
            }
            
            // Check if this is an authentication error (exit code 2)
            if (exitCode == 2)
            {
                return ShellCommandResult.Failure("Authentication required. Please run /login to authenticate.");
            }

            return ShellCommandResult.Failure($"Command failed with exit code {exitCode}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Command cancelled: {Command}", commandLine);
            return ShellCommandResult.Interrupted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", commandLine);
            return ShellCommandResult.Failure($"Command execution failed: {ex.Message}", ex);
        }
    }
}
