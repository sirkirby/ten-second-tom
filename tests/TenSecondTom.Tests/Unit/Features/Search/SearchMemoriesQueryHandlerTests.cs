using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.Search.Queries;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Search;

/// <summary>
/// Unit tests for SearchMemoriesQueryHandler.
/// Validates query handling, authentication checks, and search result filtering.
/// </summary>
public sealed class SearchMemoriesQueryHandlerTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorageProvider;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<ILogger<SearchMemoriesQueryHandler>> _mockLogger;
    private readonly SearchMemoriesQueryHandler _handler;

    public SearchMemoriesQueryHandlerTests()
    {
        _mockStorageProvider = new Mock<IMemoryStorageProvider>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockLogger = new Mock<ILogger<SearchMemoriesQueryHandler>>();

        _handler = new SearchMemoriesQueryHandler(
            _mockStorageProvider.Object,
            _mockAuthService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsMatchingEntries()
    {
        // Arrange
        var query = new SearchMemoriesQuery("meeting");
        var mockEntries = new List<MemoryEntry>
        {
            CreateDailyEntry("today-10-01-2025-1", "Had a productive morning meeting"),
            CreateDailyEntry("today-10-02-2025-1", "Follow-up meeting scheduled")
        };

        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockStorageProvider.Setup(x => x.SearchEntriesAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(mockEntries));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(e => e.EntryId == "today-10-01-2025-1");
        result.Value.Should().Contain(e => e.EntryId == "today-10-02-2025-1");
    }

    [Fact]
    public async Task Handle_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchMemoriesQuery("nonexistent");

        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockStorageProvider.Setup(x => x.SearchEntriesAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(new List<MemoryEntry>()));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithDateRangeFilter_PassesDateRangeToStorage()
    {
        // Arrange
        var startDate = new DateTime(2025, 10, 1);
        var endDate = new DateTime(2025, 10, 7);
        var query = new SearchMemoriesQuery("test", startDate, endDate);

        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockStorageProvider.Setup(x => x.SearchEntriesAsync(
            "test",
            startDate,
            endDate,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(new List<MemoryEntry>()));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorageProvider.Verify(x => x.SearchEntriesAsync(
            "test",
            startDate,
            endDate,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsAuthenticationError()
    {
        // Arrange
        var query = new SearchMemoriesQuery("test");
        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Authentication required");
    }

    [Fact]
    public async Task Handle_WithCaseInsensitiveQuery_SearchesCorrectly()
    {
        // Arrange
        var query = new SearchMemoriesQuery("MEETING");
        var mockEntries = new List<MemoryEntry>
        {
            CreateDailyEntry("today-10-01-2025-1", "Had a morning meeting")
        };

        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockStorageProvider.Setup(x => x.SearchEntriesAsync(
            "MEETING",
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(mockEntries));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenStorageFails_ReturnsError()
    {
        // Arrange
        var query = new SearchMemoriesQuery("test");
        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockStorageProvider.Setup(x => x.SearchEntriesAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Failure("Storage error occurred"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Storage error occurred");
    }

    [Fact]
    public async Task Handle_WithEmptyQuery_ReturnsValidationError()
    {
        // Arrange
        var query = new SearchMemoriesQuery("");
        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public async Task Handle_WithWhitespaceQuery_ReturnsValidationError()
    {
        // Arrange
        var query = new SearchMemoriesQuery("   ");
        _mockAuthService.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Query cannot be empty");
    }

    /// <summary>
    /// Helper method to create a DailyEntry for testing.
    /// </summary>
    private static DailyEntry CreateDailyEntry(string entryId, string userInput)
    {
        return new DailyEntry
        {
            EntryId = entryId,
            Command = "today",
            EntryNumber = 1,
            UserInput = userInput,
            LlmResponse = "Test response",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4",
                TokensUsed = 100
            }
        };
    }
}
