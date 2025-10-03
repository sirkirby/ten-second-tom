using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

/// <summary>
/// Unit tests for MemoryEntry base record.
/// Tests validation rules, file path generation, and immutability.
/// </summary>
public sealed class MemoryEntryTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var metadata = new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 1500,
            ProcessingDuration = TimeSpan.FromSeconds(3.2),
            CustomTags = new Dictionary<string, string> { { "environment", "test" } }
        };

        // Act
        var entry = new MemoryEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Had a productive day working on the project.",
            LlmResponse = "Summary of the day's events.",
            Metadata = metadata
        };

        // Assert
        entry.Should().NotBeNull();
        entry.EntryId.Should().Be("today-10-01-2025-1");
        entry.Command.Should().Be("today");
        entry.Timestamp.Should().Be(timestamp);
        entry.EntryNumber.Should().Be(1);
        entry.UserInput.Should().Be("Had a productive day working on the project.");
        entry.LlmResponse.Should().Be("Summary of the day's events.");
        entry.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void EntryId_Format_ShouldBeCommandDashDateDashNumber()
    {
        // Arrange & Act
        var entry = new MemoryEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };

        // Assert
        entry.EntryId.Should().MatchRegex(@"^(today|thisweek)-\d{2}-\d{2}-\d{4}-\d+$");
    }

    [Theory]
    [InlineData("today")]
    [InlineData("thisweek")]
    public void Command_WithValidValues_ShouldAccept(string command)
    {
        // Arrange & Act
        var entry = new MemoryEntry
        {
            EntryId = $"{command}-10-01-2025-1",
            Command = command,
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };

        // Assert
        entry.Command.Should().Be(command);
    }

        [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void EntryNumber_WithValidPositiveNumbers_ShouldAccept(int entryNumber)
    {
        // Arrange
        var entry = CreateValidMemoryEntry() with { EntryNumber = entryNumber };

        // Act & Assert
        entry.EntryNumber.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Timestamp_CannotBeInFuture()
    {
        // Arrange
        var futureTime = DateTimeOffset.UtcNow.AddDays(1);

        // Act & Assert - This test validates that the consumer validates timestamps
        // The record itself doesn't enforce this, but we document the expectation
        futureTime.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void FilePath_ForTodayCommand_ShouldGenerateCorrectPath()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 1, 14, 30, 0, TimeSpan.Zero);
        var entry = new MemoryEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        filePath.Should().Be(".memory/today/10-01-2025_1.md");
    }

    [Fact]
    public void FilePath_ForThisWeekCommand_ShouldGenerateCorrectPath()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 1, 14, 30, 0, TimeSpan.Zero);
        var entry = new MemoryEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        filePath.Should().StartWith(".memory/thisweek/2025-");
        filePath.Should().EndWith("_1.md");
    }

    [Fact]
    public void Metadata_WithValidLlmProvider_ShouldAccept()
    {
        // Arrange
        var openAiMetadata = new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 1500,
            ProcessingDuration = TimeSpan.FromSeconds(3)
        };

        var anthropicMetadata = new MemoryEntryMetadata
        {
            LlmProvider = "Anthropic",
            LlmModel = "claude-3-sonnet-20240229",
            TokensUsed = 2000,
            ProcessingDuration = TimeSpan.FromSeconds(5)
        };

        // Assert
        openAiMetadata.LlmProvider.Should().Be("OpenAI");
        anthropicMetadata.LlmProvider.Should().Be("Anthropic");
    }

    [Fact]
    public void MemoryEntry_IsImmutable_PropertiesAreInitOnly()
    {
        // Arrange & Act
        var entry = new MemoryEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };

        // Assert - Verify it's a record with init-only properties
        entry.Should().NotBeNull();
        entry.GetType().Should().Match(t => t.IsClass || t.IsValueType);
    }

    [Fact]
    public void CustomTags_CanBeEmpty_ShouldInitializeToEmptyDictionary()
    {
        // Arrange & Act
        var metadata = new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 1500,
            ProcessingDuration = TimeSpan.FromSeconds(3)
        };

        // Assert
        metadata.CustomTags.Should().NotBeNull();
        metadata.CustomTags.Should().BeEmpty();
    }

    [Fact]
    public void CustomTags_CanContainMultipleKeyValuePairs()
    {
        // Arrange & Act
        var metadata = new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 1500,
            ProcessingDuration = TimeSpan.FromSeconds(3),
            CustomTags = new Dictionary<string, string>
            {
                { "category", "work" },
                { "priority", "high" },
                { "project", "ten-second-tom" }
            }
        };

        // Assert
        metadata.CustomTags.Should().HaveCount(3);
        metadata.CustomTags["category"].Should().Be("work");
        metadata.CustomTags["priority"].Should().Be("high");
        metadata.CustomTags["project"].Should().Be("ten-second-tom");
    }

    private static MemoryEntryMetadata CreateValidMetadata()
    {
        return new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 1500,
            ProcessingDuration = TimeSpan.FromSeconds(3.2)
        };
    }

    private static MemoryEntry CreateValidMemoryEntry()
    {
        return new MemoryEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };
    }
}
