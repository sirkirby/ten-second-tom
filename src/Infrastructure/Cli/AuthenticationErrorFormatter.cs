using Spectre.Console;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Provides formatted, helpful authentication error messages for CLI users.
/// </summary>
public static class AuthenticationErrorFormatter
{
    /// <summary>
    /// Displays a comprehensive authentication error message with setup instructions.
    /// </summary>
    /// <param name="errorMessage">The error message from the authentication service.</param>
    public static void DisplayAuthenticationError(string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(errorMessage);
        
        AnsiConsole.MarkupLine($"[red]✗ Authentication failed:[/] {Markup.Escape(errorMessage)}");
        AnsiConsole.WriteLine();

        // Determine error type and show relevant guidance
        if (IsAgentError(errorMessage))
        {
            DisplayAgentErrorGuidance();
        }
        else if (IsKeyError(errorMessage))
        {
            DisplayKeyErrorGuidance();
        }
        else
        {
            DisplayGeneralSetupGuidance();
        }
    }

    /// <summary>
    /// Checks if the error is related to SSH agent issues.
    /// </summary>
    private static bool IsAgentError(string errorMessage)
    {
        return errorMessage.Contains("SSH agent", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("SSH_AUTH_SOCK", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("agent not available", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("agent denied", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("Key may not be loaded", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the error is related to SSH key issues.
    /// </summary>
    private static bool IsKeyError(string errorMessage)
    {
        return errorMessage.Contains("SSH key", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("key not found", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("public key", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Displays guidance for SSH agent-related errors.
    /// </summary>
    private static void DisplayAgentErrorGuidance()
    {
        var panel = new Panel(new Markup(
            "[yellow]SSH Agent / Key Loading Issue[/]\n\n" +
            "[bold]For 1Password SSH Agent:[/]\n" +
            "   [dim]1. Open 1Password → New Item → SSH Key[/]\n" +
            "   [dim]2. Import your private key file (~/.ssh/id_ed25519)[/]\n" +
            "   [dim]3. Copy the public key from 1Password[/]\n" +
            "   [dim]4. Update TenSecondTom__Auth__PublicKey in .env[/]\n" +
            "   [dim]5. Retry - 1Password will prompt for approval[/]\n\n" +
            "[bold]For Traditional ssh-agent:[/]\n" +
            "   [dim]# Start agent:[/]\n" +
            "   [cyan]eval \"$(ssh-agent -s)\"[/]\n\n" +
            "   [dim]# Add your key:[/]\n" +
            "   [cyan]ssh-add ~/.ssh/id_ed25519[/]\n\n" +
            "   [dim]# Verify it's loaded:[/]\n" +
            "   [cyan]ssh-add -l[/]\n\n" +
            "[bold]Configure your public key:[/]\n" +
            "   [dim]# Option A: .env file (recommended)[/]\n" +
            "   [cyan]TenSecondTom__Auth__PublicKey=ssh-ed25519 AAAAC3...[/]\n\n" +
            "   [dim]# Option B: Path to .pub file[/]\n" +
            "   [cyan]TenSecondTom__Auth__PublicKeyPath=~/.ssh/id_ed25519.pub[/]\n\n" +
            "[bold]4. Try again:[/]\n" +
            "   [cyan]tom today[/]"))
        {
            Header = new PanelHeader("🔐 Authentication Setup"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]For more details, see: docs/AUTHENTICATION.md[/]");
    }

    /// <summary>
    /// Displays guidance for SSH key-related errors.
    /// </summary>
    private static void DisplayKeyErrorGuidance()
    {
        var panel = new Panel(new Markup(
            "[yellow]SSH Key Not Found[/]\n\n" +
            "[bold]Option 1: Generate a new SSH key (Recommended)[/]\n" +
            "   [cyan]ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519[/]\n" +
            "   [dim]Press Enter for all prompts[/]\n\n" +
            "[bold]Option 2: Use existing SSH key[/]\n" +
            "   [dim]If you have an existing key at a different location:[/]\n" +
            "   [cyan]export TENSECONDTOM_AUTH_PUBLICKEY=\"$(cat /path/to/your/key.pub)\"[/]\n\n" +
            "[bold]Then try again:[/]\n" +
            "   [cyan]tom today[/]"))
        {
            Header = new PanelHeader("🔑 SSH Key Setup"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]For more details, see: docs/AUTHENTICATION.md[/]");
    }

    /// <summary>
    /// Displays general setup guidance when error type is unclear.
    /// </summary>
    private static void DisplayGeneralSetupGuidance()
    {
        var panel = new Panel(new Markup(
            "[yellow]Authentication Setup[/]\n\n" +
            "[bold]Two authentication options available:[/]\n\n" +
            "[bold cyan]Option 1: SSH Agent (Recommended - More Secure)[/]\n" +
            "  1. Start SSH agent: [cyan]eval \"$(ssh-agent -s)\"[/]\n" +
            "  2. Add key: [cyan]ssh-add ~/.ssh/id_ed25519[/]\n" +
            "  3. Configure public key (see below)\n\n" +
            "[bold green]Option 2: File-Based Keys (Simpler)[/]\n" +
            "  1. Generate key: [cyan]ssh-keygen -t ed25519[/]\n" +
            "  2. Save to: [cyan]~/.ssh/id_ed25519[/]\n" +
            "  3. Done! Application will auto-discover\n\n" +
            "[bold]Public Key Configuration:[/]\n" +
            "  [dim]# Environment variable:[/]\n" +
            "  [cyan]export TENSECONDTOM_AUTH_PUBLICKEY=\"$(cat ~/.ssh/id_ed25519.pub)\"[/]\n\n" +
            "  [dim]# Or in appsettings.json:[/]\n" +
            "  [green]{ \"TenSecondTom\": { \"Auth\": { \"PublicKeyPath\": \"~/.ssh/id_ed25519.pub\" } } }[/]"))
        {
            Header = new PanelHeader("🔐 Authentication Required"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]For detailed instructions, see: docs/AUTHENTICATION.md[/]");
    }
}
