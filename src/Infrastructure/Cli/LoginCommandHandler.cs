using Spectre.Console;
using TenSecondTom.Features.Auth.Commands;
using TenSecondTom.Shared.OutputFormatters;
using AuthHandler = TenSecondTom.Features.Auth.Handlers.LoginCommandHandler;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the login CLI command execution.
/// Provides user-friendly output for login operations.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top-level CLI handler must catch all exceptions")]
public static class LoginCommandHandler
{
    /// <summary>
    /// Executes the login command and displays results to the user.
    /// </summary>
    /// <param name="handler">The login command handler.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ExecuteAsync(AuthHandler handler, bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(handler);

        try
        {
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("[blue]→[/] Authenticating with SSH key...");
                AnsiConsole.WriteLine();
            }

            var command = new LoginCommand();
            var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

            if (jsonOutput)
            {
                // JSON output mode
                object? jsonData = result.IsSuccess
                    ? new { sessionId = result.Value.SessionId, createdAt = result.Value.CreatedAt, keyHash = result.Value.SshKeyHash }
                    : null;
                    
                string json = result.IsSuccess
                    ? JsonOutputFormatter.FormatSuccess("login", jsonData, DateTimeOffset.UtcNow)
                    : JsonOutputFormatter.FormatFailure("login", result.Error, DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else if (result.IsSuccess)
            {
                var session = result.Value;
                AnsiConsole.MarkupLine("[green]✓[/] Successfully authenticated!");
                AnsiConsole.WriteLine();
                
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Green)
                    .AddColumn(new TableColumn("[bold]Session Information[/]").Centered());
                
                table.AddRow($"Session ID: [cyan]{session.SessionId}[/]");
                table.AddRow($"Created: [dim]{session.CreatedAt:yyyy-MM-dd HH:mm:ss UTC}[/]");
                table.AddRow($"Key Hash: [dim]{Markup.Escape(session.SshKeyHash)}[/]");
                
                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]You can now use all Ten Second Tom commands.[/]");
            }
            else
            {
                var errorMessage = result.Error ?? "Unknown authentication error";
                AuthenticationErrorFormatter.DisplayAuthenticationError(errorMessage);
            }
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure("login", $"Authentication error: {ex.Message}", DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                var errorMessage = $"An error occurred during authentication: {ex.Message}";
                AuthenticationErrorFormatter.DisplayAuthenticationError(errorMessage);
            }
        }
    }
}
