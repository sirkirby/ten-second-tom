using FluentAssertions;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Tests.Unit.Shared.TextEditing.Models;

public sealed class TextEditingSessionTests
{
    [Fact]
    public void Session_TracksContentChanges()
    {
        // Arrange
        var initialContent = "Initial";
        var session = new TextEditingSession(initialContent);

        // Act
        session.UpdateContent("Modified");

        // Assert
        session.InitialContent.Should().Be(initialContent);
        session.CurrentContent.Should().Be("Modified");
        session.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Complete_ThrowsWhenCalledTwice()
    {
        // Arrange
        var session = new TextEditingSession();
        session.Complete(EditorOutcome.Saved);

        // Act
        var act = () => session.Complete(EditorOutcome.Saved);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Session already completed");
    }

    [Fact]
    public void Duration_CalculatesCorrectly()
    {
        // Arrange
        var session = new TextEditingSession();
        var startTime = session.StartedAt;

        // Act
        Thread.Sleep(50); // Small delay to ensure measurable duration
        session.Complete(EditorOutcome.Saved);

        // Assert
        session.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        session.EndedAt.Should().NotBeNull();
        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateContent_ThrowsWhenSessionCompleted()
    {
        // Arrange
        var session = new TextEditingSession();
        session.Complete(EditorOutcome.Cancelled);

        // Act
        var act = () => session.UpdateContent("New content");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot update content of completed session");
    }

    [Fact]
    public void Session_HandlesEmptyInitialContent()
    {
        // Arrange & Act
        var session = new TextEditingSession();

        // Assert
        session.InitialContent.Should().BeEmpty();
        session.CurrentContent.Should().BeEmpty();
        session.HasChanges.Should().BeFalse();
        session.ContentLength.Should().Be(0);
        session.LineCount.Should().Be(1); // Empty string has 1 line per Split behavior
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Session_CalculatesLineCountCorrectly()
    {
        // Arrange
        var multiLineContent = "Line 1\nLine 2\nLine 3";
        var session = new TextEditingSession();

        // Act
        session.UpdateContent(multiLineContent);

        // Assert
        session.LineCount.Should().Be(3);
        session.ContentLength.Should().Be(multiLineContent.Length);
    }
}
