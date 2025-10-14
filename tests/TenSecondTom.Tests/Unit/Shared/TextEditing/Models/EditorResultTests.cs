using FluentAssertions;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Tests.Unit.Shared.TextEditing.Models;

public sealed class EditorResultTests
{
    [Fact]
    public void Saved_CreatesSuccessResult_WithContent()
    {
        // Arrange
        var content = "Test content";
        var metadata = new EditorMetadata
        {
            SessionId = Guid.NewGuid(),
            Duration = TimeSpan.FromSeconds(30),
            LineCount = 1,
            CharacterCount = content.Length,
            WasModified = true
        };

        // Act
        var result = EditorResult.Saved(content, metadata);

        // Assert
        result.Outcome.Should().Be(EditorOutcome.Saved);
        result.Content.Should().Be(content);
        result.IsSaved.Should().BeTrue();
        result.IsCancelled.Should().BeFalse();
        result.IsError.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void Cancelled_CreatesResultWithEmptyContent()
    {
        // Arrange
        var metadata = new EditorMetadata
        {
            SessionId = Guid.NewGuid(),
            Duration = TimeSpan.FromSeconds(10),
            LineCount = 0,
            CharacterCount = 0,
            WasModified = false
        };

        // Act
        var result = EditorResult.Cancelled(metadata);

        // Assert
        result.Outcome.Should().Be(EditorOutcome.Cancelled);
        result.Content.Should().BeEmpty();
        result.IsSaved.Should().BeFalse();
        result.IsCancelled.Should().BeTrue();
        result.IsError.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void Error_CreatesResultWithErrorMessage()
    {
        // Arrange
        var errorMessage = "Terminal initialization failed";
        var metadata = new EditorMetadata
        {
            SessionId = Guid.NewGuid(),
            Duration = TimeSpan.FromSeconds(1),
            LineCount = 0,
            CharacterCount = 0,
            WasModified = false
        };

        // Act
        var result = EditorResult.Error(errorMessage, metadata);

        // Assert
        result.Outcome.Should().Be(EditorOutcome.Error);
        result.Content.Should().BeEmpty();
        result.IsSaved.Should().BeFalse();
        result.IsCancelled.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Metadata.Should().Be(metadata);
    }
}
