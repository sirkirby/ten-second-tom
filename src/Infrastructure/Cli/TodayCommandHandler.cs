using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.Shared.TextEditing.Models;
using AudioHandlers = TenSecondTom.Features.Audio.Handlers;
using TodayHandlers = TenSecondTom.Features.Today.Handlers;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the execution of the 'today' command.
/// Prompts the user for daily reflections and creates a daily entry.
/// </summary>
public static class TodayCommandHandler
{
    /// <summary>
    /// Executes the today command by prompting the user and creating a daily entry.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="notes">Optional notes content from command line.</param>
    /// <param name="noEdit">Whether to skip the interactive editor.</param>
    /// <param name="useDefaultTemplate">Whether to use the default template automatically.</param>
    /// <param name="templateName">Optional template name to use.</param>
    /// <param name="providerOverride">Optional LLM provider override.</param>
    /// <param name="useVoice">Whether to use voice recording for input.</param>
    /// <param name="sttSelection">STT engine selection (auto, local, or openai). Only used with voice input.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1849:Call async methods when in an async method", Justification = "Spectre.Console Ask/Confirm are synchronous by design")]
    public static async Task ExecuteAsync(
        IServiceProvider serviceProvider,
        string? notes,
        bool noEdit,
        bool useDefaultTemplate,
        string? templateName,
        string? providerOverride,
        bool useVoice,
        string? sttSelection,
        bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // Resolve required services
        var handler = serviceProvider.GetRequiredService<TodayHandlers.CreateDailyEntryHandler>();
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var textEditor = serviceProvider.GetRequiredService<IInteractiveTextEditor>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<TodayHandlers.CreateVoiceNoteEntryHandler>>();

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first (before collecting user input)
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Today,
            jsonOutput,
            CancellationToken.None).ConfigureAwait(false);

        if (!authResult.IsSuccess)
        {
            return;
        }

        // Handle voice input mode
        if (useVoice)
        {
            await ExecuteVoiceInputAsync(
                serviceProvider,
                useDefaultTemplate,
                templateName,
                providerOverride,
                sttSelection,
                jsonOutput).ConfigureAwait(false);
            return;
        }

