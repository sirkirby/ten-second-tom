namespace TenSecondTom.Infrastructure.Cli;

using Spectre.Console;

/// <summary>
/// Provides ASCII art logo and branding for the Ten Second Tom CLI application.
/// </summary>
internal static class Logo
{
    /// <summary>
    /// Displays the Ten Second Tom ASCII logo with tagline.
    /// </summary>
    /// <param name="suppressOutput">If true, suppresses logo output (e.g., for JSON mode).</param>
    public static void Display(bool suppressOutput = false)
    {
        if (suppressOutput)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new FigletText("Ten Second Tom")
                .LeftJustified()
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[dim]Your personal memory assistant[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Gets the version information for display.
    /// </summary>
    /// <returns>Version string with application metadata.</returns>
    public static string GetVersionInfo()
    {
        var version = typeof(Logo).Assembly.GetName().Version;
        return $"Ten Second Tom v{version?.Major}.{version?.Minor}.{version?.Build ?? 0}";
    }

    /// <summary>
    /// Displays the logo with version information.
    /// </summary>
    /// <param name="suppressOutput">If true, suppresses logo output (e.g., for JSON mode).</param>
    public static void DisplayWithVersion(bool suppressOutput = false)
    {
        Display(suppressOutput);
        
        if (!suppressOutput)
        {
            AnsiConsole.MarkupLine($"[dim]{GetVersionInfo()}[/]");
            AnsiConsole.WriteLine();
        }
    }
}
