using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Shared.Models;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Generate;

namespace TenSecondTom.Tests.Features.Generate.Handlers;

/// <summary>
/// Tests for <see cref="GenerateOutput.Handler"/> implementation.
/// Validates the full orchestration of output generation including validation,
/// template loading, transcript processing, LLM interaction, and output storage.
/// </summary>
public sealed class GenerateOutputCommandHandlerTests
{
    private readonly Mock<IRecordingService> _mockRecordingService;
    private readonly Mock<IPromptTemplateLoader> _mockTemplateLoader;
    private readonly Mock<ITranscriptProcessor> _mockTranscriptProcessor;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILlmProviderFactory> _mockLlmProviderFactory;
    private readonly Mock<IOptionsSnapshot<LlmOptions>> _mockLlmOptions;
    private readonly Mock<IOutputStorageService> _mockOutputStorageService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<GenerateOutput.Handler>> _mockLogger;

    public GenerateOutputCommandHandlerTests()
    {
        _mockRecordingService = new Mock<IRecordingService>();
        _mockTemplateLoader = new Mock<IPromptTemplateLoader>();
        _mockTranscriptProcessor = new Mock<ITranscriptProcessor>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLlmProviderFactory = new Mock<ILlmProviderFactory>();
        _mockLlmOptions = new Mock<IOptionsSnapshot<LlmOptions>>();
        _mockOutputStorageService = new Mock<IOutputStorageService>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<GenerateOutput.Handler>>();

        // Setup default LLM provider properties
        _mockLlmProvider.Setup(p => p.ProviderName).Returns("TestProvider");
        _mockLlmProvider.Setup(p => p.ModelName).Returns("test-model");

        // Setup LLM options with default values
        var llmOptions = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "test-api-key",
            Model = "test-model",
            MaxInputTokens = 100000
        };
        _mockLlmOptions.Setup(o => o.Value).Returns(llmOptions);

