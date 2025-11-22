using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// CLI workflow for the <c>transcribe</c> command.
/// Handles authentication, audio selection, STT configuration, and output formatting.
/// </summary>
public static class TranscribeCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    /// <summary>
    /// Executes the transcribe command.
    /// </summary>
    public static async Task<int> ExecuteAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        string? noteName,
        string? recordingName,
        string? filePath,
        string? customName,
        string? sttSelection,
        bool listOnly,
        bool forceOverwrite)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var audioLibraryService = serviceProvider.GetRequiredService<IAudioLibraryService>();
        var audioValidator = serviceProvider.GetRequiredService<IAudioConfigurationValidator>();
        var fileSystem = serviceProvider.GetRequiredService<IFileSystem>();
        var console = serviceProvider.GetRequiredService<IAnsiConsole>();

        if (listOnly)
        {
            return await DisplayLibraryAsync(audioLibraryService, console, jsonOutput).ConfigureAwait(false);
        }

        if (CountSpecifiedSources(noteName, recordingName, filePath) > 1)
        {
            CommandOutputFormatter.WriteError("Specify only one audio source: --note, --recording, or --file.", jsonOutput);
            return 1;
        }

        var audioOptionsResult = await mediator.Send(new GetAudioConfiguration.Query(), CancellationToken.None)
            .ConfigureAwait(false);

        if (!audioOptionsResult.IsSuccess || audioOptionsResult.Value is null)
        {
            CommandOutputFormatter.WriteError(audioOptionsResult.Error ?? "Audio configuration unavailable.", jsonOutput);
            return 1;
        }

        if (!SttSelectionMapper.TryParse(sttSelection, out var sttChoice, out var sttError))
        {
            CommandOutputFormatter.WriteValidationError("STT selection", sttError!, jsonOutput);
            return 1;
        }

        if (!jsonOutput && authService is MockAuthenticationService)
        {
            CommandOutputFormatter.WriteWarning("Development Mode: Authentication bypassed", jsonOutput);
            console.WriteLine();
        }

        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Transcribe,
            jsonOutput,
            CancellationToken.None).ConfigureAwait(false);

        if (!authResult.IsSuccess)
        {
            return 1;
        }

        var configResult = AudioConfigurationHelper.EnsureAudioConfigured(
            audioValidator,
            audioOptionsResult.Value,
            CommandNames.Transcribe,
            jsonOutput);

        if (!configResult.IsSuccess)
        {
            return 1;
        }

        var audioSelectionResult = await ResolveAudioSelectionAsync(
            audioLibraryService,
            fileSystem,
            console,
            noteName,
            recordingName,
            filePath,
            customName,
            jsonOutput).ConfigureAwait(false);

        if (!audioSelectionResult.IsSuccess || audioSelectionResult.Value is null)
        {
            CommandOutputFormatter.WriteError(audioSelectionResult.Error ?? "Unable to resolve audio file.", jsonOutput);
            return 1;
        }

        var selection = audioSelectionResult.Value;
        var normalizedNameResult = NormalizeBaseName(selection.BaseName);
        if (!normalizedNameResult.IsSuccess || normalizedNameResult.Value is null)
        {
            CommandOutputFormatter.WriteError(normalizedNameResult.Error ?? "Recording name invalid.", jsonOutput);
            return 1;
        }

        selection = selection with { BaseName = normalizedNameResult.Value };

        var effectiveForceOverwrite = forceOverwrite;
        if (selection.TranscriptExists && !forceOverwrite)
        {
            if (jsonOutput)
            {
                CommandOutputFormatter.WriteError(
                    $"Transcript already exists for '{selection.BaseName}'. Re-run with --force to overwrite.",
                    jsonOutput);
                return 1;
            }

            var overwriteChoice = console.Prompt(
                new SelectionPrompt<string>()
                    .Title($"A transcript already exists for '{selection.BaseName}'. Choose an action:")
                    .HighlightStyle("yellow")
                    .AddChoices("Overwrite transcript", "Cancel"));

            var overwriteConfirmed = overwriteChoice.StartsWith("Overwrite", StringComparison.OrdinalIgnoreCase);

            if (!overwriteConfirmed)
            {
                CommandOutputFormatter.WriteWarning(
                    "Transcription cancelled. Existing transcript left untouched.",
                    jsonOutput);
                return 0;
            }

            effectiveForceOverwrite = true;
        }

        var handler = serviceProvider.GetRequiredService<TranscribeLibraryAudio.Handler>();
        var command = new TranscribeLibraryAudio.Command
        {
            AudioFilePath = selection.AudioFilePath,
            RecordingBaseName = selection.BaseName,
            AudioConfig = SttSelectionMapper.BuildAudioOptions(sttChoice, audioOptionsResult.Value),
            Source = selection.Scope,
            ForceOverwrite = effectiveForceOverwrite
        };

        var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            CommandOutputFormatter.WriteError(result.Error ?? "Transcription failed.", jsonOutput);
            return 1;
        }

        if (jsonOutput)
        {
            var payload = new
            {
                success = true,
                recording_base_name = result.Value.RecordingBaseName,
                audio_file_path = result.Value.AudioFilePath,
                transcript_file_path = result.Value.TranscriptFilePath,
                stt_engine = result.Value.Transcription.SttEngine.ToString(),
                stt_model = result.Value.Transcription.SttModel,
                word_count = result.Value.Transcription.WordCount,
                transcribed_at = result.Value.Transcription.TranscribedAt,
                processing_duration_seconds = result.Value.Transcription.ProcessingDuration.TotalSeconds
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        RenderSuccess(console, result.Value);
        return 0;
    }

    private static async Task<Result<AudioLibraryItem>> ResolveAudioSelectionAsync(
        IAudioLibraryService audioLibraryService,
        IFileSystem fileSystem,
        IAnsiConsole console,
        string? noteName,
        string? recordingName,
        string? filePath,
        string? customName,
        bool jsonOutput)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return ResolveExternalFile(fileSystem, filePath, customName);
        }

        if (!string.IsNullOrWhiteSpace(noteName))
        {
            var noteResult = audioLibraryService.GetAudioFile(AudioLibraryScope.Note, noteName);
            if (noteResult.IsSuccess && !string.IsNullOrWhiteSpace(customName))
            {
                var updated = noteResult.Value! with { BaseName = customName };
                return Result<AudioLibraryItem>.Success(updated);
            }
            return noteResult;
        }

        if (!string.IsNullOrWhiteSpace(recordingName))
        {
            var recordingResult = audioLibraryService.GetAudioFile(AudioLibraryScope.Recording, recordingName);
            if (recordingResult.IsSuccess && !string.IsNullOrWhiteSpace(customName))
            {
                var updated = recordingResult.Value! with { BaseName = customName };
                return Result<AudioLibraryItem>.Success(updated);
            }
            return recordingResult;
        }

        if (jsonOutput)
        {
            return Result<AudioLibraryItem>.Failure("Specify --note, --recording, or --file when using --output-json.");
        }

        return await PromptForSelectionAsync(audioLibraryService, console, customName).ConfigureAwait(false);
    }

    private static async Task<Result<AudioLibraryItem>> PromptForSelectionAsync(
        IAudioLibraryService audioLibraryService,
        IAnsiConsole console,
        string? customName)
    {
        var recordingResult = await audioLibraryService.ListAudioFilesAsync(AudioLibraryScope.Recording).ConfigureAwait(false);
        var noteResult = await audioLibraryService.ListAudioFilesAsync(AudioLibraryScope.Note).ConfigureAwait(false);

        var choices = new List<AudioLibraryItem>();
        if (recordingResult.IsSuccess && recordingResult.Value is not null)
        {
            choices.AddRange(recordingResult.Value);
        }

        if (noteResult.IsSuccess && noteResult.Value is not null)
        {
            choices.AddRange(noteResult.Value);
        }

        if (choices.Count == 0)
        {
            return Result<AudioLibraryItem>.Failure("No audio files found. Use 'tom record' or '/note --voice' first.");
        }

        var selection = console.Prompt(
            new SelectionPrompt<AudioLibraryItem>()
                .Title("Select an audio file to transcribe:")
                .PageSize(10)
                .AddChoices(choices)
                .UseConverter(item => item.ToDisplayLabel()));

        if (!string.IsNullOrWhiteSpace(customName))
        {
            selection = selection with { BaseName = customName };
        }

        return Result<AudioLibraryItem>.Success(selection);
    }

    private static Result<AudioLibraryItem> ResolveExternalFile(
        IFileSystem fileSystem,
        string filePath,
        string? customName)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<AudioLibraryItem>.Failure("File path is required.");
        }

        var fullPath = fileSystem.Path.GetFullPath(filePath);
        if (!fileSystem.File.Exists(fullPath))
        {
            return Result<AudioLibraryItem>.Failure($"Audio file not found: {fullPath}");
        }

        var extension = fileSystem.Path.GetExtension(fullPath);
        if (!string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
        {
            return Result<AudioLibraryItem>.Failure("Only .wav files are supported for transcription.");
        }

        var fileInfo = fileSystem.FileInfo.New(fullPath);
        var baseName = string.IsNullOrWhiteSpace(customName)
            ? fileSystem.Path.GetFileNameWithoutExtension(fileInfo.Name)
            : customName;

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = $"import-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        }

        var item = new AudioLibraryItem
        {
            BaseName = baseName,
            AudioFilePath = fileInfo.FullName,
            Scope = AudioLibraryScope.External,
            RecordedAt = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToLocalTime(),
            FileSizeBytes = fileInfo.Length,
            TranscriptExists = false
        };

        return Result<AudioLibraryItem>.Success(item);
    }

    private static Result<string> NormalizeBaseName(string? baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = $"recording-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        }

        var sanitized = new string(baseName
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return Result<string>.Failure("Recording name resolves to an empty value. Provide --name with letters/numbers.");
        }

        if (sanitized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return Result<string>.Failure("Recording name includes invalid filesystem characters.");
        }

        return Result<string>.Success(sanitized);
    }

    private static async Task<int> DisplayLibraryAsync(
        IAudioLibraryService audioLibraryService,
        IAnsiConsole console,
        bool jsonOutput)
    {
        var recordings = await audioLibraryService.ListAudioFilesAsync(AudioLibraryScope.Recording).ConfigureAwait(false);
        var notes = await audioLibraryService.ListAudioFilesAsync(AudioLibraryScope.Note).ConfigureAwait(false);

        if (jsonOutput)
        {
            var payload = new
            {
                success = true,
                recordings = recordings.IsSuccess ? recordings.Value!.Select(ToDto) : Array.Empty<object>(),
                notes = notes.IsSuccess ? notes.Value!.Select(ToDto) : Array.Empty<object>()
            };

            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (recordings.IsSuccess && recordings.Value is { Count: > 0 })
        {
            console.MarkupLine("[bold]Recording Audio:[/]");
            RenderTable(console, recordings.Value);
        }
        else
        {
            console.MarkupLine("[dim]No recording audio found.[/]");
        }

        console.WriteLine();

        if (notes.IsSuccess && notes.Value is { Count: > 0 })
        {
            console.MarkupLine("[bold]Note Audio:[/]");
            RenderTable(console, notes.Value);
        }
        else
        {
            console.MarkupLine("[dim]No note audio found.[/]");
        }

        return 0;
    }

    private static object ToDto(AudioLibraryItem item) => new
    {
        base_name = item.BaseName,
        audio_file_path = item.AudioFilePath,
        recorded_at = item.RecordedAt,
        file_size_bytes = item.FileSizeBytes,
        duration_seconds = item.DurationSeconds,
        transcript_exists = item.TranscriptExists,
        scope = item.Scope.ToString().ToLowerInvariant()
    };

    private static void RenderTable(IAnsiConsole console, IReadOnlyList<AudioLibraryItem> items)
    {
        var table = new Table()
            .AddColumn("Name")
            .AddColumn("Recorded")
            .AddColumn("Size")
            .AddColumn("Duration")
            .AddColumn("Transcript");
        foreach (var item in items)
        {
            table.AddRow(
                item.BaseName.EscapeMarkup(),
                item.RecordedAt.ToString("MMM dd, yyyy h:mm tt").EscapeMarkup(),
                $"{item.FileSizeBytes / 1024.0:F1} KB",
                item.DurationSeconds.HasValue
                    ? TimeSpan.FromSeconds(item.DurationSeconds.Value).ToString(@"m\:ss")
                    : "—",
                item.TranscriptExists ? "Yes" : "No");
        }
        console.Write(table);
        console.WriteLine();
    }

    private static void RenderSuccess(IAnsiConsole console, TranscribeLibraryAudio.TranscribedLibraryRecording recording)
    {
        console.MarkupLine($"[green]✓[/] Transcribed {recording.RecordingBaseName.EscapeMarkup()} ({recording.Transcription.WordCount} words, {recording.Transcription.SttEngine})");
        console.MarkupLine($"[dim]Audio: {recording.AudioFilePath.EscapeMarkup()}[/]");
        console.MarkupLine($"[dim]Transcript: {recording.TranscriptFilePath.EscapeMarkup()}[/]");
        console.WriteLine();
        console.MarkupLine("[bold]Transcript Preview:[/]");

        var (formatted, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(recording.Transcription.TranscriptText);
        console.MarkupLine($"[dim]{formatted.EscapeMarkup()}[/]");
        if (wasTruncated)
        {
            console.MarkupLine($"[dim](Preview truncated by {truncatedChars:N0} characters)[/]");
        }
    }

    private static int CountSpecifiedSources(params string?[] values)
        => values.Count(v => !string.IsNullOrWhiteSpace(v));
}
