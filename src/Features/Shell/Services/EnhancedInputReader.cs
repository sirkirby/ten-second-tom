using System.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Shell.Models;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Enhanced input reader with Tab completion, history navigation, and escape key support.
/// Uses IConsoleKeyReader abstraction for testability.
/// </summary>
public sealed class EnhancedInputReader(
    IConsoleKeyReader consoleKeyReader,
    IAutocompleteEngine autocompleteEngine,
    ISessionManager sessionManager,
    ILogger<EnhancedInputReader> logger) : IEnhancedInputReader
{
    private const string PromptText = "[cyan]>[/] [dim](Type /help for commands)[/] ";

    /// <inheritdoc/>
    public bool IsAvailable()
    {
        // Enhanced input requires interactive terminal (not redirected input)
        return !consoleKeyReader.IsInputRedirected;
    }

    /// <inheritdoc/>
    public async Task<string?> ReadInputAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            throw new InvalidOperationException("Enhanced input reader is not available. Use IsAvailable() to check first.");
        }

        var buffer = new StringBuilder();
        var cursorPosition = 0;
        var historyIndex = -1; // -1 = not navigating history
        var autocompleteIndex = -1; // -1 = not cycling suggestions
        IReadOnlyList<AutocompleteSuggestion>? currentSuggestions = null;
        var savedBufferBeforeHistory = string.Empty; // Save buffer when starting history navigation

        // Display initial prompt
        RenderPrompt(buffer.ToString(), cursorPosition);

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check for key availability to allow cancellation checks
            while (!consoleKeyReader.KeyAvailable)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            var keyInfo = consoleKeyReader.ReadKey(intercept: true);

            // Handle Escape key - cancel input
            if (keyInfo.Key == ConsoleKey.Escape)
            {
                logger.LogDebug("Escape key pressed, cancelling input");
                ClearCurrentLine();
                return null;
            }

            // Handle Ctrl+[ (same as Escape - ASCII 27)
            if (keyInfo.KeyChar == '\x1b')
            {
                logger.LogDebug("Ctrl+[ pressed, cancelling input");
                ClearCurrentLine();
                return null;
            }

            // Handle Enter key - submit input
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine(); // Move to next line
                return buffer.ToString();
            }

            // Handle Tab key - autocomplete
            if (keyInfo.Key == ConsoleKey.Tab)
            {
                HandleTabCompletion(buffer, ref cursorPosition, ref autocompleteIndex, ref currentSuggestions, keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift));
                RenderPrompt(buffer.ToString(), cursorPosition);
                continue;
            }

            // Handle Arrow Up - history navigation backward
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                HandleHistoryNavigation(buffer, ref cursorPosition, ref historyIndex, ref savedBufferBeforeHistory, navigateBackward: true);
                ResetAutocompleteState(ref autocompleteIndex, ref currentSuggestions);
                RenderPrompt(buffer.ToString(), cursorPosition);
                continue;
            }

            // Handle Arrow Down - history navigation forward
            if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                HandleHistoryNavigation(buffer, ref cursorPosition, ref historyIndex, ref savedBufferBeforeHistory, navigateBackward: false);
                ResetAutocompleteState(ref autocompleteIndex, ref currentSuggestions);
                RenderPrompt(buffer.ToString(), cursorPosition);
                continue;
            }

            // Handle Left Arrow - move cursor left
            if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (cursorPosition > 0)
                {
                    cursorPosition--;
                    RenderPrompt(buffer.ToString(), cursorPosition);
                }
                continue;
            }

            // Handle Right Arrow - move cursor right
            if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (cursorPosition < buffer.Length)
                {
                    cursorPosition++;
                    RenderPrompt(buffer.ToString(), cursorPosition);
                }
                continue;
            }

            // Handle Home key - move cursor to start
            if (keyInfo.Key == ConsoleKey.Home)
            {
                cursorPosition = 0;
                RenderPrompt(buffer.ToString(), cursorPosition);
                continue;
            }

            // Handle End key - move cursor to end
            if (keyInfo.Key == ConsoleKey.End)
            {
                cursorPosition = buffer.Length;
                RenderPrompt(buffer.ToString(), cursorPosition);
                continue;
            }

            // Handle Backspace key - delete character before cursor
            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (cursorPosition > 0)
                {
                    buffer.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                    ResetAutocompleteState(ref autocompleteIndex, ref currentSuggestions);
                    ResetHistoryState(ref historyIndex, ref savedBufferBeforeHistory);
                    RenderPrompt(buffer.ToString(), cursorPosition);
                }
                continue;
            }

            // Handle Delete key - delete character at cursor
            if (keyInfo.Key == ConsoleKey.Delete)
            {
                if (cursorPosition < buffer.Length)
                {
                    buffer.Remove(cursorPosition, 1);
                    ResetAutocompleteState(ref autocompleteIndex, ref currentSuggestions);
                    ResetHistoryState(ref historyIndex, ref savedBufferBeforeHistory);
                    RenderPrompt(buffer.ToString(), cursorPosition);
                }
                continue;
            }

            // Handle regular character input
            if (!char.IsControl(keyInfo.KeyChar))
            {
                // If cycling through suggestions and user types, accept current suggestion first
                if (autocompleteIndex >= 0 && currentSuggestions != null && autocompleteIndex < currentSuggestions.Count)
                {
                    // Accept the suggestion - buffer already has it, just reset state
                    ResetAutocompleteState(ref autocompleteIndex, ref currentSuggestions);
                }

                buffer.Insert(cursorPosition, keyInfo.KeyChar);
                cursorPosition++;
                ResetHistoryState(ref historyIndex, ref savedBufferBeforeHistory);
                RenderPrompt(buffer.ToString(), cursorPosition);
            }
        }

        return null;
    }

    /// <summary>
    /// Handles Tab key completion cycling.
    /// </summary>
    private void HandleTabCompletion(
        StringBuilder buffer,
        ref int cursorPosition,
        ref int autocompleteIndex,
        ref IReadOnlyList<AutocompleteSuggestion>? currentSuggestions,
        bool shiftPressed)
    {
        var input = buffer.ToString();

        // Only autocomplete commands starting with '/'
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
        {
            return;
        }

        // Get suggestions if not already cycling
        if (autocompleteIndex < 0 || currentSuggestions == null)
        {
            currentSuggestions = autocompleteEngine.GetSuggestions(input);
            if (currentSuggestions.Count == 0)
            {
                logger.LogDebug("No autocomplete suggestions for input: {Input}", input);
                return;
            }
            autocompleteIndex = 0;
        }
        else
        {
            // Cycle through suggestions
            if (shiftPressed)
            {
                // Shift+Tab: cycle backward
                autocompleteIndex--;
                if (autocompleteIndex < 0)
                {
                    autocompleteIndex = currentSuggestions.Count - 1;
                }
            }
            else
            {
                // Tab: cycle forward
                autocompleteIndex++;
                if (autocompleteIndex >= currentSuggestions.Count)
                {
                    autocompleteIndex = 0;
                }
            }
        }

        // Replace buffer with current suggestion
        var suggestion = currentSuggestions[autocompleteIndex];
        buffer.Clear();
        buffer.Append(suggestion.CommandName);
        cursorPosition = buffer.Length;

        logger.LogDebug("Autocomplete: {Index}/{Total} - {Command}",
            autocompleteIndex + 1, currentSuggestions.Count, suggestion.CommandName);
    }

    /// <summary>
    /// Handles Arrow Up/Down history navigation.
    /// </summary>
    private void HandleHistoryNavigation(
        StringBuilder buffer,
        ref int cursorPosition,
        ref int historyIndex,
        ref string savedBufferBeforeHistory,
        bool navigateBackward)
    {
        IReadOnlyList<CommandHistoryEntry> history;
        try
        {
            history = sessionManager.GetHistory();
        }
        catch (InvalidOperationException)
        {
            // No active session - ignore history navigation
            return;
        }

        if (history.Count == 0)
        {
            return; // Empty history - no-op
        }

        if (navigateBackward) // Arrow Up
        {
            if (historyIndex < 0)
            {
                // Starting history navigation - save current buffer
                savedBufferBeforeHistory = buffer.ToString();
                historyIndex = history.Count - 1; // Start at newest command
            }
            else if (historyIndex > 0)
            {
                historyIndex--;
            }
            // else at oldest command - no-op

            // Update buffer with historical command
            buffer.Clear();
            buffer.Append(history[historyIndex].Command);
            cursorPosition = buffer.Length;
        }
        else // Arrow Down
        {
            if (historyIndex < 0)
            {
                return; // Not navigating history - no-op
            }

            if (historyIndex < history.Count - 1)
            {
                historyIndex++;
                buffer.Clear();
                buffer.Append(history[historyIndex].Command);
                cursorPosition = buffer.Length;
            }
            else
            {
                // At newest command - return to saved buffer
                historyIndex = -1;
                buffer.Clear();
                buffer.Append(savedBufferBeforeHistory);
                cursorPosition = buffer.Length;
                savedBufferBeforeHistory = string.Empty;
            }
        }

        logger.LogDebug("History navigation: index={Index}, command={Command}",
            historyIndex, buffer.ToString());
    }

    /// <summary>
    /// Resets autocomplete cycling state.
    /// </summary>
    private static void ResetAutocompleteState(ref int autocompleteIndex, ref IReadOnlyList<AutocompleteSuggestion>? currentSuggestions)
    {
        autocompleteIndex = -1;
        currentSuggestions = null;
    }

    /// <summary>
    /// Resets history navigation state.
    /// </summary>
    private static void ResetHistoryState(ref int historyIndex, ref string savedBufferBeforeHistory)
    {
        historyIndex = -1;
        savedBufferBeforeHistory = string.Empty;
    }

    /// <summary>
    /// Renders the prompt with current buffer and cursor position.
    /// </summary>
    private static void RenderPrompt(string buffer, int cursorPosition)
    {
        // Clear current line and redraw
        ClearCurrentLine();

        // Write prompt with Spectre.Console markup
        AnsiConsole.Markup(PromptText);

        // Write buffer text
        Console.Write(buffer);

        // Position cursor
        var promptLength = GetVisibleLength(PromptText);
        var targetColumn = promptLength + cursorPosition;
        Console.SetCursorPosition(targetColumn, Console.CursorTop);
    }

    /// <summary>
    /// Clears the current console line.
    /// </summary>
    private static void ClearCurrentLine()
    {
        Console.Write('\r');
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.Write('\r');
    }

    /// <summary>
    /// Gets the visible length of a string with Spectre.Console markup removed.
    /// </summary>
    private static int GetVisibleLength(string markup)
    {
        // Remove markup tags like [cyan], [/], [dim], etc.
        var text = markup;
        while (text.Contains('['))
        {
            var start = text.IndexOf('[');
            var end = text.IndexOf(']', start);
            if (end < 0) break;
            text = text.Remove(start, end - start + 1);
        }
        return text.Length;
    }
}
