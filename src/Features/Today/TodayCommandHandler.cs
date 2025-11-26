using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Features.Audio;  // Only for CQRS commands/queries
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Infrastructure.Auth;
using MediatR;
using TenSecondTom.Shared.Models;  // For shared types like AudioValidationResult, AudioRecording, TranscriptionResult
using TenSecondTom.Shared.Constants;  // For CommandNames, SttProviders, DirectoryNames
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Requests;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Features.Note;  // For CreateNote command
using TenSecondTom.Features.Audio.Services;

namespace TenSecondTom.Features.Today;

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
        var handler = serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var textEditor = serviceProvider.GetRequiredService<IInteractiveTextEditor>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var logger = serviceProvider.GetRequiredService<ILogger<CreateVoiceNoteEntry.Handler>>();

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
            // Query audio configuration via CQRS (use interface type for VSA compliance)
            IRequest<Result<AudioOptions>> audioConfigQuery = new GetAudioConfiguration.Query();
            var audioConfigQueryResult = await mediator.Send(audioConfigQuery, CancellationToken.None).ConfigureAwait(false);

            if (!audioConfigQueryResult.IsSuccess)
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Today,
                        "Failed to load audio configuration",
                        DateTimeOffset.UtcNow);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: Failed to load audio configuration[/]");
                }
                return;
            }

            var audioOptions = audioConfigQueryResult.Value;

            // Validate audio configuration before proceeding with voice input
            // Validate audio configuration via CQRS (VSA compliance - no direct service dependency)
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
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Today, errorMessage, DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Audio configuration incomplete[/]");
                    AnsiConsole.MarkupLine($"The [cyan]{CommandNames.Today}[/] command requires audio settings.");
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
                return; // Audio configuration incomplete
            }

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

        // Save raw note first (orchestration pattern)
        if (!jsonOutput)
        {
            AnsiConsole.MarkupLine("[blue]→[/] Saving note...");
        }

        IRequest<Result<Shared.Models.Note>> createNoteCommand = new CreateNote.Command
        {
            Content = content,
            IsVoiceNote = false,
            AudioFilePath = null
        };

        var noteResult = await mediator.Send(createNoteCommand, CancellationToken.None).ConfigureAwait(false);
        if (!noteResult.IsSuccess)
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure(
                    CommandNames.Today,
                    $"Failed to save note: {noteResult.Error}",
                    DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Failed to save note: {noteResult.Error.EscapeMarkup()}");
            }
            return;
        }

        if (!jsonOutput)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Note saved");
        }

        // Create command
        var command = new CreateDailyEntry.Command
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
                    model = entry.Metadata.LlmModel,
                    tokensUsed = entry.Metadata.TokensUsed,
                    response = entry.LlmResponse
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

            // Show truncated preview of the LLM response
            string[] responseLines = entry.LlmResponse.Split('\n');
            bool isTruncated = responseLines.Length > 10;
            string preview = isTruncated
                ? string.Join('\n', responseLines.Take(10))
                : entry.LlmResponse;

            var panel = new Panel(new Markup($"""
                [bold]Entry ID:[/] {entry.EntryId}
                [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
                [bold]Provider:[/] {entry.Metadata.LlmProvider}
                [bold]Tokens:[/] {entry.Metadata.TokensUsed}

                [bold cyan]Response:[/]
                {Markup.Escape(preview)}
                """))
            {
                Header = new PanelHeader("📋 Daily Entry"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Color.Cyan1)
            };

            AnsiConsole.Write(panel);

            // Show clickable file path
            if (isTruncated)
            {
                var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
                var rootDir = storageOptions.Value.RootDirectory ?? Path.Combine(".", DirectoryNames.ApplicationRoot);
                string fullPath = Path.Combine(rootDir, entry.FilePath);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Full entry:[/] [link]{fullPath.EscapeMarkup()}[/]");
            }

            // Send success notification (non-blocking, fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var notificationCommand = new SendNotificationRequest(
                        Title: "Daily Entry Created",
                        Message: $"Your reflection has been processed and saved.\n\nProvider: {entry.Metadata.LlmProvider}, Tokens: {entry.Metadata.TokensUsed}",
                        Priority: NotificationPriority.Normal,
                        TimeoutSeconds: null,
                        Actions: null);

                    var result = await mediator.Send(notificationCommand, CancellationToken.None);

                    if (!result.IsSuccess)
                    {
                        logger.LogWarning("Failed to send daily entry notification: {Error}", result.Error);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unexpected error sending daily entry notification (non-critical)");
                }
            }, CancellationToken.None);
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
        var voiceNoteHandler = serviceProvider.GetRequiredService<IRequestHandler<CreateVoiceNoteEntry.Command, Result<VoiceNoteEntry>>>();
        var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var logger = serviceProvider.GetRequiredService<ILogger<CreateVoiceNoteEntry.Handler>>();

        // Query audio configuration via CQRS (use interface type for VSA compliance)
        IRequest<Result<AudioOptions>> audioConfigQuery = new GetAudioConfiguration.Query();
        var audioConfigQueryResult = await mediator.Send(audioConfigQuery, CancellationToken.None).ConfigureAwait(false);

        if (!audioConfigQueryResult.IsSuccess)
        {
            var error = "Failed to load audio configuration";
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

        var audioOptions = audioConfigQueryResult.Value;

        // Query transcribe configuration via CQRS (use interface type for VSA compliance)
        IRequest<Result<TranscribeOptions>> transcribeConfigQuery = new GetTranscribeConfiguration.Query();
        var transcribeConfigQueryResult = await mediator.Send(transcribeConfigQuery, CancellationToken.None).ConfigureAwait(false);

        if (!transcribeConfigQueryResult.IsSuccess)
        {
            var error = "Failed to load transcription configuration";
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

        var transcribeOptions = transcribeConfigQueryResult.Value;

        // Get the effective storage directory using extension method
        var storageBaseDir = storageOptions.EffectiveStorageDirectory;

        if (string.IsNullOrWhiteSpace(storageBaseDir))
        {
            var error = "Storage directory not configured. Run 'tom setup' to configure.";
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
        var audioFilePath = Path.Combine(Path.GetTempPath(), $"tom-voice-{timestamp}.mp3");

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

            // Use interface type to avoid cross-feature type dependency (VSA compliance)
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

            // Note: RecordAudio.Command already handles preprocessing internally
            // The recording returned is already preprocessed according to audio configuration

            // Build transcription configuration based on CLI selection
            var transcribeConfig = BuildTranscriptionConfig(sttSelection, transcribeOptions);

            // Step 2: Transcribe audio
            if (!jsonOutput)
            {
                var providerDisplay = transcribeConfig.SttProvider == SttProviders.OpenAI ? "OpenAI" :
                                    transcribeConfig.SttProvider == SttProviders.BuiltInLocal ? "Built-in Local" : "Whisper.cpp";
                AnsiConsole.MarkupLine($"[cyan]✍️  Transcribing with {providerDisplay}...[/]");
            }
            else
            {
                logger.LogInformation("Transcribing audio with {Provider}",
                    transcribeConfig.SttProvider);
            }

            // Use interface type to avoid closure capturing concrete Command type (VSA compliance)
            IRequest<Result<TranscriptionResult>> transcribeCommand = new TranscribeAudio.Command
            {
                AudioFilePath = audioFilePath,
                TranscribeConfig = transcribeConfig
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

            // Save raw note with transcript (orchestration pattern)
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("[blue]→[/] Saving note with transcript...");
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
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Today, error, DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {error.EscapeMarkup()}");
                }
                return;
            }

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Note saved");
            }

            // Step 3: Create voice note entry with AI processing
            var voiceNoteCommand = new CreateVoiceNoteEntry.Command
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

            string? persistedAudioPath = null;
            if (!transcribeOptions.KeepFiles)
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
                var baseName = VoiceCapturePersistence.BuildVoiceEntryBaseName(entry.EntryId, recording.Filename);
                var persistResult = await VoiceCapturePersistence.PersistAsync(
                    mediator,
                    audioFilePath,
                    baseName,
                    transcribeConfig,
                    AudioLibraryScope.Today,
                    transcription,
                    logger,
                    CancellationToken.None).ConfigureAwait(false);

                if (persistResult.IsSuccess && persistResult.Value is not null)
                {
                    persistedAudioPath = persistResult.Value.AudioFilePath;
                    entry.AudioFilename = Path.GetFileName(persistedAudioPath);
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
                    model = entry.Metadata.LlmModel,
                    tokensUsed = entry.Metadata.TokensUsed,
                    response = entry.LlmResponse
                };

                Console.WriteLine(JsonOutputFormatter.FormatSuccess(CommandNames.Today, jsonData, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold green]✓ Voice note entry created successfully![/]");
                AnsiConsole.WriteLine();

                // Show truncated preview of the LLM response
                string[] responseLines = entry.LlmResponse.Split('\n');
                bool isTruncated = responseLines.Length > 10;
                string preview = isTruncated
                    ? string.Join('\n', responseLines.Take(10))
                    : entry.LlmResponse;

                var panel = new Panel(new Markup($"""
                    [bold]Entry ID:[/] {entry.EntryId}
                    [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
                    [bold]Provider:[/] {entry.Metadata.LlmProvider}
                    [bold]Audio:[/] {entry.AudioDuration.TotalSeconds:F1}s ({entry.SttEngine})
                    [bold]Tokens:[/] {entry.Metadata.TokensUsed}

                    [bold cyan]Response:[/]
                    {Markup.Escape(preview)}
                    """))
                {
                    Header = new PanelHeader("🎤 Voice Note Entry"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(foreground: Color.Cyan1)
                };

                AnsiConsole.Write(panel);

                // Show clickable file path
                if (isTruncated)
                {
                    var rootDir = storageOptions.RootDirectory ?? Path.Combine(".", DirectoryNames.ApplicationRoot);
                    string fullPath = Path.Combine(rootDir, entry.FilePath);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Full entry:[/] [link]{fullPath.EscapeMarkup()}[/]");
                }

                if (transcribeOptions.KeepFiles)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Audio saved: {entry.AudioFilename}[/]");
                }

                // Send success notification (non-blocking, fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var notificationCommand = new SendNotificationRequest(
                            Title: "Voice Note Entry Created",
                            Message: $"Voice note processed and saved.\n\nTranscription: {transcription.WordCount} words ({entry.SttEngine})\nTokens: {entry.Metadata.TokensUsed}",
                            Priority: NotificationPriority.Normal,
                            TimeoutSeconds: null,
                            Actions: null);

                        var result = await mediator.Send(notificationCommand, CancellationToken.None);

                        if (!result.IsSuccess)
                        {
                            logger.LogWarning("Failed to send voice note entry notification: {Error}", result.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Unexpected error sending voice note entry notification (non-critical)");
                    }
                }, CancellationToken.None);
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
    /// Builds transcription configuration based on CLI STT selection and TranscribeOptions.
    /// Maps user-friendly CLI options (auto/local/openai) to the proper configuration.
    /// </summary>
    private static TenSecondTom.Shared.Options.TranscribeOptions BuildTranscriptionConfig(
        string? sttSelection,
        TenSecondTom.Shared.Options.TranscribeOptions transcribeOptions)
    {
        var normalizedSelection = sttSelection?.ToLowerInvariant();
        var providers = transcribeOptions.Providers ?? new Dictionary<string, Dictionary<string, string>>();

        return normalizedSelection switch
        {
            // "auto" or null: Use configured provider
            "auto" or null => new TenSecondTom.Shared.Options.TranscribeOptions
            {
                SttProvider = transcribeOptions.SttProvider,
                Providers = providers,
                KeepFiles = transcribeOptions.KeepFiles
            },

            // "local": Use local provider
            "local" => new TenSecondTom.Shared.Options.TranscribeOptions
            {
                SttProvider = SttProviders.WhisperCpp,
                Providers = providers,
                KeepFiles = transcribeOptions.KeepFiles
            },

            // "openai": Force OpenAI provider
            "openai" => new TenSecondTom.Shared.Options.TranscribeOptions
            {
                SttProvider = SttProviders.OpenAI,
                Providers = providers,
                KeepFiles = transcribeOptions.KeepFiles
            },

            _ => throw new ArgumentException(
                $"Invalid STT selection: '{sttSelection}'. Valid values: 'auto', 'local', 'openai'.",
                nameof(sttSelection))
        };
    }
}
