namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Result of API key validation
/// </summary>
public sealed record ApiValidationResult
{
    /// <summary>
    /// Gets whether the API key is valid
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets whether format validation passed
    /// </summary>
    public bool FormatValid { get; init; }

    /// <summary>
    /// Gets whether network validation passed
    /// </summary>
    public bool NetworkValid { get; init; }

    /// <summary>
    /// Gets the number of retry attempts made
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the total time spent on validation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the timestamp when validation was performed
    /// </summary>
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ApiValidationResult Success(TimeSpan duration, int retryCount = 0) => new()
    {
        IsValid = true,
        FormatValid = true,
        NetworkValid = true,
        Duration = duration,
        RetryCount = retryCount
    };

    /// <summary>
    /// Creates a format validation failure result
    /// </summary>
    public static ApiValidationResult FormatFailure(string errorMessage) => new()
    {
        IsValid = false,
        FormatValid = false,
        NetworkValid = false,
        ErrorMessage = errorMessage,
        Duration = TimeSpan.Zero
    };

    /// <summary>
    /// Creates a network validation failure result
    /// </summary>
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
