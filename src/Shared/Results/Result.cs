using System.Diagnostics.CodeAnalysis;

namespace TenSecondTom.Shared.Results;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error message.
/// This type encourages explicit error handling instead of exceptions for expected failure cases.
/// </summary>
/// <typeparam name="T">The type of the value in case of success.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
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
    /// Gets the value of a successful result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the value of a failed result.</exception>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Cannot access Value of a failed result. Error: {_error}");
            }

            return _value!;
        }
    }

    /// <summary>
    /// Gets the error message of a failed result, or null for successful results.
    /// </summary>
    public string? Error => _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error message cannot be null or whitespace.", nameof(error));
        }

        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The value of the successful operation.</param>
    /// <returns>A successful result containing the value.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory pattern for Result type")]
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A failed result containing the error message.</returns>
    /// <exception cref="ArgumentException">Thrown when error is null or whitespace.</exception>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory pattern for Result type")]
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
    /// Implicit conversion from a value to a successful result.
    /// </summary>
    /// <param name="value">The value to wrap in a result.</param>
    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "Success method provides the alternate")]
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Matches the result to one of two functions based on success or failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the result after matching.</typeparam>
    /// <param name="onSuccess">Function to execute if the result is successful.</param>
    /// <param name="onFailure">Function to execute if the result is a failure.</param>
    /// <returns>The result of the executed function.</returns>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string showing whether the result is success or failure and its content.</returns>
    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {_value}"
            : $"Failure: {_error}";
    }

    /// <inheritdoc/>
    public bool Equals(Result<T> other)
    {
        if (IsSuccess != other.IsSuccess)
        {
            return false;
        }

        return IsSuccess
            ? EqualityComparer<T>.Default.Equals(_value, other._value)
            : _error == other._error;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Result<T> other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return IsSuccess
            ? HashCode.Combine(IsSuccess, _value)
            : HashCode.Combine(IsSuccess, _error);
    }

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    public static bool operator ==(Result<T> left, Result<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two results are not equal.
    /// </summary>
    public static bool operator !=(Result<T> left, Result<T> right)
    {
        return !left.Equals(right);
    }
}
