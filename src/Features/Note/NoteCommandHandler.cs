using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Infrastructure.Auth;
using MediatR;
using TenSecondTom.Shared.Constants;  // For CommandNames, SttProviders, DirectoryNames
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Features.Audio;  // Only for CQRS commands/queries
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Models;  // For shared types like AudioValidationResult, AudioRecording, TranscriptionResult

namespace TenSecondTom.Features.Note;

/// <summary>
/// Handles the execution of the 'note' command.
/// Captures quick notes without LLM processing.
/// </summary>
public static class NoteCommandHandler
{
    /// <summary>
    /// Executes the note command by capturing user content and creating a note entry.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="content">Optional note content from command line.</param>
    /// <param name="noEdit">Whether to skip the interactive editor.</param>
    /// <param name="useVoice">Whether to use voice recording for input.</param>
    /// <param name="sttSelection">STT engine selection (auto, local, openai). Only used with voice.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ExecuteAsync(
        IServiceProvider serviceProvider,
        string? content,
        bool noEdit,
        bool useVoice,
        string? sttSelection,
        bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // Resolve required services
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var textEditor = serviceProvider.GetRequiredService<IInteractiveTextEditor>();
        var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
        var logger = serviceProvider.GetRequiredService<ILogger<CreateNote.Handler>>();

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first (before collecting user input)
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Note,
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
                sttSelection,
                jsonOutput).ConfigureAwait(false);
            return;
        }

        // Validate: --no-edit requires content argument
        if (noEdit && string.IsNullOrWhiteSpace(content))
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure(
                    CommandNames.Note,
                    "--no-edit flag requires content argument. Usage: tom note \"your note here\" --no-edit",
                    DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --no-edit flag requires content argument");
                AnsiConsole.MarkupLine("Usage: tom note \"your note here\" --no-edit");
            }
            return;
        }

        // Gather content
        string noteContent;

        if (noEdit && !string.IsNullOrWhiteSpace(content))
        {
            // Use CLI argument directly
            noteContent = content;
        }
        else
        {
            // Interactive editor mode
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[bold cyan]📝 Quick Note[/]");
                AnsiConsole.MarkupLine("[dim](Press Ctrl+D when done, Ctrl+C to cancel)[/]\n");
            }

            var editorConfig = EditorConfiguration.Default with { Title = "Quick Note" };
            EditorResult editorResult = await textEditor.EditAsync(
                initialContent: content, // Use content as initial content if provided
                configuration: editorConfig,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            if (editorResult.IsCancelled)
            {
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine("[yellow]Note creation cancelled.[/]");
                }
                return;
            }

            if (editorResult.IsError)
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Note,
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
                        CommandNames.Note,
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

            noteContent = editorResult.Content.Trim();

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] Content saved\n");
            }
        }

        // Create command
        var command = new CreateNote.Command
        {
            Content = noteContent,
            IsVoiceNote = false,
            AudioFilePath = null
        };

        // Execute command
        Shared.Models.Note? note = null;
        Result<Shared.Models.Note> commandResult;

        if (jsonOutput)
        {
            commandResult = await mediator.Send(command, CancellationToken.None).ConfigureAwait(false);
            if (commandResult.IsSuccess)
            {
                note = commandResult.Value;
            }
        }
        else
        {
            commandResult = Result<Shared.Models.Note>.Failure("Not executed");
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Saving note...[/]", async ctx =>
                {
                    commandResult = await mediator.Send(command, CancellationToken.None).ConfigureAwait(false);

                    if (commandResult.IsSuccess)
                    {
                        note = commandResult.Value;
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
            if (commandResult.IsSuccess && note != null)
            {
                jsonData = new
                {
                    entryId = note.EntryId,
                    timestamp = note.Timestamp,
                    entryNumber = note.EntryNumber,
                    isVoiceNote = note.IsVoiceNote,
                    contentLength = note.Content.Length
                };
            }

            string json = commandResult.IsSuccess
                ? JsonOutputFormatter.FormatSuccess(CommandNames.Note, jsonData, DateTimeOffset.UtcNow)
                : JsonOutputFormatter.FormatFailure(CommandNames.Note, commandResult.Error, DateTimeOffset.UtcNow);
            Console.WriteLine(json);
        }
        else if (note != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✓ Note created successfully![/]");
            AnsiConsole.WriteLine();

            // Show preview of the note content
            string[] contentLines = note.Content.Split('\n');
            bool isTruncated = contentLines.Length > 5;
            string preview = isTruncated
                ? string.Join('\n', contentLines.Take(5))
                : note.Content;

            var panel = new Panel(new Markup($"""
                [bold]Entry ID:[/] {note.EntryId}
                [bold]Timestamp:[/] {note.Timestamp:yyyy-MM-dd HH:mm:ss}
                [bold]Entry Number:[/] {note.EntryNumber}

                [bold cyan]Content:[/]
                {Markup.Escape(preview)}
                """))
            {
                Header = new PanelHeader("📝 Quick Note"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Color.Cyan1)
            };

            AnsiConsole.Write(panel);

            // Show clickable file path
            var rootDir = storageOptions.Value.GetEffectiveStorageDirectory();
            string fullPath = Path.Combine(rootDir, note.FilePath);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Saved to:[/] [link]{fullPath.EscapeMarkup()}[/]");

            if (isTruncated)
            {
                AnsiConsole.MarkupLine("[dim]... (content truncated in preview)[/]");
            }
        }
    }

    /// <summary>
    /// Executes the voice input workflow for the note command.
    /// Records audio, transcribes it, and creates a note entry without LLM processing.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="sttSelection">STT engine selection (auto, local, openai).</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    private static async Task ExecuteVoiceInputAsync(
        IServiceProvider serviceProvider,
        string? sttSelection,
        bool jsonOutput)
    {
        // Resolve required services
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var logger = serviceProvider.GetRequiredService<ILogger<CreateNote.Handler>>();

        // Query audio configuration via CQRS (VSA compliance)
        IRequest<Result<AudioOptions>> audioConfigQuery = new GetAudioConfiguration.Query();
        var audioConfigQueryResult = await mediator.Send(audioConfigQuery, CancellationToken.None).ConfigureAwait(false);

        if (!audioConfigQueryResult.IsSuccess)
        {
            var error = "Failed to load audio configuration";
            if (jsonOutput)
            {
                Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Note, error, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
            }
            return;
        }

        var audioOptions = audioConfigQueryResult.Value;

        // Build transcription configuration based on STT selection
        var transcribeConfig = BuildTranscriptionConfig(sttSelection, audioOptions);

        // Validate audio configuration
        IRequest<Result<AudioValidationResult>> validateQuery = new ValidateAudioConfiguration.Query();
        var validationResult = await mediator.Send(validateQuery, CancellationToken.None).ConfigureAwait(false);

        if (!validationResult.IsSuccess || validationResult.Value is not { IsConfigured: true })
        {
            var validation = validationResult.Value;
            var errorMessage = validation is not null
                ? $"Audio configuration incomplete. Missing:\n{string.Join("\n", validation.MissingItems.Select(item => $"  - {item}"))}"
                : "Failed to validate audio configuration";

            if (jsonOutput)
            {
                Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Note, errorMessage, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Audio configuration incomplete[/]");
                AnsiConsole.MarkupLine($"The [cyan]{CommandNames.Note}[/] command requires audio settings.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Missing configuration:[/]");
                if (validation is not null)
                {
                    foreach (var item in validation.MissingItems)
                    {
                        AnsiConsole.MarkupLine($"  • {item}");
                    }
                }
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Configure with: [cyan]tom config audio[/][/]");
            }
            return;
        }

        // Get the effective storage directory
        var storageBaseDir = storageOptions.GetEffectiveStorageDirectory();

        if (string.IsNullOrWhiteSpace(storageBaseDir))
        {
            var error = "Storage directory not configured. Run 'tom setup' to configure.";
            if (jsonOutput)
            {
                Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Note, error, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
            }
            return;
        }

        // Create temp audio file path
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var audioFilePath = Path.Combine(Path.GetTempPath(), $"tom-note-voice-{timestamp}.wav");

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

            IRequest<Result<AudioRecording>> recordCommand = new RecordAudio.Command
            {
                OutputPath = audioFilePath,
                MaxDurationSeconds = audioOptions.Timeouts.TodaySeconds  // Use TodaySeconds for voice notes
            };
            var recordResult = await mediator.Send(recordCommand, CancellationToken.None).ConfigureAwait(false);

            if (!recordResult.IsSuccess)
            {
                var error = $"Recording failed: {recordResult.Error}";
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Note, error, DateTimeOffset.UtcNow));
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

            // Step 2: Transcribe audio
            if (!jsonOutput)
            {
                var providerDisplay = transcribeConfig.SttProvider == SttProviders.OpenAI ? "OpenAI" : "local";
                var fallbackInfo = transcribeConfig.SttFallbackEnabled ? $" (fallback to {transcribeConfig.SttFallbackProvider})" : "";
                AnsiConsole.MarkupLine($"[cyan]✍️  Transcribing with {providerDisplay}{fallbackInfo}...[/]");
            }
            else
            {
                logger.LogInformation("Transcribing audio with {Provider}, FallbackEnabled={FallbackEnabled}",
                    transcribeConfig.SttProvider, transcribeConfig.SttFallbackEnabled);
            }

            IRequest<Result<TranscriptionResult>> transcribeCommand = new TranscribeAudio.Command
            {
                AudioFilePath = audioFilePath,
                AudioConfig = transcribeConfig
            };

            Result<TranscriptionResult> transcribeResult;
            if (jsonOutput)
            {
                transcribeResult = await mediator.Send(transcribeCommand, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                transcribeResult = Result<TranscriptionResult>.Failure("Not executed");
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("cyan"))
                    .StartAsync("[cyan]Transcribing...[/]", async ctx =>
                    {
                        transcribeResult = await mediator.Send(transcribeCommand, CancellationToken.None).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }

            if (!transcribeResult.IsSuccess)
            {
                var error = $"Transcription failed: {transcribeResult.Error}";
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Note, error, DateTimeOffset.UtcNow));
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

            // Step 3: Save note with transcript
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("[blue]→[/] Saving voice note...");
            }

            IRequest<Result<Shared.Models.Note>> createNoteCommand = new CreateNote.Command
            {
                Content = transcription.TranscriptText,
                IsVoiceNote = true,
                AudioFilePath = audioFilePath
            };

            var noteResult = await mediator.Send(createNoteCommand, CancellationToken.None).ConfigureAwait(false);
            if (!noteResult.IsSuccess)
            {
                var error = $"Failed to save note: {noteResult.Error}";
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Note, error, DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {error.EscapeMarkup()}");
                }
                return;
            }

            var note = noteResult.Value;

            // Step 4: Handle audio file persistence + transcript storage
            string? persistedAudioPath = null;
            if (!audioOptions.KeepFiles)
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
                var baseName = VoiceCapturePersistence.BuildVoiceEntryBaseName(note.EntryId, recording.Filename);
                var persistResult = await VoiceCapturePersistence.PersistAsync(
                    mediator,
                    audioFilePath,
                    baseName,
                    transcribeConfig,
                    AudioLibraryScope.Note,
                    transcription,
                    logger,
                    CancellationToken.None).ConfigureAwait(false);

                if (persistResult.IsSuccess && persistResult.Value is not null)
                {
                    persistedAudioPath = persistResult.Value.AudioFilePath;
                    note = note with { AudioFilePath = persistedAudioPath };
                }
            }

            // Display results
            if (jsonOutput)
            {
                var jsonData = new
                {
                    entryId = note.EntryId,
                    timestamp = note.Timestamp,
                    entryNumber = note.EntryNumber,
                    isVoiceNote = note.IsVoiceNote,
                    audioDuration = recording.Duration.TotalSeconds,
                    wordCount = transcription.WordCount,
                    sttEngine = transcription.SttEngine.ToString(),
                    contentLength = note.Content.Length
                };

                Console.WriteLine(JsonOutputFormatter.FormatSuccess(CommandNames.Note, jsonData, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold green]✓ Voice note created successfully![/]");
                AnsiConsole.WriteLine();

                // Show preview of the transcript
                string[] contentLines = note.Content.Split('\n');
                bool isTruncated = contentLines.Length > 5;
                string preview = isTruncated
                    ? string.Join('\n', contentLines.Take(5))
                    : note.Content;

                var panel = new Panel(new Markup($"""
                    [bold]Entry ID:[/] {note.EntryId}
                    [bold]Timestamp:[/] {note.Timestamp:yyyy-MM-dd HH:mm:ss}
                    [bold]Entry Number:[/] {note.EntryNumber}
                    [bold]Audio:[/] {recording.Duration.TotalSeconds:F1}s ({transcription.SttEngine})
                    [bold]Words:[/] {transcription.WordCount}

                    [bold cyan]Transcript:[/]
                    {Markup.Escape(preview)}
                    """))
                {
                    Header = new PanelHeader("🎤 Voice Note"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(foreground: Color.Cyan1)
                };

                AnsiConsole.Write(panel);

                // Show clickable file path
                string fullPath = Path.Combine(storageBaseDir, note.FilePath);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Saved to:[/] [link]{fullPath.EscapeMarkup()}[/]");

                if (isTruncated)
                {
                    AnsiConsole.MarkupLine("[dim]... (content truncated in preview)[/]");
                }

                if (audioOptions.KeepFiles && persistedAudioPath is not null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Audio saved: {Path.GetFileName(persistedAudioPath)}[/]");
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
    /// Builds transcription configuration based on STT selection.
    /// </summary>
    /// <param name="sttSelection">STT engine selection (auto, local, openai).</param>
    /// <param name="audioOptions">Base audio options from configuration.</param>
    /// <returns>Configured AudioOptions for transcription.</returns>
    private static AudioOptions BuildTranscriptionConfig(string? sttSelection, AudioOptions audioOptions)
    {
        var normalizedSelection = sttSelection?.ToLowerInvariant();

        return normalizedSelection switch
        {
            // "auto": Try local provider first, fallback to configured fallback if enabled
            "auto" or null => new AudioOptions
            {
                SttProvider = audioOptions.SttProvider,
                SttApiKey = audioOptions.SttApiKey,
                SttFallbackEnabled = true,
                SttFallbackProvider = audioOptions.SttFallbackProvider,
                SttFallbackApiKey = audioOptions.SttFallbackApiKey,
                SttBinaryPath = audioOptions.SttBinaryPath,
                SttModel = audioOptions.SttModel,
                SttFallbackBinaryPath = audioOptions.SttFallbackBinaryPath,
                SttFallbackModel = audioOptions.SttFallbackModel,
                KeepFiles = audioOptions.KeepFiles,
                Recorder = audioOptions.Recorder,
                Preprocessing = audioOptions.Preprocessing,
                Timeouts = audioOptions.Timeouts
            },

            // "local": Use only the configured local provider (no fallback)
            "local" => new AudioOptions
            {
                SttProvider = audioOptions.SttProvider,
                SttApiKey = audioOptions.SttApiKey,
                SttFallbackEnabled = false,
                SttBinaryPath = audioOptions.SttBinaryPath,
                SttModel = audioOptions.SttModel,
                KeepFiles = audioOptions.KeepFiles,
                Recorder = audioOptions.Recorder,
                Preprocessing = audioOptions.Preprocessing,
                Timeouts = audioOptions.Timeouts
            },

            // "openai": Force OpenAI provider, no fallback
            "openai" => new AudioOptions
            {
                SttProvider = SttProviders.OpenAI,
                SttApiKey = audioOptions.SttApiKey,
                SttFallbackEnabled = false,
                SttBinaryPath = audioOptions.SttBinaryPath,
                SttModel = audioOptions.SttModel,
                KeepFiles = audioOptions.KeepFiles,
                Recorder = audioOptions.Recorder,
                Preprocessing = audioOptions.Preprocessing,
                Timeouts = audioOptions.Timeouts
            },

            _ => throw new ArgumentException(
                $"Invalid STT selection: '{sttSelection}'. Valid values: 'auto', 'local', 'openai'.",
                nameof(sttSelection))
        };
    }
}
