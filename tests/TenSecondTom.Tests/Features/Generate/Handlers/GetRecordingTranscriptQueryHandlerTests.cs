using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Generate;

namespace TenSecondTom.Tests.Features.Generate.Handlers;

/// <summary>
/// Tests for <see cref="GetRecordingTranscript.Handler"/> implementation.
/// Validates query handling for loading transcript content.
/// </summary>
public sealed class GetRecordingTranscriptQueryHandlerTests
{
    private readonly Mock<IRecordingService> _mockRecordingService;
    private readonly Mock<ILogger<GetRecordingTranscript.Handler>> _mockLogger;

    public GetRecordingTranscriptQueryHandlerTests()
    {
        _mockRecordingService = new Mock<IRecordingService>();
        _mockLogger = new Mock<ILogger<GetRecordingTranscript.Handler>>();
    }

    [Fact]
    public async Task Handle_WithValidTranscript_ReturnsContent()
    {
        // Arrange
        var expectedContent = "This is the transcript content.";
        var transcriptPath = "/test/recording/10-21-2025_1.txt";

        _mockRecordingService
            .Setup(s => s.GetTranscriptContentAsync(transcriptPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(expectedContent));

        var handler = CreateHandler();
        var query = new GetRecordingTranscript.Query
        {
            TranscriptFilePath = transcriptPath
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedContent);
    }

    [Fact]
    public async Task Handle_WithFileNotFound_ReturnsFailure()
    {
        // Arrange
        var transcriptPath = "/test/missing.txt";

        _mockRecordingService
            .Setup(s => s.GetTranscriptContentAsync(transcriptPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Transcript file not found"));

        var handler = CreateHandler();
        var query = new GetRecordingTranscript.Query
        {
            TranscriptFilePath = transcriptPath
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithEmptyPath_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var query = new GetRecordingTranscript.Query
        {
            TranscriptFilePath = string.Empty
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("required");
    }

    [Fact]
    public async Task Handle_WithNullPath_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var query = new GetRecordingTranscript.Query
        {
            TranscriptFilePath = null!
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        // Arrange
        var transcriptPath = "/test/recording.txt";
        var cts = new CancellationTokenSource();

        _mockRecordingService
            .Setup(s => s.GetTranscriptContentAsync(transcriptPath, cts.Token))
            .ReturnsAsync(Result<string>.Success("content"));

        var handler = CreateHandler();
        var query = new GetRecordingTranscript.Query
        {
            TranscriptFilePath = transcriptPath,
            CancellationToken = cts.Token
        };

        // Act
        var result = await handler.Handle(query, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRecordingService.Verify(
            s => s.GetTranscriptContentAsync(transcriptPath, cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnServiceFailure_PropagatesError()
    {
        // Arrange
        var transcriptPath = "/test/recording.txt";

        _mockRecordingService
            .Setup(s => s.GetTranscriptContentAsync(transcriptPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Unable to read file"));

        var handler = CreateHandler();
        var query = new GetRecordingTranscript.Query
        {
            TranscriptFilePath = transcriptPath
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Unable to read file");
    }

    #region Helper Methods

    private GetRecordingTranscript.Handler CreateHandler()
    {
        return new GetRecordingTranscript.Handler(
            _mockRecordingService.Object,
            _mockLogger.Object);
    }

    #endregion
}
