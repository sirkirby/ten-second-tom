using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TenSecondTom.Shared.Results;
using System.Diagnostics.CodeAnalysis;

namespace TenSecondTom.Infrastructure.Behaviors;

/// <summary>
/// Pipeline behavior that automatically validates requests using FluentValidation validators.
/// Intercepts all MediatR requests and runs validation before handler execution.
/// </summary>
/// <typeparam name="TRequest">The request type to validate.</typeparam>
/// <typeparam name="TResponse">The response type (must be Result or Result&lt;T&gt;).</typeparam>
/// <remarks>
/// This behavior:
/// - Automatically discovers and injects validators via assembly scanning
/// - Validates requests before handler execution
/// - Returns Result.ValidationFailure if validation fails
/// - Allows handlers to assume valid input (no validation code needed)
///
/// To add validation for a command/query:
/// 1. Create a validator class inheriting AbstractValidator&lt;TRequest&gt;
/// 2. FluentValidation assembly scanning will auto-discover it
/// 3. ValidationPipelineBehavior will automatically execute it
/// </remarks>
public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="validators">All validators for the request type (injected via assembly scanning).</param>
    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Validates the request before handler execution.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of handler execution or validation failure.</returns>
    [SuppressMessage("Reliability", "CA2016:Forward the CancellationToken parameter", Justification = "MediatR RequestHandlerDelegate does not expose a CancellationToken parameter.")]
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // If no validators registered for this request type, skip validation
        if (!_validators.Any())
        {
            return await next().ConfigureAwait(false);
        }

        // Run all validators
        var validationFailures = await ValidateAsync(request, cancellationToken).ConfigureAwait(false);

        // If validation passed, continue to handler
        if (validationFailures.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        // Validation failed - create appropriate Result failure response
        return CreateValidationFailureResponse(validationFailures);
    }

    /// <summary>
    /// Runs all validators for the request and collects failures.
    /// </summary>
    private async Task<ValidationFailure[]> ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        var validationContext = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(validationContext, cancellationToken)))
            .ConfigureAwait(false);

        return validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToArray();
    }

    /// <summary>
    /// Creates a Result.ValidationFailure response using reflection.
    /// Handles both Result and Result&lt;T&gt; response types.
    /// </summary>
    private TResponse CreateValidationFailureResponse(ValidationFailure[] validationFailures)
    {
        var errors = validationFailures
            .Select(f => $"{f.PropertyName}: {f.ErrorMessage}")
            .ToArray();

        var errorMessage = string.Join("; ", errors);

        // Check if TResponse is Result (non-generic)
        if (typeof(TResponse) == typeof(Result))
        {
            var result = Result.Failure(errorMessage);
            return (TResponse)(object)result;
        }

        // Check if TResponse is Result<T> (generic)
        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(string) });

            if (failureMethod != null)
            {
                var result = failureMethod.Invoke(null, new object[] { errorMessage });
                return (TResponse)result!;
            }
        }

        // Fallback: throw exception if we can't create a failure response
        // This should never happen if all commands/queries return Result or Result<T>
        throw new InvalidOperationException(
            $"ValidationPipelineBehavior cannot create failure response for type {typeof(TResponse).Name}. " +
            $"Ensure all commands/queries return Result or Result<T>.");
    }
}
