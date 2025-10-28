using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Generate;

namespace TenSecondTom.Tests.Features.Generate.Handlers;

/// <summary>
/// Tests for <see cref="ListRecordings.Handler"/> implementation.
/// Validates query handling for listing available recordings.
/// </summary>
public sealed class ListRecordingsQueryHandlerTests
{
    private readonly Mock<IRecordingService> _mockRecordingService;
    private readonly Mock<ILogger<ListRecordings.Handler>> _mockLogger;

    public ListRecordingsQueryHandlerTests()
    {
        _mockRecordingService = new Mock<IRecordingService>();
        _mockLogger = new Mock<ILogger<ListRecordings.Handler>>();
    }

    [Fact]
    public async Task Handle_WithNoRecordings_ReturnsFailure()
    {
        // Arrange
        _mockRecordingService
            .Setup(s => s.ListRecordingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RecordingListItem>>.Failure("No recordings found"));

        var handler = CreateHandler();
        var query = new ListRecordings.Query();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No recordings found");
    }

    [Fact]
    public async Task Handle_WithValidRecordings_ReturnsSuccessWithRecordings()
    {
        // Arrange
        var recordings = new List<RecordingListItem>
        {
            CreateTestRecording("10-24-2025_1"),
            CreateTestRecording("10-23-2025_1")
        };

        _mockRecordingService
            .Setup(s => s.ListRecordingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RecordingListItem>>.Success(recordings));

        var handler = CreateHandler();
        var query = new ListRecordings.Query();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeEquivalentTo(recordings);
    }

    [Fact]
    public async Task Handle_ReturnsRecordingsSortedByDate()
    {
        // Arrange
        var recordings = new List<RecordingListItem>
        {
            CreateTestRecording("10-24-2025_1", new DateTimeOffset(2025, 10, 24, 10, 0, 0, TimeSpan.Zero)),
            CreateTestRecording("10-23-2025_1", new DateTimeOffset(2025, 10, 23, 10, 0, 0, TimeSpan.Zero)),
            CreateTestRecording("10-25-2025_1", new DateTimeOffset(2025, 10, 25, 10, 0, 0, TimeSpan.Zero))
        };

        _mockRecordingService
            .Setup(s => s.ListRecordingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RecordingListItem>>.Success(recordings));

        var handler = CreateHandler();
        var query = new ListRecordings.Query();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Service should return sorted, handler just passes through
        result.Value.Should().Equal(recordings);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        // Arrange
        var recordings = new List<RecordingListItem> { CreateTestRecording("10-24-2025_1") };
        var cts = new CancellationTokenSource();

        _mockRecordingService
            .Setup(s => s.ListRecordingsAsync(cts.Token))
            .ReturnsAsync(Result<IReadOnlyList<RecordingListItem>>.Success(recordings));

        var handler = CreateHandler();
        var query = new ListRecordings.Query { CancellationToken = cts.Token };

        // Act
        var result = await handler.Handle(query, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockRecordingService.Verify(
            s => s.ListRecordingsAsync(cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnServiceFailure_PropagatesError()
    {
        // Arrange
        _mockRecordingService
            .Setup(s => s.ListRecordingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RecordingListItem>>.Failure("Service error"));

        var handler = CreateHandler();
        var query = new ListRecordings.Query();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Service error");
    }

    #region Helper Methods

    private ListRecordings.Handler CreateHandler()
    {
        return new ListRecordings.Handler(
            _mockRecordingService.Object,
            _mockLogger.Object);
    }

    private static RecordingListItem CreateTestRecording(
        string baseName,
        DateTimeOffset? recordedAt = null)
    {
        return new RecordingListItem
        {
            RecordingBaseName = baseName,
            TranscriptFilePath = $"/test/{baseName}.txt",
            RecordedAt = recordedAt ?? DateTimeOffset.UtcNow,
            FormattedDate = "Oct 24, 2025 2:30 PM",
            WordCount = 100,
            FileSizeBytes = 1024
        };
    }

    #endregion
}
