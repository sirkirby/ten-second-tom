// This file is deprecated - the type has been moved to TenSecondTom.Shared.Models
// for proper architectural separation.

namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Deprecated: Use TenSecondTom.Shared.Models.ApiValidationResult instead.
/// This is a type alias for backward compatibility only.
/// </summary>
[Obsolete("Use TenSecondTom.Shared.Models.ApiValidationResult instead", false)]
public sealed record ApiValidationResult
{
    public required bool IsValid { get; init; }
    public bool FormatValid { get; init; }
    public bool NetworkValid { get; init; }
    public int RetryCount { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    public static ApiValidationResult Success(TimeSpan duration, int retryCount = 0) => new()
    {
        IsValid = true,
        FormatValid = true,
        NetworkValid = true,
        Duration = duration,
        RetryCount = retryCount
    };

    public static ApiValidationResult FormatFailure(string errorMessage) => new()
    {
        IsValid = false,
        FormatValid = false,
        NetworkValid = false,
        ErrorMessage = errorMessage,
        Duration = TimeSpan.Zero
    };

    public static ApiValidationResult NetworkFailure(string errorMessage, TimeSpan duration, int retryCount) => new()
    {
        IsValid = false,
        FormatValid = true,
        NetworkValid = false,
        ErrorMessage = errorMessage,
        Duration = duration,
        RetryCount = retryCount
    };
}
