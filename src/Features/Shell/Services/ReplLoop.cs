using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Shared.Models;
using ShellCommandResult = TenSecondTom.Features.Shell.Models.CommandResult;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Manages the Read-Eval-Print Loop for the shell mode.
/// </summary>
public interface IReplLoop
{
    /// <summary>
    /// Runs the REPL loop until the user quits or cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>Exit code (0 for success).</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the REPL loop with Spectre.Console for rich terminal UI.
/// </summary>
public sealed class ReplLoop : IReplLoop
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IAutocompleteEngine _autocompleteEngine;
    private readonly IOutputPaginator _paginator;
    private readonly ILogger<ReplLoop> _logger;
    private readonly CommandAutoCompleteSource _autoCompleteSource;

    public ReplLoop(
        IServiceScopeFactory scopeFactory,
        ISessionManager sessionManager,
        IAutocompleteEngine autocompleteEngine,
        IOutputPaginator paginator,
        ILogger<ReplLoop> logger)
    {
        _scopeFactory = scopeFactory;
        _sessionManager = sessionManager;
        _autocompleteEngine = autocompleteEngine;
        _paginator = paginator;
        _logger = logger;
        _autoCompleteSource = new CommandAutoCompleteSource(autocompleteEngine);
    }

    /// <inheritdoc/>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Start session
            _sessionManager.StartSession();
            _logger.LogInformation("Shell session started");

            // Display banner
            DisplayBanner();

            bool shouldExit = false;

            // Main REPL loop
            while (!shouldExit && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Read input with autocomplete support
                    string? input = ReadInput();

                    // Skip empty input
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        continue;
                    }

                    // Check for quit commands
                    if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
                    {
                        shouldExit = true;
                        AnsiConsole.MarkupLine("[dim]Goodbye![/]");
                        continue;
                    }

                    // Create a new scope for each command execution
                    // This ensures that scoped services (like IOptionsSnapshot) get fresh instances
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var router = scope.ServiceProvider.GetRequiredService<ICommandRouter>();

                        // Route and execute command
                        var result = await router.RouteAsync(input, cancellationToken).ConfigureAwait(false);

                        // Add to history
                        _sessionManager.AddToHistory(
                            input,
                            result.IsSuccess,
                            result.Message == "(interrupted)",
                            result.Message);

                        // Display result feedback
                        DisplayResult(result);
                    }

                    // Clear any buffered console input left by the command
                    // This ensures clean state for the next prompt (especially after voice commands)
                    while (Console.KeyAvailable)
                    {
                        Console.ReadKey(intercept: true);
                    }

                    // Add visual spacing between commands
                    AnsiConsole.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    AnsiConsole.MarkupLine("[yellow]Operation cancelled[/]");
                    // Continue loop - don't exit on single command cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in REPL loop");
                    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                }
            }

            // End session
            _sessionManager.EndSession();
            _logger.LogInformation("Shell session ended");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in REPL loop");
            AnsiConsole.MarkupLine($"[red]Fatal error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    /// <summary>
    /// Displays the startup banner with logo, name, and version.
    /// </summary>
    private void DisplayBanner()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new FigletText("Ten Second Tom")
                .LeftJustified()
                .Color(Color.Cyan1));

        var version = typeof(ReplLoop).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        AnsiConsole.MarkupLine($"[dim]Version {version} - Your personal memory assistant[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Type [cyan]/help[/] for commands, [cyan]/quit[/] to exit[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Reads user input with autocomplete support.
    /// </summary>
    private string? ReadInput()
    {
        // Show helpful hint on first line
        var prompt = new TextPrompt<string>("[cyan]>[/] [dim](Type /help for commands)[/]")
            .AllowEmpty()
            .ShowDefaultValue(false);

        var input = AnsiConsole.Prompt(prompt);

        // If user typed partial command starting with '/', show matching suggestions
        if (!string.IsNullOrWhiteSpace(input) && input.StartsWith('/') && input.Length > 1)
        {
            var suggestions = _autoCompleteSource.GetSuggestions(input).ToList();
            
            // Show suggestions if we have matches (changed from <=3 to show all matches)
            if (suggestions.Count > 0)
            {
                AnsiConsole.MarkupLine($"[dim]  💡 Did you mean: {string.Join(" | ", suggestions)}[/]");
            }
        }

        return input;
    }

    /// <summary>
    /// Displays the result of a command execution.
    /// </summary>
    private void DisplayResult(ShellCommandResult result)
    {
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
        {
            if (result.Message == "(interrupted)")
            {
                AnsiConsole.MarkupLine("[yellow]Command interrupted[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.Message)}");
            }
        }
        else if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message) && result.Message != "(interrupted)")
        {
            // Success message if provided
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(result.Message)}[/]");
        }

        // Note: Actual command output is handled by individual command handlers
        // This only displays router-level feedback
    }
}
