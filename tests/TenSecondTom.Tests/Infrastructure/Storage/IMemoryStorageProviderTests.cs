using FluentAssertions;
using Moq;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Infrastructure.Storage;

/// <summary>
/// Unit tests for IMemoryStorageProvider interface contract.
/// Tests define expected behavior using mock implementations.
/// </summary>
public sealed class IMemoryStorageProviderTests
{
    private readonly Mock<IMemoryStorageProvider> _mockProvider;

    public IMemoryStorageProviderTests()
    {
        _mockProvider = new Mock<IMemoryStorageProvider>();
    }

    [Fact]
    public async Task SaveAsync_CreatesEntryWithCorrectEntryId()
    {
        // Arrange
        var entry = new MemoryEntry
        {
            EntryId = "today-10-02-2025-1",
            Command = "today",
            Timestamp = DateTime.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };

        _mockProvider
            .Setup(p => p.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry>.Success(entry));

        // Act
        Result<MemoryEntry> result = await _mockProvider.Object.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EntryId.Should().Be("today-10-02-2025-1");
    }

    [Fact]
    public async Task SaveAsync_ReturnsResultOfMemoryEntry()
    {
        // Arrange
        var entry = new MemoryEntry
        {
            EntryId = "today-10-02-2025-1",
            Command = "today",
            Timestamp = DateTime.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };

        _mockProvider
            .Setup(p => p.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry>.Success(entry));

        // Act
        Result<MemoryEntry> result = await _mockProvider.Object.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Result<MemoryEntry>>();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetEntriesAsync_FiltersByCommandAndDateRange()
    {
        // Arrange
        DateTime startDate = new(2025, 10, 1);
        DateTime endDate = new(2025, 10, 2);
        var entries = new List<MemoryEntry>
        {
            new()
            {
                EntryId = "today-10-01-2025-1",
                Command = "today",
                Timestamp = new DateTime(2025, 10, 1, 12, 0, 0),
                EntryNumber = 1,
                UserInput = "Test input",
                LlmResponse = "Test response",
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "OpenAI",
                    LlmModel = "gpt-4"
                }
            }
        };

        _mockProvider
            .Setup(p => p.GetEntriesAsync("today", startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(entries));

        // Act
        Result<IReadOnlyList<MemoryEntry>> result = await _mockProvider.Object
            .GetEntriesAsync("today", startDate, endDate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Command.Should().Be("today");
        result.Value[0].Timestamp.Should().BeOnOrAfter(startDate).And.BeOnOrBefore(endDate);
    }

    [Fact]
    public async Task CountEntriesAsync_ReturnsCorrectCountForDate()
    {
        // Arrange
        DateTime targetDate = new(2025, 10, 2);
        _mockProvider
            .Setup(p => p.CountEntriesAsync("today", targetDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(3));

        // Act
        Result<int> result = await _mockProvider.Object
            .CountEntriesAsync("today", targetDate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
    }

    [Fact]
    public async Task SearchEntriesAsync_FiltersByQueryText()
    {
        // Arrange
        const string query = "meeting";
        var entries = new List<MemoryEntry>
        {
            new()
            {
                EntryId = "today-10-01-2025-1",
                Command = "today",
                Timestamp = DateTime.UtcNow,
                EntryNumber = 1,
                UserInput = "Had a productive meeting today",
                LlmResponse = "Test response",
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "OpenAI",
                    LlmModel = "gpt-4"
                }
            }
        };

        _mockProvider
            .Setup(p => p.SearchEntriesAsync(query, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(entries));

        // Act
        Result<IReadOnlyList<MemoryEntry>> result = await _mockProvider.Object
            .SearchEntriesAsync(query, null, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].UserInput.Should().ContainEquivalentOf(query);
    }

    [Fact]
    public async Task DeleteEntriesAsync_RemovesEntriesByDateRange()
    {
        // Arrange
        DateTime startDate = new(2025, 10, 1);
        DateTime endDate = new(2025, 10, 2);
        _mockProvider
            .Setup(p => p.DeleteEntriesAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(5));

        // Act
        Result<int> result = await _mockProvider.Object
            .DeleteEntriesAsync(startDate, endDate, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_RespectsRetentionPolicy()
    {
        // Arrange
        var policy = RetentionPolicy.Days30;
        _mockProvider
            .Setup(p => p.PurgeExpiredEntriesAsync(policy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(10));

        // Act
        Result<int> result = await _mockProvider.Object
            .PurgeExpiredEntriesAsync(policy, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task GetEntryByIdAsync_RetrievesSpecificEntry()
    {
        // Arrange
        const string entryId = "today-10-02-2025-1";
        var entry = new MemoryEntry
        {
            EntryId = entryId,
            Command = "today",
            Timestamp = DateTime.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };

        _mockProvider
            .Setup(p => p.GetEntryByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(entry));

        // Act
        Result<MemoryEntry?> result = await _mockProvider.Object
            .GetEntryByIdAsync(entryId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.EntryId.Should().Be(entryId);
    }

    [Fact]
    public async Task GetEntryByIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        const string entryId = "nonexistent-entry";
        _mockProvider
            .Setup(p => p.GetEntryByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(null));

        // Act
        Result<MemoryEntry?> result = await _mockProvider.Object
            .GetEntryByIdAsync(entryId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_HandlesErrors()
    {
        // Arrange
        var entry = new MemoryEntry
        {
            EntryId = "today-10-02-2025-1",
            Command = "today",
            Timestamp = DateTime.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };

        _mockProvider
            .Setup(p => p.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry>.Failure("Storage error"));

        // Act
        Result<MemoryEntry> result = await _mockProvider.Object.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Storage error");
    }
}
