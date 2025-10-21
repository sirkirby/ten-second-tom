using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Handlers;

/// <summary>
/// Handles the <see cref="CreateVoiceNoteEntryCommand"/> to create a voice note entry.
/// Creates a structured daily entry from voice transcription with audio metadata.
/// </summary>
public sealed class CreateVoiceNoteEntryHandler : IRequestHandler<CreateVoiceNoteEntryCommand, Result<VoiceNoteEntry>>
{
    private readonly ILogger<CreateVoiceNoteEntryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVoiceNoteEntryHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public CreateVoiceNoteEntryHandler(
        ILogger<CreateVoiceNoteEntryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the CreateVoiceNoteEntryCommand to create a voice note entry.
    /// </summary>
    /// <param name="request">The command containing voice note data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the created VoiceNoteEntry or an error.</returns>
    public Task<Result<VoiceNoteEntry>> Handle(
        CreateVoiceNoteEntryCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        // Validate transcript text
        if (string.IsNullOrWhiteSpace(request.TranscriptText))
        {
            return Task.FromResult(Result<VoiceNoteEntry>.Failure("Voice note transcript cannot be empty or whitespace"));
        }

        _logger.LogInformation(
            "Creating voice note entry: AudioFile={AudioFile}, TranscriptLength={Length}",
            request.Recording.Filename,
            request.TranscriptText.Length);

        var today = DateTimeOffset.UtcNow;
        var entryNumber = 1; // TODO: Get next entry number from storage

        // Create voice note entry with audio metadata
        var entry = new VoiceNoteEntry
        {
            // Voice-specific properties
            AudioFilename = request.Recording.Filename,
            AudioDuration = request.Recording.Duration,
            TranscriptText = request.TranscriptText,
            SttEngine = request.Transcription.SttEngine,
            SttModel = request.Transcription.SttModel,

            // MemoryEntry base properties
            EntryId = $"{CommandNames.Today}-{today:MM-dd-yyyy}-{entryNumber}",
            Command = CommandNames.Today,
            Timestamp = today,
            EntryNumber = entryNumber,
            UserInput = request.TranscriptText,
            LlmResponse = string.Empty, // Will be populated by LLM in full implementation
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "None", // No LLM processing in minimal implementation
                LlmModel = "None",
                TokensUsed = 0,
                ProcessingDuration = TimeSpan.Zero,
                CustomTags = new Dictionary<string, string>
                {
                    ["input-method"] = "voice",
                    ["audio-file"] = request.Recording.Filename,
                    ["audio-duration"] = request.Recording.Duration.TotalSeconds.ToString("F2"),
                    ["stt-engine"] = request.Transcription.SttEngine.ToString(),
                    ["stt-model"] = request.Transcription.SttModel ?? "unknown"
                }
            },

            // DailyEntry properties
            Summary = new DailySummary
            {
                KeyEvents = [],
                Themes = [],
                TodoItems = [],
                ImportantPeople = [],
                NotableTasks = []
            }
        };

        _logger.LogInformation(
            "Voice note entry created: EntryId={EntryId}, AudioDuration={Duration}s",
            entry.EntryId,
            entry.AudioDuration.TotalSeconds);

        return Task.FromResult(Result<VoiceNoteEntry>.Success(entry));
    }
}
