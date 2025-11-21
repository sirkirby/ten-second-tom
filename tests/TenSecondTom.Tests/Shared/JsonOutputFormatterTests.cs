using FluentAssertions;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.Tests.Shared;

/// <summary>
/// Unit tests for JSON output formatter.
/// </summary>
public sealed class JsonOutputFormatterTests
{
    [Fact]
    public void FormatSuccess_WithValidData_ReturnsValidJson()
    {
        // Arrange
        var data = new { Id = 1, Name = "Test" };
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatSuccess("test-command", data, timestamp);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"command\":\"test-command\"");
        json.Should().Contain("\"timestamp\":\"2025-10-02T14:30:00");
        json.Should().Contain("\"data\":");
        json.Should().Contain("\"id\":1");
        json.Should().Contain("\"name\":\"Test\"");
        json.Should().Contain("\"error\":null");
    }

    [Fact]
    public void FormatSuccess_WithNullData_ReturnsJsonWithNullData()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatSuccess("test-command", null, timestamp);

        // Assert
        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"data\":null");
    }

    [Fact]
    public void FormatFailure_WithErrorMessage_ReturnsValidJson()
    {
        // Arrange
        string errorMessage = "Authentication failed";
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatFailure("login", errorMessage, timestamp);

        // Assert
        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"command\":\"login\"");
        json.Should().Contain("\"error\":\"Authentication failed\"");
        json.Should().Contain("\"data\":null");
    }

    [Fact]
    public void FormatFailure_WithNullErrorMessage_ReturnsJsonWithNullError()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatFailure("test-command", null, timestamp);

        // Assert
        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"error\":null");
    }

    [Fact]
    public void FormatFromResult_WithSuccessResult_ReturnsSuccessJson()
    {
        // Arrange
        var data = new { Message = "Success" };
        var result = Result<object>.Success(data);
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatFromResult("test-command", result, timestamp);

        // Assert
        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"message\":\"Success\"");
    }

    [Fact]
    public void FormatFromResult_WithFailureResult_ReturnsFailureJson()
    {
        // Arrange
        var result = Result<object>.Failure("Operation failed");
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatFromResult("test-command", result, timestamp);

        // Assert
        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"error\":\"Operation failed\"");
    }

    [Fact]
    public void FormatFromResult_WithComplexObject_SerializesCorrectly()
    {
        // Arrange
        var data = new
        {
            EntryId = "today-10-02-2025-1",
            FilePath = ".memory/note/10-02-2025_1_generated.md",
            Summary = new
            {
                KeyEvents = new[] { "Event 1", "Event 2" },
                Themes = new[] { "Theme 1" },
                TodoItems = new[] { "Task 1" }
            }
        };
        var result = Result<object>.Success(data);
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);

        // Act
        string json = JsonOutputFormatter.FormatFromResult("today", result, timestamp);

        // Assert
        json.Should().Contain("\"entryId\":\"today-10-02-2025-1\"");
        json.Should().Contain("\"filePath\":\".memory/note/10-02-2025_1_generated.md\"");
        json.Should().Contain("\"keyEvents\"");
        json.Should().Contain("\"Event 1\"");
    }

    [Fact]
    public void FormatSuccess_WithIso8601Timestamp_ReturnsCorrectFormat()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 45, 123, TimeSpan.FromHours(-5));

        // Act
        string json = JsonOutputFormatter.FormatSuccess("test", null, timestamp);

        // Assert
        // ISO8601 format should include timezone offset
        json.Should().Match("*\"timestamp\":\"2025-10-02T14:30:45*\"*");
    }

    [Fact]
    public void FormatSuccess_WithEmptyCommandName_IncludesEmptyCommand()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        string json = JsonOutputFormatter.FormatSuccess(string.Empty, null, timestamp);

        // Assert
        json.Should().Contain("\"command\":\"\"");
    }

    [Fact]
    public void FormatFailure_WithSpecialCharactersInError_EscapesCorrectly()
    {
        // Arrange
        string errorMessage = "Error: \"Authentication\" failed\nReason: Connection timeout";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        string json = JsonOutputFormatter.FormatFailure("test", errorMessage, timestamp);

        // Assert
        json.Should().NotBeNullOrEmpty();
        // JSON should be valid (no unescaped quotes or newlines)
        var action = () => System.Text.Json.JsonDocument.Parse(json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Format_ProducesValidJson_ThatCanBeParsed()
    {
        // Arrange
        var data = new { Test = "value", Number = 42, Flag = true };
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        string json = JsonOutputFormatter.FormatSuccess("test", data, timestamp);

        // Assert - should be able to parse the JSON
        var action = () => System.Text.Json.JsonDocument.Parse(json);
        action.Should().NotThrow();
    }

    [Fact]
    public void FormatFromResult_WithNullCommand_IncludesNullCommand()
    {
        // Arrange
        var result = Result<object>.Success("data");
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        string json = JsonOutputFormatter.FormatFromResult(null!, result, timestamp);

        // Assert
        json.Should().Contain("\"command\":null");
    }
}
