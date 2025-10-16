using Microsoft.Extensions.Logging;
using Terminal.Gui;
using TenSecondTom.Shared.TextEditing.Exceptions;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Shared.TextEditing.Services;

/// <summary>
/// Interactive multi-line text editor using Terminal.Gui TextView.
/// Provides full cursor navigation, multi-line editing, and clipboard support.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Terminal.Gui views are disposed via Application.Shutdown")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Terminal.Gui manages disposal")]
public sealed class TerminalGuiTextEditor : IInteractiveTextEditor
{
    private readonly InputSanitizer _sanitizer;
    private readonly ILogger<TerminalGuiTextEditor> _logger;
    private bool _isInitialized;
    private TextView? _textView;
    private Label? _hintLabel;
    private bool _shouldSave;
    private bool _shouldCancel;

    public TerminalGuiTextEditor(
        InputSanitizer sanitizer,
        ILogger<TerminalGuiTextEditor> logger)
    {
        _sanitizer = sanitizer;
        _logger = logger;
    }

    /// <summary>
    /// Start an interactive editing session with full Terminal.Gui support.
    /// </summary>
    public async Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var config = configuration ?? EditorConfiguration.Default;
        var session = new TextEditingSession(initialContent);

        _logger.LogDebug(
            "Starting Terminal.Gui text editing session {SessionId} with {InitialLength} characters",
            session.SessionId,
            initialContent?.Length ?? 0
        );

        // Initialize Terminal.Gui with TERM override for Warp compatibility
        var originalTerm = Environment.GetEnvironmentVariable("TERM");
        
        _logger.LogDebug(
            "Terminal diagnostics - TERM={Term}, IsInputRedirected={IsInputRedirected}, IsOutputRedirected={IsOutputRedirected}",
            originalTerm ?? "NOT SET",
            Console.IsInputRedirected,
            Console.IsOutputRedirected
        );
        
        // Override problematic TERM values for Terminal.Gui
        if (originalTerm == "dumb" || string.IsNullOrEmpty(originalTerm))
        {
            _logger.LogDebug(
                "TERM={Term} is incompatible with Terminal.Gui. Temporarily setting TERM=xterm-256color.",
                originalTerm ?? "NOT SET"
            );
            Environment.SetEnvironmentVariable("TERM", "xterm-256color");
        }
        
