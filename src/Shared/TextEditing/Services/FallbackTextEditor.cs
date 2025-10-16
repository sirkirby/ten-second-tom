using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Shared.TextEditing.Services;

/// <summary>
/// Wrapper editor that tries Terminal.Gui first, then falls back to StreamBasedTextEditor on failure.
/// This handles cases where terminal detection fails but Terminal.Gui can't actually initialize.
/// </summary>
public sealed class FallbackTextEditor : IInteractiveTextEditor
{
    private readonly TerminalGuiTextEditor _primaryEditor;
    private readonly StreamBasedTextEditor _fallbackEditor;
    private readonly ILogger<FallbackTextEditor> _logger;
    private bool _useFallback;

    public FallbackTextEditor(
        TerminalGuiTextEditor primaryEditor,
        StreamBasedTextEditor fallbackEditor,
        ILogger<FallbackTextEditor> logger)
    {
        _primaryEditor = primaryEditor;
        _fallbackEditor = fallbackEditor;
        _logger = logger;
    }

    public async Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        // If we've already determined fallback is needed, use it directly
        if (_useFallback)
        {
            return await _fallbackEditor.EditAsync(initialContent, configuration, cancellationToken);
        }

        try
        {
            _logger.LogDebug("Attempting to use TerminalGuiTextEditor");
            return await _primaryEditor.EditAsync(initialContent, configuration, cancellationToken);
        }
        catch (Exception ex) when (
            ex.Message.Contains("Terminal.Gui Application.Top is null", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("terminal may not support TUI", StringComparison.OrdinalIgnoreCase) ||
            ex.GetType().Name == "EditorException")
        {
            _logger.LogInformation(
                "Interactive TUI not available in this terminal, using simplified editor. " +
                "(Terminal: TERM={Term})",
                Environment.GetEnvironmentVariable("TERM") ?? "not set"
            );

            // Mark that we should use fallback from now on
            _useFallback = true;

            // Retry with fallback editor
            return await _fallbackEditor.EditAsync(initialContent, configuration, cancellationToken);
        }
    }
}

