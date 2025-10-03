using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

/// <summary>
/// Unit tests for UserSession model.
/// Tests session management and SSH key authentication tracking.
/// </summary>
public sealed class UserSessionTests
{
    [Fact]
    public void Create_WithValidSession_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sshKeyHash = "sha256:abc123def456...";

        // Act
        var session = new UserSession
        {
            SessionId = sessionId,
            SshKeyHash = sshKeyHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().Be(sessionId);
        session.SshKeyHash.Should().Be(sshKeyHash);
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SessionId_ShouldBeUniqueGuid()
    {
        // Arrange & Act
        var session1 = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash1",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        var session2 = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash2",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        // Assert
        session1.SessionId.Should().NotBe(session2.SessionId);
    }

    [Fact]
    public void SshKeyHash_ShouldStoreHashValue()
    {
        // Arrange
        var expectedHash = "sha256:1234567890abcdef";

        // Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = expectedHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        // Assert
        session.SshKeyHash.Should().Be(expectedHash);
    }

    [Fact]
    public void IsActive_WhenTrue_IndicatesActiveSession()
    {
        // Arrange & Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        // Assert
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenFalse_IndicatesInactiveSession()
    {
        // Arrange & Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            LastAccessedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        // Assert
        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreatedAt_ShouldRecordSessionCreationTime()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2025, 10, 1, 10, 0, 0, TimeSpan.Zero);

        // Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = createdAt,
            LastAccessedAt = createdAt
        };

        // Assert
        session.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void LastAccessedAt_ShouldTrackMostRecentAccess()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var lastAccessedAt = DateTimeOffset.UtcNow;

        // Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = createdAt,
            LastAccessedAt = lastAccessedAt
        };

        // Assert
        session.LastAccessedAt.Should().Be(lastAccessedAt);
        session.LastAccessedAt.Should().BeAfter(session.CreatedAt);
    }

    [Fact]
    public void LastAccessedAt_CanEqualCreatedAt_ForNewSessions()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = timestamp,
            LastAccessedAt = timestamp
        };

        // Assert
        session.CreatedAt.Should().Be(session.LastAccessedAt);
    }

    [Fact]
    public void UserSession_IsImmutable_PropertiesAreInitOnly()
    {
        // Arrange
        var original = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "original-hash",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        // Act - Create modified copy using 'with' expression
        var modified = original with { IsActive = false, LastAccessedAt = DateTimeOffset.UtcNow.AddMinutes(5) };

        // Assert
        original.IsActive.Should().BeTrue();
        modified.IsActive.Should().BeFalse();
        original.Should().NotBe(modified);
    }

    [Fact]
    public void SessionDuration_CanBeCalculatedFromTimestamps()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2025, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var lastAccessedAt = new DateTimeOffset(2025, 10, 1, 11, 30, 0, TimeSpan.Zero);

        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = createdAt,
            LastAccessedAt = lastAccessedAt
        };

        // Act
        TimeSpan duration = session.LastAccessedAt - session.CreatedAt;

        // Assert
        duration.TotalMinutes.Should().Be(90);
    }

    [Fact]
    public void ExpiresAt_WhenProvided_ShouldStoreExpirationTime()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        // Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

        // Assert
        session.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void ExpiresAt_CanBeNull_ForNonExpiringSession()
    {
        // Arrange & Act
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "hash",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        };

        // Assert
        session.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void SshKeyHash_ShouldSupportDifferentHashAlgorithms()
    {
        // Arrange & Act
        var sha256Session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "sha256:abc123",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        var sha512Session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "sha512:xyz789",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        // Assert
        sha256Session.SshKeyHash.Should().StartWith("sha256:");
        sha512Session.SshKeyHash.Should().StartWith("sha512:");
    }
}