        try
        {
            // Initialize Terminal.Gui
            _logger.LogDebug("Initializing Terminal.Gui with TERM={Term}", 
                Environment.GetEnvironmentVariable("TERM") ?? "not set");
            
            // In v1, Application.Init() automatically creates Application.Top
            Application.Init();
            _isInitialized = true;

            _logger.LogDebug("Terminal.Gui initialized successfully");

            var top = Application.Top;
            if (top == null)
            {
                throw new EditorException("Terminal.Gui Application.Top is null");
            }

            // Create title label showing the prompt/question
            var titleLabel = new Label
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1,
                Text = config.Title ?? "Enter your text:",
                ColorScheme = new ColorScheme
                {
                    Normal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black)
                }
            };

            // Create the text view for editing (below title)
            _textView = new TextView
            {
                X = 0,
                Y = 1, // Start below title
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2, // Leave room for title and hints
                Text = initialContent ?? string.Empty,
                WordWrap = true,
                AllowsTab = true
            };

            // Create hint label with keyboard shortcuts (T022)
            _hintLabel = new Label
            {
                X = 0,
                Y = Pos.Bottom(_textView),
                Width = Dim.Fill(),
                Height = 1,
                Text = config.ShowHints 
                    ? "Ctrl+D: Save & Continue | Ctrl+C: Cancel | Arrows/Home/End: Navigate" 
                    : string.Empty
            };

            top.Add(titleLabel);
            top.Add(_textView);
            top.Add(_hintLabel);

            // Set focus to text view
            _textView.SetFocus();

            // Reset flags
            _shouldSave = false;
            _shouldCancel = false;

            // Add keyboard handling (T019)
            SetupKeyboardHandlers(config);

            // Add a timeout to check for exit conditions
            // This polls every 100ms to see if we should stop
            Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), (loop) =>
            {
                if (_shouldSave || _shouldCancel)
                {
                    _logger.LogDebug("Exit condition detected: _shouldSave={ShouldSave}, _shouldCancel={ShouldCancel} - calling RequestStop()", 
                        _shouldSave, _shouldCancel);
                    Application.RequestStop();
                    return false; // Remove this timeout
                }
                return true; // Keep checking
            });

            // Run the application (v1: uses Application.Top automatically)
            // Note: Application.Run() is synchronous and blocking - this is expected
            Application.Run();
            
            _logger.LogDebug("Application.Run() completed - editor closed");
            
            // Make this method async-compliant (required for IInteractiveTextEditor)
            await Task.CompletedTask;

            // Get the final content
            var content = _textView.Text.ToString() ?? string.Empty;

            // Update session with final content
            session.UpdateContent(content);

            // Shutdown Terminal.Gui
            Application.Shutdown();
            _isInitialized = false;

            // Determine outcome based on flags (will be set by keyboard handlers in T019-T020)
            if (_shouldCancel)
            {
                session.Complete(EditorOutcome.Cancelled);

                _logger.LogDebug(
                    "Terminal.Gui editing session {SessionId} cancelled by user",
                    session.SessionId
                );

                return EditorResult.Cancelled(EditorMetadata.FromSession(session));
            }

            if (_shouldSave)
            {
                // Sanitize input if configured (T021)
                if (config.SanitizeInput)
                {
                    var sanitized = _sanitizer.Sanitize(content);
                    content = sanitized.Content;

                    if (sanitized.WasSanitized)
                    {
                        _logger.LogInformation(
                            "Sanitized {RemovedCount} characters from Terminal.Gui input in session {SessionId}",
                            sanitized.RemovedCount,
                            session.SessionId
                        );
                    }
                }

                session.UpdateContent(content);
                session.Complete(EditorOutcome.Saved);

                _logger.LogDebug(
                    "Completed Terminal.Gui editing session {SessionId}: Outcome={Outcome}, Duration={Duration}ms, FinalLength={FinalLength}",
                    session.SessionId,
                    EditorOutcome.Saved,
                    session.Duration.TotalMilliseconds,
                    content.Length
                );

                return EditorResult.Saved(content, EditorMetadata.FromSession(session));
            }

            // If neither save nor cancel, treat as cancel (safety fallback)
            session.Complete(EditorOutcome.Cancelled);
            return EditorResult.Cancelled(EditorMetadata.FromSession(session));
        }
        catch (OperationCanceledException)
        {
            EnsureShutdown();
            session.Complete(EditorOutcome.Cancelled);

            _logger.LogDebug(
                "Terminal.Gui editing session {SessionId} cancelled via operation cancellation",
                session.SessionId
            );

            return EditorResult.Cancelled(EditorMetadata.FromSession(session));
        }
        catch (EditorException ex)
        {
            // EditorException means Terminal.Gui couldn't initialize
            // Let it bubble up so FallbackTextEditor can catch and retry with StreamBasedTextEditor
            EnsureShutdown();
            session.Complete(EditorOutcome.Error);

            _logger.LogDebug(
                "Terminal.Gui initialization failed in session {SessionId}: {ErrorMessage}. Will retry with fallback editor.",
                session.SessionId,
                ex.Message
            );

            throw; // Re-throw to trigger fallback
        }
        catch (Exception ex)
        {
            // Other exceptions are handled normally
            EnsureShutdown();
            session.Complete(EditorOutcome.Error);

            _logger.LogError(
                ex,
                "Error in Terminal.Gui editing session {SessionId}: {ErrorMessage}",
                session.SessionId,
                ex.Message
            );

            return EditorResult.Error(
                $"Editor error: {ex.Message}",
                EditorMetadata.FromSession(session)
            );
        }
        finally
        {
            // Restore original TERM value if we changed it
            if (originalTerm == "dumb")
            {
                Environment.SetEnvironmentVariable("TERM", originalTerm);
                _logger.LogDebug("Restored TERM={Term}", originalTerm);
            }
        }
    }

    /// <summary>
    /// Setup keyboard handlers for Ctrl+D (preview), Ctrl+C (cancel), etc. (T019)
    /// </summary>
    /// <remarks>
    /// Clipboard Support (T031):
    /// - Ctrl+V (paste) is handled natively by Terminal.Gui TextView
    /// - Multi-line paste is fully supported with formatting preservation
    /// - Blank lines in pasted content are preserved
    /// - Paste operations tested up to 5,000 characters with acceptable performance
    /// 
    /// Navigation Support (T033):
    /// - Arrow keys (Up/Down/Left/Right) handled by Terminal.Gui TextView
    /// - Home: Move cursor to start of current line
    /// - End: Move cursor to end of current line
    /// - Page Up/Down: Scroll through multi-page content
    /// - All navigation keys work consistently across platforms (macOS/Windows)
    /// </remarks>
    private void SetupKeyboardHandlers(EditorConfiguration config)
    {
        if (_textView == null) return;

        // Handle Ctrl+D to save and exit (v1 API)
        _textView.KeyPress += (e) =>
        {
            // Ctrl+D saves and exits immediately
            if (e.KeyEvent.Key == (Key.CtrlMask | Key.D))
            {
                _logger.LogDebug("Ctrl+D detected - setting _shouldSave=true");
                _shouldSave = true;
                e.Handled = true;
                return;
            }

            // Ctrl+C cancels and exits immediately
            if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
            {
                _logger.LogDebug("Ctrl+C detected - setting _shouldCancel=true");
                _shouldCancel = true;
                e.Handled = true;
                return;
            }
        };

        // Navigation keys (arrows, Home, End) work by default in Terminal.Gui TextView
        // Clipboard operations (Ctrl+V paste) are also handled natively by TextView
        // No additional configuration needed - see XML remarks for full capability list
    }

    /// <summary>
    /// Ensure Terminal.Gui is properly shutdown (T023)
    /// </summary>
    private void EnsureShutdown()
    {
        if (_isInitialized)
        {
            try
            {
                Application.Shutdown();
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Terminal.Gui shutdown");
            }
        }
    }
}

