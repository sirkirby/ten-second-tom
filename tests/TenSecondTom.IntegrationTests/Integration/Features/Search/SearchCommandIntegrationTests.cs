using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Search;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.IntegrationTests.Integration.Features.Search;

/// <summary>
/// Integration-style tests for the search CLI handler that verify authentication,
/// search execution, and JSON output formatting end-to-end.
/// </summary>
public sealed class SearchCommandIntegrationTests
{
    private readonly Mock<IMemoryStorageProvider> _storageProviderMock = new();
    private readonly Mock<IAuthenticationService> _authServiceMock = new();
    private readonly SearchMemories.Handler _handler;
    private readonly StorageOptions _storageOptions = new()
    {
        RootDirectory = "/tmp/ten-second-tom",
        MemoryDirectory = "/tmp/ten-second-tom/.memory"
    };

    public SearchCommandIntegrationTests()
    {
        _authServiceMock
            .Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _authServiceMock
            .Setup(a => a.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(new UserSession
            {
                SessionId = Guid.NewGuid(),
                SshKeyHash = "sha256:test",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow
            }));

        _handler = new SearchMemories.Handler(
            _storageProviderMock.Object,
            _authServiceMock.Object,
            Mock.Of<ILogger<SearchMemories.Handler>>());
    }

    [Fact]
    public async Task ExecuteAsync_WithResultsAndJsonOutput_WritesSuccessPayload()
    {
        // Arrange
        var entries = new List<MemoryEntry>
        {
            new()
            {
                EntryId = "today-2025-01-15-1",
                EntryNumber = 1,
                Command = CommandNames.Today,
                Timestamp = new DateTimeOffset(2025, 01, 15, 9, 30, 0, TimeSpan.Zero),
                UserInput = "Standup summary with focus points",
                LlmResponse = "AI summary",
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "OpenAI",
                    LlmModel = "gpt-4o-mini",
                    TokensUsed = 256,
                    ProcessingDuration = TimeSpan.FromSeconds(2)
                }
            },
            new()
            {
                EntryId = "thisweek-2025-01-12-1",
                EntryNumber = 2,
                Command = CommandNames.ThisWeek,
                Timestamp = new DateTimeOffset(2025, 01, 12, 8, 0, 0, TimeSpan.Zero),
                UserInput = "Weekly review with goals",
                LlmResponse = "Weekly recap",
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "Anthropic",
                    LlmModel = "claude-3-sonnet",
                    TokensUsed = 512,
                    ProcessingDuration = TimeSpan.FromSeconds(3)
                }
            }
        };

        _storageProviderMock
            .Setup(p => p.SearchEntriesAsync("focus", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(entries));

        using var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        try
        {
            // Act
            await SearchCommandHandler.ExecuteAsync(
                _handler,
                _authServiceMock.Object,
                _storageOptions,
                query: "focus",
                jsonOutput: true,
                cancellationToken: CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        string json = output.ToString().Trim();
        json.Should().NotBeNullOrEmpty();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("command").GetString().Should().Be(CommandNames.Search);

        var data = root.GetProperty("data");
        data.GetProperty("query").GetString().Should().Be("focus");
        data.GetProperty("resultCount").GetInt32().Should().Be(entries.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSearchFails_WritesJsonError()
    {
        // Arrange
        _storageProviderMock
            .Setup(p => p.SearchEntriesAsync("failure", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Failure("Index unavailable"));

        using var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        try
        {
            // Act
            await SearchCommandHandler.ExecuteAsync(
                _handler,
                _authServiceMock.Object,
                _storageOptions,
                query: "failure",
                jsonOutput: true,
                cancellationToken: CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        string json = output.ToString().Trim();
        json.Should().NotBeNullOrEmpty();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Contain("Index unavailable");
    }
}