        // Validate: --no-edit requires notes argument
        if (noEdit && string.IsNullOrWhiteSpace(notes))
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure(
                    CommandNames.Today,
                    "--no-edit flag requires notes argument. Usage: tom today \"your notes here\" --no-edit",
                    DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --no-edit flag requires notes argument");
                AnsiConsole.MarkupLine("Usage: tom today \"your notes here\" --no-edit");
            }
            return;
        }

        // Gather content
        string content;

        if (noEdit && !string.IsNullOrWhiteSpace(notes))
        {
            // Use CLI argument directly
            content = notes;
        }
        else
        {
            // Interactive editor mode
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[bold cyan]📝 Notes for Today[/]");
                AnsiConsole.MarkupLine("[dim](Press Ctrl+D when done, Ctrl+C to cancel)[/]\n");
            }

            var editorConfig = EditorConfiguration.Default with { Title = "Notes for Today" };
            EditorResult editorResult = await textEditor.EditAsync(
                initialContent: notes, // Use notes as initial content if provided
                configuration: editorConfig,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            if (editorResult.IsCancelled)
            {
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine("[yellow]Entry creation cancelled.[/]");
                }
                return;
            }

            if (editorResult.IsError)
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Today,
                        $"Editor error: {editorResult.ErrorMessage}",
                        DateTimeOffset.UtcNow);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Editor error: {editorResult.ErrorMessage.EscapeMarkup()}[/]");
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(editorResult.Content))
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Today,
                        "No content entered",
                        DateTimeOffset.UtcNow);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No content entered. Exiting.[/]");
                }
                return;
            }

            content = editorResult.Content.Trim();

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] Content saved\n");
            }
        }

        // Create command
        var command = new CreateDailyEntryCommand
        {
            Content = content,
            TemplateName = templateName,
            UseDefaultTemplate = useDefaultTemplate,
            LlmProviderOverride = providerOverride
        };

        // Execute command with progress indicator (only show progress in non-JSON mode)
        DailyEntry? entry = null;
        Result<DailyEntry> commandResult;

        if (jsonOutput)
        {
            commandResult = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);
            if (commandResult.IsSuccess)
            {
                entry = commandResult.Value;
            }
        }
        else
        {
            commandResult = Result<DailyEntry>.Failure("Not executed");
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Processing your reflection...[/]", async ctx =>
                {
                    commandResult = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

                    if (commandResult.IsSuccess)
                    {
                        entry = commandResult.Value;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] {commandResult.Error.EscapeMarkup()}");
                    }
                }).ConfigureAwait(false);
        }

        // Display results
        if (jsonOutput)
        {
            // JSON output mode
            object? jsonData = null;
            if (commandResult.IsSuccess && entry != null)
            {
                jsonData = new
                {
                    entryId = entry.EntryId,
                    timestamp = entry.Timestamp,
                    provider = entry.Metadata.LlmProvider,
                    summary = new
                    {
                        keyEvents = entry.Summary.KeyEvents,
                        themes = entry.Summary.Themes,
                        todoItems = entry.Summary.TodoItems.Select(t => new { description = t.Description, isCompleted = t.IsCompleted }),
                        importantPeople = entry.Summary.ImportantPeople,
                        notableTasks = entry.Summary.NotableTasks
                    }
                };
            }

            string json = commandResult.IsSuccess
                ? JsonOutputFormatter.FormatSuccess(CommandNames.Today, jsonData, DateTimeOffset.UtcNow)
                : JsonOutputFormatter.FormatFailure(CommandNames.Today, commandResult.Error, DateTimeOffset.UtcNow);
            Console.WriteLine(json);
        }
        else if (entry != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✓ Daily entry created successfully![/]");
            AnsiConsole.WriteLine();

            var panel = new Panel(new Markup($"""
                [bold]Entry ID:[/] {entry.EntryId}
                [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
                [bold]Provider:[/] {entry.Metadata.LlmProvider}

                [bold cyan]Summary:[/]
                [dim]{entry.LlmResponse.Split('\n').Take(5).Aggregate((a, b) => a + "\n" + b)}...[/]
                """))
            {
                Header = new PanelHeader("📋 Daily Entry Summary"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Color.Cyan1)
            };

            AnsiConsole.Write(panel);

            // Show key events if any
            if (entry.Summary.KeyEvents.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Key Events:[/]");
                foreach (string keyEvent in entry.Summary.KeyEvents)
                {
                    AnsiConsole.MarkupLine($"  • {Markup.Escape(keyEvent)}");
                }
            }

            // Show todo items if any
            if (entry.Summary.TodoItems.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Todo Items:[/]");
                foreach (TodoItem todo in entry.Summary.TodoItems)
                {
                    string status = todo.IsCompleted ? "✓" : "○";
                    AnsiConsole.MarkupLine($"  {status} {Markup.Escape(todo.Description)}");
                }
            }
        }
    }

    /// <summary>
    /// Executes the voice input workflow for the today command.
    /// Records audio, transcribes it, and creates a voice note entry.
    /// </summary>
    private static async Task ExecuteVoiceInputAsync(
        IServiceProvider serviceProvider,
        bool useDefaultTemplate,
        string? templateName,
        string? providerOverride,
        string? sttSelection,
        bool jsonOutput)
    {
        // Resolve required services
        var recordHandler = serviceProvider.GetRequiredService<AudioHandlers.IRequestHandler<RecordAudioCommand, Result<AudioRecording>>>();
        var transcribeHandler = serviceProvider.GetRequiredService<AudioHandlers.IRequestHandler<TranscribeAudioCommand, Result<TranscriptionResult>>>();
        var voiceNoteHandler = serviceProvider.GetRequiredService<TodayHandlers.IRequestHandler<CreateVoiceNoteEntryCommand, Result<VoiceNoteEntry>>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<TodayHandlers.CreateVoiceNoteEntryHandler>>();

        // Get audio configuration
        var audioConfig = configuration.GetSection("TenSecondTom:Audio").Get<AudioConfiguration>()
            ?? new AudioConfiguration();

        // Get memory directory from configuration
        var memoryDirectory = configuration.GetValue<string>("TenSecondTom:MemoryDirectory");
        if (string.IsNullOrWhiteSpace(memoryDirectory))
        {
            var error = "Memory directory not configured. Run 'tom setup' to configure.";
            if (jsonOutput)
            {
                Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Today, error, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
            }
            return;
        }

        // Expand home directory
        memoryDirectory = memoryDirectory.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        // Create temp audio file path
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var audioFilePath = Path.Combine(Path.GetTempPath(), $"tom-voice-{timestamp}.wav");

        try
        {
            // Step 1: Record audio
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("[cyan]🎤 Recording... Press Enter to stop.[/]");
            }
            else
            {
                logger.LogInformation("Recording audio to {AudioFile}", audioFilePath);
            }

            var recordCommand = new RecordAudioCommand { OutputPath = audioFilePath };
            var recordResult = await recordHandler.Handle(recordCommand, CancellationToken.None).ConfigureAwait(false);

            if (!recordResult.IsSuccess)
            {
                var error = $"Recording failed: {recordResult.Error}";
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Today, error, DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
                }
                return;
            }

            var recording = recordResult.Value;

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Recording complete ({recording.Duration.TotalSeconds:F1}s)");
            }

            // Parse STT selection
            SttSelection selection = ParseSttSelection(sttSelection ?? audioConfig.PreferredStt);

            // Step 2: Transcribe audio
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine($"[cyan]✍️  Transcribing with {selection}...[/]");
            }
            else
            {
                logger.LogInformation("Transcribing audio with {Selection}", selection);
            }

            var transcribeCommand = new TranscribeAudioCommand
            {
                AudioFilePath = audioFilePath,
                Selection = selection
            };

            Result<TranscriptionResult> transcribeResult;
            if (jsonOutput)
            {
                transcribeResult = await transcribeHandler.Handle(transcribeCommand, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                transcribeResult = Result<TranscriptionResult>.Failure("Not executed");
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Transcribing...[/]", async ctx =>
                    {
                        transcribeResult = await transcribeHandler.Handle(transcribeCommand, CancellationToken.None).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }

            if (!transcribeResult.IsSuccess)
            {
                var error = $"Transcription failed: {transcribeResult.Error}";
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Today, error, DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
                }
                return;
            }

            var transcription = transcribeResult.Value;

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] Transcription complete ({transcription.WordCount} words, {transcription.SttEngine})");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Transcript:[/]");
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(transcription.TranscriptText.Trim())}[/]");
                AnsiConsole.WriteLine();
            }

            // Step 3: Create voice note entry
            var voiceNoteCommand = new CreateVoiceNoteEntryCommand
            {
                TranscriptText = transcription.TranscriptText,
                Recording = recording,
                Transcription = transcription,
                TemplateName = templateName,
                UseDefaultTemplate = useDefaultTemplate,
                LlmProviderOverride = providerOverride
            };

            var voiceNoteResult = await voiceNoteHandler.Handle(voiceNoteCommand, CancellationToken.None).ConfigureAwait(false);

            if (!voiceNoteResult.IsSuccess)
            {
                var error = $"Failed to create voice note entry: {voiceNoteResult.Error}";
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Today, error, DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
                }
                return;
            }

            var entry = voiceNoteResult.Value;

            // Step 4: Clean up audio file if configured
            if (!audioConfig.KeepFiles)
            {
                try
                {
                    File.Delete(audioFilePath);
                    logger.LogDebug("Deleted temporary audio file: {AudioFile}", audioFilePath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete temporary audio file: {AudioFile}", audioFilePath);
                }
            }
            else
            {
                // Move audio file to memory directory if keeping files
                var audioDestPath = Path.Combine(memoryDirectory, recording.Filename);
                try
                {
                    File.Move(audioFilePath, audioDestPath, overwrite: true);
                    logger.LogInformation("Moved audio file to {Destination}", audioDestPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to move audio file to memory directory");
                }
            }

            // Display results
            if (jsonOutput)
            {
                var jsonData = new
                {
                    entryId = entry.EntryId,
                    timestamp = entry.Timestamp,
                    audioFilename = entry.AudioFilename,
                    audioDuration = entry.AudioDuration.TotalSeconds,
                    wordCount = transcription.WordCount,
                    sttEngine = entry.SttEngine.ToString(),
                    sttModel = entry.SttModel
                };

                Console.WriteLine(JsonOutputFormatter.FormatSuccess(CommandNames.Today, jsonData, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine("[bold green]✓ Voice note entry created successfully![/]");
                AnsiConsole.MarkupLine($"[dim]Entry ID: {entry.EntryId}[/]");
                if (audioConfig.KeepFiles)
                {
                    AnsiConsole.MarkupLine($"[dim]Audio saved: {entry.AudioFilename}[/]");
                }
            }
        }
        finally
        {
            // Ensure cleanup of temp file if it still exists
            if (File.Exists(audioFilePath))
            {
                try
                {
                    File.Delete(audioFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Parses STT selection string to enum.
    /// </summary>
    private static SttSelection ParseSttSelection(string? selection)
    {
        return selection?.ToLowerInvariant() switch
        {
            "local" => SttSelection.Local,
            "openai" => SttSelection.OpenAI,
            _ => SttSelection.Auto
        };
    }
}
