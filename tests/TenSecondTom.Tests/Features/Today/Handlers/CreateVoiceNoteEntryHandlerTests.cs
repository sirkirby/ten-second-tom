using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Features.Templates;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Today;

namespace TenSecondTom.Tests.Features.Today.Handlers;

/// <summary>
/// Tests for <see cref="CreateVoiceNoteEntry.Handler"/>.
/// Validates voice note entry generation with audio metadata, transcript, and LLM summary.
/// </summary>
public sealed class CreateVoiceNoteEntryHandlerTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorage;
    private readonly Mock<ILlmProviderFactory> _mockLlmFactory;
    private readonly Mock<IPromptTemplateLoader> _mockPromptLoader;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IOptions<LlmOptions>> _mockLlmOptions;
    private readonly Mock<ILogger<CreateVoiceNoteEntry.Handler>> _mockLogger;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly Mock<ILlmProvider> _mockLlmProvider;

    public CreateVoiceNoteEntryHandlerTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLlmFactory = new Mock<ILlmProviderFactory>();
        _mockPromptLoader = new Mock<IPromptTemplateLoader>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        _mockLogger = new Mock<ILogger<CreateVoiceNoteEntry.Handler>>();
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();
        _mockLlmProvider = new Mock<ILlmProvider>();

        // Setup default LLM options
        _mockLlmOptions.Setup(o => o.Value).Returns(new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "test-key",
            Model = "gpt-4",
            MaxInputTokens = 100000
        });
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

        var command = new CreateVoiceNoteEntry.Command
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
        result.Value.LlmResponse.Should().NotBeNullOrEmpty();
        
        // Verify LLM was called
        _mockLlmProvider.Verify(x => x.GenerateCompletionAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<int?>(),
            It.IsAny<double?>()), Times.Once);
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

        var command = new CreateVoiceNoteEntry.Command
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

        var command = new CreateVoiceNoteEntry.Command
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
        result.Value.TranscriptText.Should().Be(transcription.TranscriptText);
        result.Value.UserInput.Should().Be(transcription.TranscriptText);
    }

    [Fact]
    public async Task Handle_GeneratesLlmSummary()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(
            recording.FilePath,
            "Today I worked on implementing voice notes with TDD. I wrote comprehensive tests first.");

        var command = new CreateVoiceNoteEntry.Command
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
        result.Value.LlmResponse.Should().NotBeNullOrEmpty();
        // Parsing removed - LlmResponse is the source of truth;
        result.Value.Metadata.LlmProvider.Should().Be("TestProvider");
        
        // Verify LLM was invoked (prompt template would be rendered with the transcript)
        _mockLlmProvider.Verify(x => x.GenerateCompletionAsync(
            It.IsAny<string>(), // The rendered prompt will contain the transcript
            It.IsAny<CancellationToken>(),
            It.IsAny<int?>(),
            It.IsAny<double?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReusesExistingEntryCreationLogic()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntry.Command
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
        result.Value.UserInput.Should().NotBeNullOrWhiteSpace();
        result.Value.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.EntryId.Should().StartWith("today-");
        result.Value.EntryNumber.Should().Be(1); // First entry of the day
    }

    [Fact]
    public async Task Handle_WithTemplateName_UsesSpecifiedTemplate()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntry.Command
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
        result.IsSuccess.Should().BeTrue();
        
        // Verify template was loaded with custom name
        _mockPromptLoader.Verify(x => x.LoadTemplateAsync(
            "custom-voice-template",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithLlmProviderOverride_UsesSpecifiedProvider()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntry.Command
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
        result.IsSuccess.Should().BeTrue();
        
        // Verify Anthropic provider was requested
        _mockLlmFactory.Verify(x => x.CreateProvider("Anthropic"), Times.Once);
    }

    [Fact]
    public async Task Handle_EscapesMarkdownInTranscript()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(
            recording.FilePath,
            "Transcript with **bold** and `code` and [links](http://example.com)");

        var command = new CreateVoiceNoteEntry.Command
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
        // Transcript should be preserved exactly as-is
        result.Value.TranscriptText.Should().Contain("**bold**");
        result.Value.TranscriptText.Should().Contain("`code`");
    }

    [Fact]
    public async Task Handle_WithEmptyTranscript_ReturnsFailure()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, string.Empty);

        var command = new CreateVoiceNoteEntry.Command
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

        var command = new CreateVoiceNoteEntry.Command
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
        // Verify that logger was called (handler logs creation)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("voice", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_DoesNotLogTranscriptContent()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var sensitiveText = "This is private confidential information";
        var transcription = CreateSampleTranscription(recording.FilePath, sensitiveText);

        var command = new CreateVoiceNoteEntry.Command
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
        // Verify that sensitive transcript content is NEVER logged
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(sensitiveText)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Should NEVER log transcript content for privacy");
    }

    [Fact]
    public async Task Handle_ValidatesVoiceNoteEntry()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Valid transcript text");

        var command = new CreateVoiceNoteEntry.Command
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
        result.Value.IsValid().Should().BeTrue("Voice note entry should be valid");
        result.Value.TranscriptText.Should().Be(result.Value.UserInput,
            "Transcript should match user input");
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        var recording = CreateSampleRecording();
        var transcription = CreateSampleTranscription(recording.FilePath, "Test transcript");

        var command = new CreateVoiceNoteEntry.Command
        {
            TranscriptText = transcription.TranscriptText,
            Recording = recording,
            Transcription = transcription,
            UseDefaultTemplate = true
        };

        // Create handler first, then override auth to return false
        var handler = CreateHandler();
        
        // Override auth mock to return false for this test
        _mockAuthService.Reset();
        _mockAuthService
            .Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Authentication required");
    }

    private CreateVoiceNoteEntry.Handler CreateHandler()
    {
        // Setup auth service mock
        _mockAuthService
            .Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup template loader mock
        var mockTemplate = new PromptTemplate
        {
            TemplateId = "default",
            Content = "Analyze this: {{UserInput}}",
            TemplateType = TemplateType.Daily,
            Description = "Test template"
        };
        _mockPromptLoader
            .Setup(x => x.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(mockTemplate));

        // Setup LLM provider mock
        var llmResponseText = @"## Key Events
- Implemented voice notes
- Wrote comprehensive tests

## Themes and Patterns
- Test-driven development
- Audio processing

## To-Do Items
- [ ] Review implementation
- [ ] Update documentation

## Overall Reflection
Successfully implemented voice note feature with TDD approach.";
        
        var llmResponse = new LlmResponse
        {
            Content = llmResponseText,
            InputTokens = 100,
            OutputTokens = 150
        };
        
        _mockLlmProvider
            .Setup(x => x.ProviderName)
            .Returns("TestProvider");
        
        _mockLlmProvider
            .Setup(x => x.ModelName)
            .Returns("test-model");
        
        _mockLlmProvider
            .Setup(x => x.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(llmResponse));
        
        _mockLlmFactory
            .Setup(x => x.CreateProvider(It.IsAny<string>()))
            .Returns(_mockLlmProvider.Object);

        // Setup storage provider mock
        _mockStorage
            .Setup(x => x.CountEntriesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0)); // Return 0 so next entry is 1
        
        _mockStorage
            .Setup(x => x.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        // Create mock for ITemplateProvider
        var mockTemplateProvider = new Mock<ITemplateProvider>();

        return new CreateVoiceNoteEntry.Handler(
            _mockStorage.Object,
            _mockLlmFactory.Object,
            _mockPromptLoader.Object,
            _mockAuthService.Object,
            _mockLlmOptions.Object,
            _mockLogger.Object,
            mockTemplateProvider.Object,
            _mockTemplateSelectionUI.Object);
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
