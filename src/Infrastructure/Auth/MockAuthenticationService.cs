using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Mock authentication service for development and testing.
/// Always authenticates successfully without requiring SSH keys.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Referenced by DI container")]
public sealed class MockAuthenticationService : IAuthenticationService
{
    private readonly ILogger<MockAuthenticationService> _logger;
    private UserSession? _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAuthenticationService"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public MockAuthenticationService(ILogger<MockAuthenticationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<Result<UserSession>> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Simple warning message for development mode
        _logger.LogWarning("Using MockAuthenticationService - authentication bypassed for development");
#pragma warning restore CA1848
        
        _session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "mock-session",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        };

        return Task.FromResult(Result<UserSession>.Success(_session));
    }

    /// <inheritdoc/>
    public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_session?.IsActive ?? false);
    }

    /// <inheritdoc/>
    public Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session == null || !_session.IsActive)
        {
            return Task.FromResult(Result<bool>.Failure("No active session to logout."));
        }

        _session = null;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Simple log message for development mode
        _logger.LogInformation("Mock session logged out");
#pragma warning restore CA1848
        return Task.FromResult(Result<bool>.Success(true));
    }
}
