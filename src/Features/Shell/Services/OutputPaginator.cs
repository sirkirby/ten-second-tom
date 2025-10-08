using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Handles pagination of long output to fit terminal height.
/// </summary>
public interface IOutputPaginator
{
    /// <summary>
    /// Displays content with automatic pagination if needed.
    /// </summary>
    /// <param name="lines">Lines of content to display.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisplayAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements output pagination with terminal height detection.
/// Algorithm: If lines less than or equal to (terminal height - 5), display full output.
/// Otherwise, use Spectre.Console pager with Space for next page, q to quit.
/// </summary>
public sealed class OutputPaginator : IOutputPaginator
{
    private const int ReservedLines = 5; // For prompt, margins, etc.
    private readonly ILogger<OutputPaginator> _logger;

    public OutputPaginator(ILogger<OutputPaginator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DisplayAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        var lineList = lines.ToList();
        
        if (lineList.Count == 0)
        {
            // Nothing to display
            return;
        }

        try
        {
            // Detect terminal height dynamically
            int terminalHeight = Console.WindowHeight;
            int availableLines = Math.Max(terminalHeight - ReservedLines, 10); // Min 10 lines

            _logger.LogDebug("Terminal height: {Height}, Available lines: {Available}, Content lines: {Content}",
                terminalHeight, availableLines, lineList.Count);

            if (lineList.Count <= availableLines)
            {
                // Display all content without pagination
                foreach (var line in lineList)
                {
                    AnsiConsole.MarkupLine(Markup.Escape(line));
                }
            }
            else
            {
                // Activate pagination
                await DisplayPagedAsync(lineList, availableLines, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error displaying paginated output");
            
            // Fallback: Display without pagination
            foreach (var line in lineList)
            {
                AnsiConsole.MarkupLine(Markup.Escape(line));
            }
        }
    }

    /// <summary>
    /// Displays content with page-by-page navigation.
    /// </summary>
    private async Task DisplayPagedAsync(List<string> lines, int linesPerPage, CancellationToken cancellationToken)
    {
        int totalPages = (int)Math.Ceiling((double)lines.Count / linesPerPage);
        int currentPage = 0;

        while (currentPage < totalPages && !cancellationToken.IsCancellationRequested)
        {
            // Clear console for new page (optional - may prefer scrolling)
            // AnsiConsole.Clear();

            // Display current page
            int startIndex = currentPage * linesPerPage;
            int endIndex = Math.Min(startIndex + linesPerPage, lines.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                AnsiConsole.MarkupLine(Markup.Escape(lines[i]));
            }

            // Show pagination controls
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Page {currentPage + 1}/{totalPages} - Press [cyan]Space[/] for next page, [cyan]q[/] to quit[/]");

            // Wait for user input
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Q || key.KeyChar == 'q')
            {
                // Quit pagination
                break;
            }
            else if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Enter)
            {
                // Next page
                currentPage++;
            }
            else if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.PageUp)
            {
                // Previous page
                currentPage = Math.Max(0, currentPage - 1);
            }
            else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.PageDown)
            {
                // Next page (same as Space)
                currentPage++;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
