using System.Diagnostics.CodeAnalysis;

namespace TenSecondTom.Shared.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail with an error message.
/// This type is used for operations that don't return a value (void operations).
/// For operations that return values, use <see cref="Result{T}"/>.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly string? _error;

    /// <summary>
    /// Gets a value indicating whether the result represents a successful operation.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the result represents a failed operation.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message of a failed result, or null for successful results.
    /// </summary>
    public string? Error => _error;

    private Result(bool isSuccess, string? error)
    {
        if (!isSuccess && string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error message cannot be null or whitespace for failed results.", nameof(error));
        }

        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A failed result containing the error message.</returns>
    /// <exception cref="ArgumentException">Thrown when error is null or whitespace.</exception>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Matches the result to one of two functions based on success or failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after matching.</typeparam>
    /// <param name="onSuccess">Function to execute if the result is successful.</param>
    /// <param name="onFailure">Function to execute if the result is a failure.</param>
    /// <returns>The result of the executed function.</returns>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess() : onFailure(_error!);
    }

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string showing whether the result is success or failure.</returns>
    public override string ToString()
    {
        return IsSuccess
            ? "Success"
            : $"Failure: {_error}";
    }

    /// <inheritdoc/>
    public bool Equals(Result other)
    {
        if (IsSuccess != other.IsSuccess)
        {
            return false;
        }

        return IsSuccess || _error == other._error;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Result other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return IsSuccess
            ? HashCode.Combine(IsSuccess)
            : HashCode.Combine(IsSuccess, _error);
    }

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    public static bool operator ==(Result left, Result right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two results are not equal.
    /// </summary>
    public static bool operator !=(Result left, Result right)
    {
        return !left.Equals(right);
    }
}