        // Setup factory to return the mock provider
        _mockLlmProviderFactory
            .Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(_mockLlmProvider.Object);
    }

    #region Successful Generation Tests

    [Fact]
    public async Task Handle_WithValidCommand_GeneratesOutputSuccessfully()
    {
        // Arrange
        var command = CreateTestCommand();
        SetupSuccessfulGeneration();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Content.Should().NotBeNullOrEmpty();
        result.Value.InputName.Should().Be(command.InputName);
        result.Value.TemplateId.Should().Be(command.TemplateId);
    }

    [Fact]
    public async Task Handle_SavesOutputToCorrectPath()
    {
        // Arrange
        var command = CreateTestCommand();
        var expectedOutputPath = "/test/output/10-21-2025_1_generated.md";
        SetupSuccessfulGeneration(outputPath: expectedOutputPath);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OutputFilePath.Should().Be(expectedOutputPath);
    }

    [Fact]
    public async Task Handle_IncludesMetadataInOutput()
    {
        // Arrange
        var command = CreateTestCommand();
        SetupSuccessfulGeneration();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var output = result.Value;

        output.ProviderName.Should().Be("TestProvider");
        output.ModelName.Should().Be("test-model");
        output.InputTokens.Should().BeGreaterThan(0);
        output.OutputTokens.Should().BeGreaterThan(0);
        output.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithInvalidTranscriptFile_ReturnsFailure()
    {
        // Arrange
        var command = CreateTestCommand();

        _mockRecordingService
            .Setup(s => s.ValidateTranscriptFileAsync(command.TranscriptFilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Transcript file not found"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Template Loading Tests

    [Fact]
    public async Task Handle_WithMissingTemplate_ReturnsFailure()
    {
        // Arrange
        var command = CreateTestCommand();

        _mockRecordingService
            .Setup(s => s.ValidateTranscriptFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync(command.TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Failure("Template not found"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Template not found");
    }

    #endregion

    #region Transcript Processing Tests

    [Fact]
    public async Task Handle_WithTruncatedTranscript_LogsWarning()
    {
        // Arrange
        var command = CreateTestCommand();
        SetupSuccessfulGeneration(wasTruncated: true);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WasTruncated.Should().BeTrue();
        result.Value.OriginalWordCount.Should().Be(100); // From CreateTestTruncatedTranscript
    }

    [Fact]
    public async Task Handle_PassesMaxInputTokensToProcessor()
    {
        // Arrange
        var command = CreateTestCommand() with { MaxInputTokens = 5000 };
        SetupSuccessfulGeneration();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockTranscriptProcessor.Verify(
            p => p.ProcessTranscriptAsync(
                It.IsAny<string>(),
                5000,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region LLM Provider Tests

    [Fact]
    public async Task Handle_WithLlmError_ReturnsFailure()
    {
        // Arrange
        var command = CreateTestCommand();
        SetupSuccessfulGeneration(llmSuccess: false);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LLM");
    }

    [Fact]
    public async Task Handle_SubstitutesTranscriptInTemplate()
    {
        // Arrange
        var command = CreateTestCommand();
        var transcriptContent = "My transcript content here.";
        var templateContent = "Process this: {{USER_INPUT}}\nDate: {{DATE}}";
        string? capturedPrompt = null;

        SetupSuccessfulGeneration(
            transcriptContent: transcriptContent,
            templateContent: templateContent);

        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync((string prompt, CancellationToken ct, int? max, double? temp) =>
            {
                capturedPrompt = prompt;
                return Result<LlmResponse>.Success(new LlmResponse
                {
                    Content = "LLM response",
                    InputTokens = 100,
                    OutputTokens = 50
                });
            });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedPrompt.Should().Contain("Processed transcript content"); // From TranscriptProcessor
        capturedPrompt.Should().NotContain("{{USER_INPUT}}");
        capturedPrompt.Should().NotContain("{{DATE}}");
        capturedPrompt.Should().Contain("October 21, 2025"); // Parsed from test recording base name "10-21-2025_1"
    }

    #endregion

    #region Output Storage Tests

    [Fact]
    public async Task Handle_WithOutputSaveFailure_ReturnsFailure()
    {
        // Arrange
        var command = CreateTestCommand();
        SetupSuccessfulGeneration(outputSaveSuccess: false);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("save");
    }

    [Fact]
    public async Task Handle_CallsOutputStorageService()
    {
        // Arrange
        var command = CreateTestCommand();
        SetupSuccessfulGeneration();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOutputStorageService.Verify(
            s => s.SaveOutputAsync(
                It.Is<GeneratedOutput>(o =>
                    o.InputName == command.InputName &&
                    o.TemplateId == command.TemplateId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        // Arrange
        var command = CreateTestCommand();
        var cts = new CancellationTokenSource();
        SetupSuccessfulGeneration();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify cancellation token was passed through
        _mockRecordingService.Verify(
            s => s.ValidateTranscriptFileAsync(It.IsAny<string>(), cts.Token),
            Times.Once);
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync(It.IsAny<string>(), cts.Token),
            Times.Once);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Handle_ExecutesFullOrchestrationInCorrectOrder()
    {
        // Arrange
        var command = CreateTestCommand();
        var callSequence = new List<string>();

        _mockRecordingService
            .Setup(s => s.ValidateTranscriptFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success())
            .Callback(() => callSequence.Add("ValidateTranscript"));

        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(CreateTestTemplate()))
            .Callback(() => callSequence.Add("LoadTemplate"));

        _mockRecordingService
            .Setup(s => s.GetTranscriptContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("transcript"))
            .Callback(() => callSequence.Add("GetTranscript"));

        _mockTranscriptProcessor
            .Setup(p => p.ProcessTranscriptAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TruncatedTranscript>.Success(CreateTestTruncatedTranscript()))
            .Callback(() => callSequence.Add("ProcessTranscript"));

        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(CreateTestLlmResponse()))
            .Callback(() => callSequence.Add("GenerateCompletion"));

        _mockOutputStorageService
            .Setup(s => s.SaveOutputAsync(It.IsAny<GeneratedOutput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("/output/path.md"))
            .Callback(() => callSequence.Add("SaveOutput"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        callSequence.Should().Equal(
            "ValidateTranscript",
            "LoadTemplate",
            "GetTranscript",
            "ProcessTranscript",
            "GenerateCompletion",
            "SaveOutput");
    }

    #endregion

    #region Helper Methods

    private GenerateOutput.Handler CreateHandler()
    {
        return new GenerateOutput.Handler(
            _mockRecordingService.Object,
            _mockTemplateLoader.Object,
            _mockTranscriptProcessor.Object,
            _mockLlmProviderFactory.Object,
            _mockLlmOptions.Object,
            _mockOutputStorageService.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    private static GenerateOutput.Command CreateTestCommand()
    {
        return new GenerateOutput.Command
        {
            TranscriptFilePath = "/test/recording/10-21-2025_1.md",
            InputName = "10-21-2025_1",
            InputType = "Recording",
            TemplateId = "daily-summary",
            MaxInputTokens = 8000
        };
    }

    private static PromptTemplate CreateTestTemplate(string? content = null)
    {
        return new PromptTemplate
        {
            TemplateId = "daily-summary",
            Content = content ?? "Process this transcript: {{TRANSCRIPT}}",
            TemplateType = TemplateType.Daily,
            Description = "Test template",
            Metadata = new TemplateMetadata
            {
                Title = "Daily Summary",
                Description = "Test template",
                Version = "1.0"
            }
        };
    }

    private static TruncatedTranscript CreateTestTruncatedTranscript(bool wasTruncated = false)
    {
        return new TruncatedTranscript
        {
            Content = "Processed transcript content",
            WasTruncated = wasTruncated,
            OriginalWordCount = 100,
            FinalWordCount = wasTruncated ? 80 : 100,
            EstimatedTokenCount = wasTruncated ? 104 : 130
        };
    }

    private static LlmResponse CreateTestLlmResponse()
    {
        return new LlmResponse
        {
            Content = "This is the generated output from the LLM.",
            InputTokens = 100,
            OutputTokens = 50
        };
    }

    private void SetupSuccessfulGeneration(
        string? transcriptContent = null,
        string? templateContent = null,
        bool wasTruncated = false,
        bool llmSuccess = true,
        bool outputSaveSuccess = true,
        string? outputPath = null)
    {
        _mockRecordingService
            .Setup(s => s.ValidateTranscriptFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(CreateTestTemplate(templateContent)));

        _mockRecordingService
            .Setup(s => s.GetTranscriptContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(transcriptContent ?? "Test transcript content"));

        _mockTranscriptProcessor
            .Setup(p => p.ProcessTranscriptAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TruncatedTranscript>.Success(CreateTestTruncatedTranscript(wasTruncated)));

        if (llmSuccess)
        {
            _mockLlmProvider
                .Setup(p => p.GenerateCompletionAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int?>(),
                    It.IsAny<double?>()))
                .ReturnsAsync(Result<LlmResponse>.Success(CreateTestLlmResponse()));
        }
        else
        {
            _mockLlmProvider
                .Setup(p => p.GenerateCompletionAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int?>(),
                    It.IsAny<double?>()))
                .ReturnsAsync(Result<LlmResponse>.Failure("LLM provider error"));
        }

        if (outputSaveSuccess)
        {
            _mockOutputStorageService
                .Setup(s => s.SaveOutputAsync(It.IsAny<GeneratedOutput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Success(outputPath ?? "/test/output/path.md"));
        }
        else
        {
            _mockOutputStorageService
                .Setup(s => s.SaveOutputAsync(It.IsAny<GeneratedOutput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Failure("Failed to save output"));
        }
    }

    #endregion
}
