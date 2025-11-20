using FluentAssertions;
using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Features.Shell.Services;
using Xunit;

namespace TenSecondTom.Tests.Features.Shell;

/// <summary>
/// Contract tests for the Session Manager component.
/// Tests verify the interface contract defined in contracts/session-manager.md
/// </summary>
public sealed class SessionManagerContractTests
{
    [Fact]
    public void StartSession_InitializesNewSession()
    {
        // Arrange
        var sessionManager = new SessionManager();
        
        // Act
        sessionManager.StartSession();
        
        // Assert
        var session = sessionManager.GetCurrentSession();
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeEmpty();
        session.Status.Should().Be(SessionStatus.Active);
        session.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddToHistory_WithValidCommand_AddsEntry()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        
        // Act
        sessionManager.AddToHistory("/today", wasSuccessful: true, resultSummary: "Completed");
        
        // Assert
        var history = sessionManager.GetHistory();
        history.Should().HaveCount(1);
        history[0].Command.Should().Be("/today");
        history[0].WasSuccessful.Should().BeTrue();
        history[0].ResultSummary.Should().Be("Completed");
    }

    [Fact]
    public void AddToHistory_ExceedsCapacity_RemovesOldest()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        
        // Act
        // Add 101 commands to exceed capacity of 100
        for (int i = 1; i <= 101; i++)
        {
            sessionManager.AddToHistory($"/command{i}", wasSuccessful: true);
        }
        
        // Assert
        var history = sessionManager.GetHistory();
        history.Should().HaveCount(100);
        history[0].Command.Should().Be("/command2"); // First entry removed
        history[99].Command.Should().Be("/command101");
    }

    [Fact]
    public void GetHistory_ReturnsChronologicalOrder()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        sessionManager.AddToHistory("/first", wasSuccessful: true);
        sessionManager.AddToHistory("/second", wasSuccessful: true);
        sessionManager.AddToHistory("/third", wasSuccessful: true);
        
        // Act
        var history = sessionManager.GetHistory();
        
        // Assert
        history[0].Command.Should().Be("/first");
        history[1].Command.Should().Be("/second");
        history[2].Command.Should().Be("/third");
        history[0].SequenceNumber.Should().BeLessThan(history[1].SequenceNumber);
    }

    [Fact]
    public void EndSession_TerminatesSession()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        
        // Act
        sessionManager.EndSession();
        
        // Assert
        var session = sessionManager.GetCurrentSession();
        session.Should().NotBeNull();
        session!.Status.Should().Be(SessionStatus.Terminated);
        session.EndTime.Should().NotBeNull();
        session.EndTime.Should().BeAfter(session.StartTime);
    }

    [Fact]
    public void StartSession_CalledTwice_ThrowsException()
    {
        // Arrange
        var sessionManager = new SessionManager();
        sessionManager.StartSession();
        
        // Act
        Action act = () => sessionManager.StartSession();
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already active*");
    }

    [Fact]
    public void AddToHistory_BeforeStart_ThrowsException()
    {
        // Arrange
        var sessionManager = new SessionManager();
        
        // Act
        Action act = () => sessionManager.AddToHistory("/today", wasSuccessful: true);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*no active session*");
    }
}
