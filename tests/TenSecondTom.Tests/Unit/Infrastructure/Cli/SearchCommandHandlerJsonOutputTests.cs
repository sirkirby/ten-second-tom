using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Cli;

/// <summary>
/// Tests to verify SearchCommandHandler properly formats JSON output.
/// These tests ensure the --output-json flag works correctly.
/// </summary>
public sealed class SearchCommandHandlerJsonOutputTests : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalConsoleOut;

    public SearchCommandHandlerJsonOutputTests()
    {
        // Capture console output
        _stringWriter = new StringWriter();
        _originalConsoleOut = Console.Out;
        Console.SetOut(_stringWriter);
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WhenNotAuthenticated_OutputsValidJson()
    {
        // Arrange
        var mockStorageProvider = new Mock<IMemoryStorageProvider>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockLogger = new Mock<ILogger<SearchMemoriesQueryHandler>>();
        
        var handler = new SearchMemoriesQueryHandler(
            mockStorageProvider.Object,
            mockAuthService.Object,
            mockLogger.Object);
        
        mockAuthService
            .Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await SearchCommandHandler.ExecuteAsync(
            handler,
            mockAuthService.Object,
            "test query",
            jsonOutput: true,
            cancellationToken: CancellationToken.None);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(output);
        var root = jsonDoc.RootElement;
        
        // Verify JSON structure
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("command").GetString().Should().Be("search");
        root.GetProperty("error").GetString().Should().Contain("Authentication");
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WhenSearchSucceeds_OutputsValidJsonWithResults()
    {
        // Arrange
        var mockStorageProvider = new Mock<IMemoryStorageProvider>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockLogger = new Mock<ILogger<SearchMemoriesQueryHandler>>();
        
        var handler = new SearchMemoriesQueryHandler(
            mockStorageProvider.Object,
            mockAuthService.Object,
            mockLogger.Object);
        
        mockAuthService
            .Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var testEntry = new MemoryEntry
        {
            EntryId = "today-10-08-2025-1",
            EntryNumber = 1,
            Command = "today",
            Timestamp = new DateTimeOffset(2025, 10, 8, 12, 0, 0, TimeSpan.Zero),
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };

        mockStorageProvider
            .Setup(x => x.SearchEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { testEntry });

        // Act
        await SearchCommandHandler.ExecuteAsync(
            handler,
            mockAuthService.Object,
            "test query",
            jsonOutput: true,
            cancellationToken: CancellationToken.None);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(output);
        var root = jsonDoc.RootElement;
        
        // Verify JSON structure
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("command").GetString().Should().Be("search");
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
        
        // Verify data structure
        var data = root.GetProperty("data");
        data.GetProperty("query").GetString().Should().Be("test query");
        data.GetProperty("resultCount").GetInt32().Should().Be(1);
        
        // Verify results array
        var results = data.GetProperty("results");
        results.GetArrayLength().Should().Be(1);
        
        var firstResult = results[0];
        firstResult.GetProperty("entryNumber").GetInt32().Should().Be(1);
        firstResult.GetProperty("command").GetString().Should().Be("today");
        firstResult.GetProperty("userInput").GetString().Should().Be("Test input");
    }

    public void Dispose()
    {
        Console.SetOut(_originalConsoleOut);
        _stringWriter.Dispose();
    }
}
