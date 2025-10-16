using Spectre.Console;
using TenSecondTom.Features.Auth.Commands;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Constants;
using AuthHandler = TenSecondTom.Features.Auth.Handlers.LogoutCommandHandler;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the logout CLI command execution.
/// Provides user-friendly output for logout operations.
/// </summary>
public static class LogoutCommandHandler
{
    /// <summary>
    /// Executes the logout command and displays results to the user.
    /// </summary>
    /// <param name="handler">The logout command handler.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ExecuteAsync(AuthHandler handler, bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(handler);

        try
        {
            var command = new LogoutCommand();
            var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

            if (jsonOutput)
            {
                // JSON output mode
                string json = result.IsSuccess
                    ? JsonOutputFormatter.FormatSuccess(CommandNames.Logout, new { message = "Successfully logged out" }, DateTimeOffset.UtcNow)
                    : JsonOutputFormatter.FormatFailure(CommandNames.Logout, result.Error, DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else if (result.IsSuccess)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Successfully logged out.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]You will need to authenticate again to use commands that require access to your memory.[/]");
            }
            else
            {
                var errorMessage = result.Error ?? "Unknown error occurred";
                AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(errorMessage)}");
            }
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure(CommandNames.Logout, $"Logout error: {ex.Message}", DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] An error occurred during logout: {Markup.Escape(ex.Message)}");
            }
        }
    }
}
