using FluentAssertions;
using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Features.Shell.Services;
using Xunit;

namespace TenSecondTom.Tests.Features.Shell;

/// <summary>
/// Unit tests for SessionManager edge cases, particularly circular buffer behavior.
/// </summary>
public sealed class SessionManagerTests
{
    [Fact]
    public void AddToHistory_Adding101Entries_RemovesOldestAndKeepsLatest100()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();

        // Act - Add 101 entries to overflow the 100-entry buffer
        for (int i = 1; i <= 101; i++)
        {
            sessionManager.AddToHistory($"/command{i}", wasSuccessful: true, wasInterrupted: false, $"Result {i}");
        }

        // Assert
        var history = sessionManager.GetHistory();
        history.Should().HaveCount(100, "circular buffer should maintain exactly 100 entries");
        
        // First entry should be command2 (command1 was removed)
        history[0].Command.Should().Be("/command2", "oldest entry (/command1) should be removed");
        
        // Last entry should be command101
        history[99].Command.Should().Be("/command101", "newest entry should be retained");
        
        // Verify order is maintained
        for (int i = 0; i < 99; i++)
        {
            history[i].SequenceNumber.Should().BeLessThan(history[i + 1].SequenceNumber,
                "sequence numbers should remain in order after buffer overflow");
        }
    }

    [Fact]
    public void AddToHistory_SequenceNumbers_ContinueIncrementingAfterOverflow()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();

        // Act - Add entries to cause overflow
        for (int i = 1; i <= 105; i++)
        {
            sessionManager.AddToHistory($"/cmd{i}", wasSuccessful: true, wasInterrupted: false);
        }

        // Assert
        var history = sessionManager.GetHistory();
        
        // First sequence number should be 6 (entries 1-5 were removed)
        history[0].SequenceNumber.Should().Be(6, "sequence numbers should continue incrementing, not reset");
        
        // Last sequence number should be 105
        history[^1].SequenceNumber.Should().Be(105, "sequence should reach the total count added");
        
        // All sequence numbers should be consecutive
        for (int i = 0; i < history.Count - 1; i++)
        {
            (history[i + 1].SequenceNumber - history[i].SequenceNumber).Should().Be(1,
                "sequence numbers should be consecutive");
        }
    }

    [Fact]
    public void GetHistory_AfterOverflow_ReturnsLatest100Only()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        
        // Add 150 commands
        for (int i = 1; i <= 150; i++)
        {
            sessionManager.AddToHistory($"/cmd{i}", wasSuccessful: true, wasInterrupted: false);
        }

        // Act
        var history = sessionManager.GetHistory();

        // Assert
        history.Should().HaveCount(100, "should never exceed 100 entries");
        
        // Should contain commands 51-150
        history[0].Command.Should().Be("/cmd51");
        history[49].Command.Should().Be("/cmd100");
        history[99].Command.Should().Be("/cmd150");
        
        // Should NOT contain commands 1-50
        history.Should().NotContain(entry => entry.Command == "/cmd1");
        history.Should().NotContain(entry => entry.Command == "/cmd50");
    }

    [Fact]
    public void AddToHistory_ExactlyAtCapacity_DoesNotRemoveEntries()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();

        // Act - Add exactly 100 entries (at capacity, not over)
        for (int i = 1; i <= 100; i++)
        {
            sessionManager.AddToHistory($"/cmd{i}", wasSuccessful: true, wasInterrupted: false);
        }

        // Assert
        var history = sessionManager.GetHistory();
        history.Should().HaveCount(100);
        
        // All original entries should still be present
        history[0].Command.Should().Be("/cmd1", "first entry should not be removed when at capacity");
        history[99].Command.Should().Be("/cmd100");
    }

    [Fact]
    public void AddToHistory_MultipleOverflows_MaintainsCorrectState()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();

        // Act - Add 250 entries (overflow multiple times)
        for (int i = 1; i <= 250; i++)
        {
            sessionManager.AddToHistory($"/cmd{i}", wasSuccessful: i % 2 == 0, wasInterrupted: false);
        }

        // Assert
        var history = sessionManager.GetHistory();
        history.Should().HaveCount(100);
        
        // Should contain entries 151-250
        history[0].Command.Should().Be("/cmd151");
        history[0].SequenceNumber.Should().Be(151);
        history[99].Command.Should().Be("/cmd250");
        history[99].SequenceNumber.Should().Be(250);
        
        // Verify success flags are preserved correctly
        history[0].WasSuccessful.Should().BeFalse(); // 151 is odd
        history[1].WasSuccessful.Should().BeTrue();  // 152 is even
    }

    [Fact]
    public void GetHistory_ReturnsReadOnlyList_PreventingExternalModification()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        sessionManager.AddToHistory("/cmd1", wasSuccessful: true, wasInterrupted: false);

        // Act
        var history = sessionManager.GetHistory();

        // Assert
        history.Should().BeAssignableTo<IReadOnlyList<CommandHistoryEntry>>(
            "history should be read-only to prevent external modification");
        
        // Verify attempting to cast to mutable collection type would fail at runtime
        // ReadOnlyCollection<T> does implement IList<T> for compatibility, but mutations throw
        if (history is IList<CommandHistoryEntry> mutableList)
        {
            // Verify that mutation operations throw
            Action addAction = () => mutableList.Add(new CommandHistoryEntry
            {
                SequenceNumber = 999,
                Command = "/test",
                WasSuccessful = true
            });
            
            addAction.Should().Throw<NotSupportedException>(
                "ReadOnlyCollection should not allow Add operations");
        }
    }

    [Fact]
    public void GetHistory_EmptySession_ReturnsEmptyList()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();

        // Act
        var history = sessionManager.GetHistory();

        // Assert
        history.Should().BeEmpty("new session should have no history");
    }
}
