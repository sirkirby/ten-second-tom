using FluentAssertions;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Tests.Unit.Shared.TextEditing.Models;

public sealed class EditorMetadataTests
{
    [Fact]
    public void FromSession_ExtractsSessionDataCorrectly()
    {
        // Arrange
        var initialContent = "Initial content";
        var modifiedContent = "Modified content with more text";
        var session = new TextEditingSession(initialContent);

        session.UpdateContent(modifiedContent);
        Thread.Sleep(10); // Small delay to ensure measurable duration
        session.Complete(EditorOutcome.Saved);

        // Act
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.SessionId.Should().Be(session.SessionId);
        metadata.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        metadata.LineCount.Should().Be(session.LineCount);
        metadata.CharacterCount.Should().Be(modifiedContent.Length);
        metadata.WasModified.Should().BeTrue();
    }

    [Fact]
    public void FromSession_HandlesEmptyContent()
    {
        // Arrange
        var session = new TextEditingSession();
        session.Complete(EditorOutcome.Cancelled);

        // Act
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.SessionId.Should().Be(session.SessionId);
        metadata.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        metadata.LineCount.Should().Be(1); // Empty string has 1 line per Split behavior
        metadata.CharacterCount.Should().Be(0);
        metadata.WasModified.Should().BeFalse();
    }

    [Fact]
    public void Empty_ProvidesDefaultMetadata()
    {
        // Act
        var metadata = EditorMetadata.Empty;

        // Assert
        metadata.SessionId.Should().Be(Guid.Empty);
        metadata.Duration.Should().Be(TimeSpan.Zero);
        metadata.LineCount.Should().Be(0);
        metadata.CharacterCount.Should().Be(0);
        metadata.WasModified.Should().BeFalse();
    }

    /// <summary>
    /// T040: Test WasModified flag when content is unchanged from initial
    /// </summary>
    [Fact]
    public void FromSession_WasModified_IsFalseWhenContentUnchanged()
    {
        // Arrange: User opens existing entry but makes no changes
        var initialContent = "Existing entry content";
        var session = new TextEditingSession(initialContent);
        
        // Don't update content - user just opened and closed
        session.Complete(EditorOutcome.Saved);

        // Act
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.WasModified.Should().BeFalse("content was not changed");
        metadata.CharacterCount.Should().Be(initialContent.Length);
    }

    /// <summary>
    /// T040: Test WasModified flag when content is changed from initial
    /// </summary>
    [Fact]
    public void FromSession_WasModified_IsTrueWhenContentChanged()
    {
        // Arrange: User edits existing entry
        var initialContent = "Original text";
        var modifiedContent = "Original text with additions";
        var session = new TextEditingSession(initialContent);
        
        // Act: User modifies the content
        session.UpdateContent(modifiedContent);
        session.Complete(EditorOutcome.Saved);
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.WasModified.Should().BeTrue("content was changed");
        metadata.CharacterCount.Should().Be(modifiedContent.Length);
        metadata.CharacterCount.Should().NotBe(initialContent.Length);
    }

    /// <summary>
    /// T040: Test WasModified flag when creating new entry (no initial content)
    /// </summary>
    [Fact]
    public void FromSession_WasModified_IsTrueForNewEntry()
    {
        // Arrange: New entry (no initial content)
        var session = new TextEditingSession(); // null initial content
        
        // Act: User adds content
        var newContent = "This is a new entry";
        session.UpdateContent(newContent);
        session.Complete(EditorOutcome.Saved);
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.WasModified.Should().BeTrue("new content was added");
        metadata.CharacterCount.Should().Be(newContent.Length);
    }

    /// <summary>
    /// T040: Test WasModified flag when content is set to same value
    /// </summary>
    [Fact]
    public void FromSession_WasModified_IsFalseWhenSetToSameValue()
    {
        // Arrange: Edit scenario where user "changes" but ends up with same content
        var initialContent = "Same text";
        var session = new TextEditingSession(initialContent);
        
        // Act: Update to same content (e.g., user made changes then undid them)
        session.UpdateContent("Same text"); // Identical to initial
        session.Complete(EditorOutcome.Saved);
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.WasModified.Should().BeFalse("final content equals initial content");
    }

    /// <summary>
    /// T040: Test WasModified with whitespace-only changes
    /// </summary>
    [Fact]
    public void FromSession_WasModified_IsTrueForWhitespaceChanges()
    {
        // Arrange: Subtle whitespace changes
        var initialContent = "Line 1\nLine 2";
        var modifiedContent = "Line 1\n\nLine 2"; // Added blank line
        var session = new TextEditingSession(initialContent);
        
        // Act
        session.UpdateContent(modifiedContent);
        session.Complete(EditorOutcome.Saved);
        var metadata = EditorMetadata.FromSession(session);

        // Assert
        metadata.WasModified.Should().BeTrue("whitespace changes are modifications");
        metadata.LineCount.Should().Be(3);
    }

    /// <summary>
    /// T040: Test WasModified integrated with EditorResult
    /// </summary>
    [Fact]
    public void EditorResult_IncludesWasModifiedInMetadata()
    {
        // Arrange
        var initialContent = "Initial";
        var modifiedContent = "Initial with changes";
        var session = new TextEditingSession(initialContent);
        
        session.UpdateContent(modifiedContent);
        session.Complete(EditorOutcome.Saved);

        // Act: Create EditorResult with metadata
        var metadata = EditorMetadata.FromSession(session);
        var result = EditorResult.Saved(modifiedContent, metadata);

        // Assert: Can access WasModified through result
        result.IsSaved.Should().BeTrue();
        result.Metadata.WasModified.Should().BeTrue();
        result.Content.Should().Be(modifiedContent);
    }
}
