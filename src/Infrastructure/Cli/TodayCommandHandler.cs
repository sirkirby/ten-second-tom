using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Contracts;
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
        var recordHandler = serviceProvider.GetRequiredService<IRequestHandler<RecordAudioCommand, Result<AudioRecording>>>();
        var transcribeHandler = serviceProvider.GetRequiredService<IRequestHandler<TranscribeAudioCommand, Result<TranscriptionResult>>>();
        var audioPreprocessor = serviceProvider.GetRequiredService<IAudioPreprocessor>();
        var voiceNoteHandler = serviceProvider.GetRequiredService<IRequestHandler<CreateVoiceNoteEntryCommand, Result<VoiceNoteEntry>>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<TodayHandlers.CreateVoiceNoteEntryHandler>>();

        // Get audio configuration
        var audioConfig = configuration.GetSection("TenSecondTom:Audio").Get<AudioConfiguration>()
            ?? new AudioConfiguration();

        // Get memory directory from configuration with proper precedence
        // PRIMARY: Storage:MemoryDirectory (from .env, user secrets, environment vars)
        // FALLBACK: TenSecondTom:MemoryDirectory (from appsettings.json)
        var memoryDirectory = configuration.GetMemoryDirectory(expandHomeDirectory: true);
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

            var recordCommand = new RecordAudioCommand 
            { 
                OutputPath = audioFilePath,
                MaxDurationSeconds = audioConfig.Timeouts.TodaySeconds  // Use TodaySeconds for voice notes
            };
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

            // Step 1.5: Preprocess audio (remove silence if configured)
            var preprocessResult = await audioPreprocessor.PreprocessAsync(
                recording.FilePath,
                replaceOriginal: true,
                CancellationToken.None).ConfigureAwait(false);

            if (!preprocessResult.IsSuccess)
            {
                logger.LogWarning("Audio preprocessing failed: {Error}. Continuing with original audio.", preprocessResult.Error);
                // Continue with original audio - preprocessing failure is not fatal
            }
            else
            {
                var preprocStats = preprocessResult.Value;
                logger.LogInformation(
                    "Audio preprocessing completed: OriginalDuration={OriginalDuration}s, ProcessedDuration={ProcessedDuration}s, " +
                    "Reduction={Reduction:F1}%",
                    preprocStats.OriginalDuration.TotalSeconds,
                    preprocStats.ProcessedDuration.TotalSeconds,
                    preprocStats.DurationReductionPercent);

                if (!jsonOutput && preprocStats.DurationReductionPercent > 0)
                {
                    AnsiConsole.MarkupLine($"[dim]  Removed {preprocStats.DurationReductionPercent:F1}% silence ({preprocStats.ProcessedDuration.TotalSeconds:F1}s remaining)[/]");
                }

                // Update recording metadata with preprocessed values
                recording = new AudioRecording
                {
                    Filename = recording.Filename,
                    FilePath = recording.FilePath,
                    Duration = preprocStats.ProcessedDuration,
                    SampleRate = recording.SampleRate,
                    Channels = recording.Channels,
                    Format = recording.Format,
                    Encoding = recording.Encoding,
                    RecordedAt = recording.RecordedAt,
                    FileSizeBytes = preprocStats.ProcessedSizeBytes
                };
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

            // Step 3: Create voice note entry with AI processing
            var voiceNoteCommand = new CreateVoiceNoteEntryCommand
            {
                TranscriptText = transcription.TranscriptText,
                Recording = recording,
                Transcription = transcription,
                TemplateName = templateName,
                UseDefaultTemplate = useDefaultTemplate,
                LlmProviderOverride = providerOverride
            };

            VoiceNoteEntry? entry = null;
            Result<VoiceNoteEntry> voiceNoteResult;

            if (jsonOutput)
            {
                voiceNoteResult = await voiceNoteHandler.Handle(voiceNoteCommand, CancellationToken.None).ConfigureAwait(false);
                if (voiceNoteResult.IsSuccess)
                {
                    entry = voiceNoteResult.Value;
                }
            }
            else
            {
                // Show AI processing spinner
                voiceNoteResult = Result<VoiceNoteEntry>.Failure("Not executed");
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Generating AI summary...[/]", async ctx =>
                    {
                        voiceNoteResult = await voiceNoteHandler.Handle(voiceNoteCommand, CancellationToken.None).ConfigureAwait(false);

                        if (voiceNoteResult.IsSuccess)
                        {
                            entry = voiceNoteResult.Value;
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]Error:[/] {voiceNoteResult.Error.EscapeMarkup()}");
                        }
                    }).ConfigureAwait(false);
            }

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

            if (entry == null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Failed to create entry");
                return;
            }

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
                // Move audio file to today directory with consistent naming pattern
                var todayDir = Path.Combine(memoryDirectory, "today");
                Directory.CreateDirectory(todayDir); // Ensure directory exists
                
                // Extract date and number from entry-id (e.g., "today-10-21-2025-1" -> "10-21-2025_1.wav")
                var entryIdParts = entry.EntryId.Split('-');
                if (entryIdParts.Length >= 5)
                {
                    var month = entryIdParts[1];
                    var day = entryIdParts[2];
                    var year = entryIdParts[3];
                    var number = entryIdParts[4];
                    var newFilename = $"{month}-{day}-{year}_{number}.wav";
                    var audioDestPath = Path.Combine(todayDir, newFilename);
                    
                    try
                    {
                        File.Move(audioFilePath, audioDestPath, overwrite: true);
                        entry.AudioFilename = newFilename; // Update entry to reflect actual filename
                        logger.LogInformation("Moved audio file to {Destination}", audioDestPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to move audio file to today directory");
                    }
                }
                else
                {
                    logger.LogWarning("Invalid entry ID format: {EntryId}, falling back to original filename", entry.EntryId);
                    var audioDestPath = Path.Combine(todayDir, recording.Filename);
                    try
                    {
                        File.Move(audioFilePath, audioDestPath, overwrite: true);
                        logger.LogInformation("Moved audio file to {Destination}", audioDestPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to move audio file to today directory");
                    }
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
                    sttModel = entry.SttModel,
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

                Console.WriteLine(JsonOutputFormatter.FormatSuccess(CommandNames.Today, jsonData, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold green]✓ Voice note entry created successfully![/]");
                AnsiConsole.WriteLine();

                var panel = new Panel(new Markup($"""
                    [bold]Entry ID:[/] {entry.EntryId}
                    [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
                    [bold]Provider:[/] {entry.Metadata.LlmProvider}
                    [bold]Audio:[/] {entry.AudioDuration.TotalSeconds:F1}s ({entry.SttEngine})

                    [bold cyan]Summary:[/]
                    [dim]{entry.LlmResponse.Split('\n').Take(5).Aggregate((a, b) => a + "\n" + b)}...[/]
                    """))
                {
                    Header = new PanelHeader("🎤 Voice Note Summary"),
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

                if (audioConfig.KeepFiles)
                {
                    AnsiConsole.WriteLine();
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
