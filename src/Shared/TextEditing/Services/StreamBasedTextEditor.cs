using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Shared.TextEditing.Services;

/// <summary>
/// Simple fallback text editor using Console.ReadLine for non-interactive terminals.
/// Supports multi-line input via line-by-line entry with explicit completion.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging for text editor, delegate overhead not justified")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top-level exception handler for editor")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Console application, no synchronization context")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "CLI tool, localization not required")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "User input normalization, lowercase is appropriate")]
public sealed class StreamBasedTextEditor : IInteractiveTextEditor
{
    private readonly InputSanitizer _sanitizer;
    private readonly ILogger<StreamBasedTextEditor> _logger;

    public StreamBasedTextEditor(
        InputSanitizer sanitizer,
        ILogger<StreamBasedTextEditor> logger)
    {
        _sanitizer = sanitizer;
        _logger = logger;
    }

    /// <summary>
    /// Start an interactive editing session with optional initial content.
    /// Uses Console.ReadLine in a loop until EOF (Ctrl+D) or explicit save/cancel.
    /// </summary>
    public async Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var config = configuration ?? EditorConfiguration.Default;
        var session = new TextEditingSession(initialContent);

        _logger.LogDebug(
            "Starting stream-based text editing session {SessionId} with {InitialLength} characters",
            session.SessionId,
            initialContent?.Length ?? 0
        );

        try
        {
            // Display initial content if provided
            if (!string.IsNullOrEmpty(initialContent))
            {
                Console.WriteLine("Editing existing content:");
                Console.WriteLine("---");
                Console.WriteLine(initialContent);
                Console.WriteLine("---");
            }

            // Display instructions
            Console.WriteLine("Enter your text (press Ctrl+D when finished):");
            Console.WriteLine();

            var lines = new List<string>();

            // If we have initial content, start with those lines
            if (!string.IsNullOrEmpty(initialContent))
            {
                lines.AddRange(initialContent.Split('\n'));
            }

            // Read lines until EOF (Ctrl+D)
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Task.Run(() => Console.ReadLine(), cancellationToken);

                if (line == null) // EOF received (Ctrl+D)
                {
                    break;
                }

                lines.Add(line);

                // Check length constraints
                var currentContent = string.Join('\n', lines);
                if (currentContent.Length > config.MaxContentLength)
                {
                    Console.WriteLine($"Warning: Content exceeds maximum length of {config.MaxContentLength} characters");
                    lines.RemoveAt(lines.Count - 1); // Remove last line
                    break;
                }

                if (lines.Count > config.MaxLineCount)
                {
                    Console.WriteLine($"Warning: Content exceeds maximum of {config.MaxLineCount} lines");
                    lines.RemoveAt(lines.Count - 1); // Remove last line
                    break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                session.Complete(EditorOutcome.Cancelled);
                _logger.LogInformation(
                    "Stream-based editing session {SessionId} cancelled via cancellation token",
                    session.SessionId
                );
                return EditorResult.Cancelled(EditorMetadata.FromSession(session));
            }

            // Join lines and sanitize if configured
            var content = string.Join('\n', lines);
            
            if (config.SanitizeInput)
            {
                var sanitized = _sanitizer.Sanitize(content);
                content = sanitized.Content;

                if (sanitized.WasSanitized)
                {
                    _logger.LogInformation(
                        "Sanitized {RemovedCount} characters from stream-based input in session {SessionId}",
                        sanitized.RemovedCount,
                        session.SessionId
                    );
                }
            }

            session.UpdateContent(content);

            // Show preview and prompt for save/cancel
            Console.WriteLine();
            Console.WriteLine("--- Preview ---");
            
            var previewLines = content.Split('\n');
            var linesToShow = config.PreviewLineLimit > 0 && previewLines.Length > config.PreviewLineLimit
                ? config.PreviewLineLimit
                : previewLines.Length;

            for (int i = 0; i < linesToShow; i++)
            {
                Console.WriteLine(previewLines[i]);
            }

            if (config.PreviewLineLimit > 0 && previewLines.Length > config.PreviewLineLimit)
            {
                Console.WriteLine($"... ({previewLines.Length - config.PreviewLineLimit} more lines)");
            }

            Console.WriteLine("---------------");
            Console.WriteLine();
            
            Console.Write("Save this content? [Y/n]: ");
            var rawResponse = Console.ReadLine();
            var response = rawResponse?.Trim().ToLowerInvariant();
            
            // Empty response (just Enter), null (EOF), "y", or "yes" all mean save
            // Only explicit "n" or "no" will cancel
            bool shouldSave = string.IsNullOrEmpty(response) || response == "y" || response == "yes";
            
            if (rawResponse == null)
            {
                _logger.LogDebug("No input available for confirmation (EOF), auto-saving content");
            }

            if (shouldSave)
            {
                session.Complete(EditorOutcome.Saved);

                _logger.LogDebug(
                    "Completed stream-based editing session {SessionId}: Outcome={Outcome}, Duration={Duration}ms, FinalLength={FinalLength}",
                    session.SessionId,
                    EditorOutcome.Saved,
                    session.Duration.TotalMilliseconds,
                    content.Length
                );

                return EditorResult.Saved(content, EditorMetadata.FromSession(session));
            }
            else
            {
                session.Complete(EditorOutcome.Cancelled);

                _logger.LogInformation(
                    "Stream-based editing session {SessionId} cancelled by user at save prompt",
                    session.SessionId
                );

                return EditorResult.Cancelled(EditorMetadata.FromSession(session));
            }
        }
        catch (OperationCanceledException)
        {
            session.Complete(EditorOutcome.Cancelled);

            _logger.LogInformation(
                "Stream-based editing session {SessionId} cancelled via operation cancellation",
                session.SessionId
            );

            return EditorResult.Cancelled(EditorMetadata.FromSession(session));
        }
        catch (Exception ex)
        {
            session.Complete(EditorOutcome.Error);

            _logger.LogError(
                ex,
                "Error in stream-based editing session {SessionId}: {ErrorMessage}",
                session.SessionId,
                ex.Message
            );

            return EditorResult.Error(
                $"Editor error: {ex.Message}",
                EditorMetadata.FromSession(session)
            );
        }
    }
}

