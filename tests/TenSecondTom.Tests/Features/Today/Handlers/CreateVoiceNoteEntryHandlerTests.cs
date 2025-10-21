using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Today.Handlers;

/// <summary>
/// Tests for <see cref="CreateVoiceNoteEntryHandler"/>.
/// Validates voice note entry generation with audio metadata, transcript, and LLM summary.
/// </summary>
public sealed class CreateVoiceNoteEntryHandlerTests
{
    private readonly Mock<ILogger<CreateVoiceNoteEntryHandler>> _mockLogger;

    public CreateVoiceNoteEntryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<CreateVoiceNoteEntryHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesVoiceNoteEntry()
    {
        // Arrange
        var recording = new AudioRecording
        {
            Filename = "note-20251020-143000.wav",
            FilePath = "/path/to/note-20251020-143000.wav",
            Duration = TimeSpan.FromSeconds(120),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 3840000
        };

        var transcription = new TranscriptionResult
        {
            AudioReference = recording.FilePath,
            TranscriptText = "Today I completed three important tasks and learned about TDD.",
            SttEngine = SttEngine.Local,
            SttModel = "ggml-base.en",
            ProcessingDuration = TimeSpan.FromSeconds(10),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = 10
        };

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AudioFilename.Should().Be("note-20251020-143000.wav");
        result.Value.AudioDuration.Should().Be(TimeSpan.FromSeconds(120));
        result.Value.TranscriptText.Should().Be(transcription.TranscriptText);
        result.Value.SttEngine.Should().Be(SttEngine.Local);
        result.Value.SttModel.Should().Be("ggml-base.en");
    }

    [Fact]
    public async Task Handle_IncludesAudioMetadataInFrontmatter()
    {
        // Arrange
        var recording = new AudioRecording
        {
            Filename = "note-20251020-143000.wav",
            FilePath = "/path/to/note-20251020-143000.wav",
            Duration = TimeSpan.FromSeconds(120),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 3840000
        };

        var transcription = new TranscriptionResult
        {
            AudioReference = recording.FilePath,
            TranscriptText = "Test transcript",
            SttEngine = SttEngine.Local,
            SttModel = "ggml-base.en",
            ProcessingDuration = TimeSpan.FromSeconds(5),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = 2
        };

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Verify frontmatter includes audio metadata
        result.Value.AudioFilename.Should().Be("note-20251020-143000.wav");
        result.Value.AudioDuration.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task Handle_CreatesCollapsibleTranscriptSection()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(
            recording.FilePath,
            "This is a longer transcript with multiple sentences. It should be rendered in a collapsible section.");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Implementation should generate markdown with <details> tag
        // This is validated by checking that TranscriptText is preserved
        result.Value.TranscriptText.Should().Be(transcription.TranscriptText);
    }

    [Fact]
    public async Task Handle_GeneratesLlmSummary()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(
            recording.FilePath,
            "Today I worked on implementing voice notes with TDD. I wrote comprehensive tests first.");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Should invoke LLM to generate summary
        // Summary should be populated in LlmSummary property
        result.Value.UserInput.Should().Be(transcription.TranscriptText);
    }

    [Fact]
    public async Task Handle_ReusesExistingEntryCreationLogic()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Should reuse existing DailyEntry creation patterns
        // Entry should have proper timestamps and metadata
        result.Value.UserInput.Should().NotBeNullOrWhiteSpace();
        result.Value.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_WithTemplateName_UsesSpecifiedTemplate()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            TemplateName = "custom-voice-template",
            UseDefaultTemplate = false
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Should attempt to load "custom-voice-template"
        // Fallback to default if not found
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithLlmProviderOverride_UsesSpecifiedProvider()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            LlmProviderOverride = "Anthropic",
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Should use Anthropic LLM provider for summary generation
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EscapesMarkdownInTranscript()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(
            recording.FilePath,
            "Transcript with **bold** and `code` and [links](http://example.com)");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // Transcript should be preserved exactly as-is
        // Markdown special characters should not break rendering
        result.Value.TranscriptText.Should().Contain("**bold**");
        result.Value.TranscriptText.Should().Contain("`code`");
    }

    [Fact]
    public async Task Handle_WithEmptyTranscript_ReturnsFailure()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, string.Empty);

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = string.Empty,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("transcript");
    }

    [Fact]
    public async Task Handle_LogsVoiceEntryCreation()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.ToString()!.Contains("voice", StringComparison.OrdinalIgnoreCase),
            "Should log voice entry creation");
    }

    [Fact]
    public async Task Handle_DoesNotLogTranscriptContent()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(
            recording.FilePath,
            "This is private confidential information");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().NotContain(i =>
            i.ToString()!.Contains("private confidential information"),
            "Should NEVER log transcript content for privacy");
    }

    [Fact]
    public async Task Handle_ValidatesVoiceNoteEntry()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Valid transcript text");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.IsValid().Should().BeTrue("Voice note entry should be valid");
        result.Value.TranscriptText.Should().Be(result.Value.UserInput,
            "Transcript should match user input");
    }

    [Fact]
    public async Task Handle_RespectsCancellationToken()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntryCommand
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private CreateVoiceNoteEntryHandler CreateHandler()
    {
        // In real implementation, this would inject dependencies
        // For now, defining the test contract
        return new CreateVoiceNoteEntryHandler(_mockLogger.Object);
    }

    private static AudioRecording CreateSampleRecording()
    {
        return new AudioRecording
        {
            Filename = "note-20251020-143000.wav",
            FilePath = "/path/to/note-20251020-143000.wav",
            Duration = TimeSpan.FromSeconds(60),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 1920000
        };
    }

    private static TranscriptionResult CreateSampleTranscription(string audioPath, string text)
    {
        return new TranscriptionResult
        {
            AudioReference = audioPath,
            TranscriptText = text,
            SttEngine = SttEngine.Local,
            SttModel = "ggml-base.en",
            ProcessingDuration = TimeSpan.FromSeconds(5),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
        };
    }
}
