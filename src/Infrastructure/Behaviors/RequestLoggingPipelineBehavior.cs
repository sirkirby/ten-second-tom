using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Behaviors;

/// <summary>
/// Pipeline behavior that automatically logs all MediatR requests with timing and result information.
/// Provides consistent logging format and automatic performance tracking.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <remarks>
/// This behavior:
/// - Logs request start at Debug level
/// - Times request execution
/// - Logs completion with duration and success/failure status
/// - Logs errors with full context
/// - Executes as the outermost pipeline behavior (wraps all other behaviors)
///
/// Log output examples:
/// - [DEBUG] Executing request CreateDailyEntryCommand
/// - [INFO] Completed request CreateDailyEntryCommand in 245ms (Success)
/// - [WARNING] Request CreateDailyEntryCommand failed after 120ms: Authentication required
/// </remarks>
public sealed class RequestLoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RequestLoggingPipelineBehavior(ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs request execution with timing and result information.
    /// </summary>
    /// <param name="request">The request being executed.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result from the handler.</returns>
    [SuppressMessage("Reliability", "CA2016:Forward the CancellationToken parameter", Justification = "MediatR RequestHandlerDelegate does not expose a CancellationToken parameter.")]
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Log request start
        _logger.LogDebug("Executing request {RequestName}", requestName);

        // Time the execution
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute the handler (and any inner pipeline behaviors)
            var response = await next().ConfigureAwait(false);

            stopwatch.Stop();

            // Log completion with result status
            LogCompletion(requestName, stopwatch.ElapsedMilliseconds, response);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log exception
            _logger.LogError(
                ex,
                "Request {RequestName} failed after {ElapsedMs}ms with exception: {ExceptionMessage}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw; // Re-throw to preserve stack trace
        }
    }

    /// <summary>
    /// Logs request completion with duration and success/failure status.
    /// </summary>
    private void LogCompletion(string requestName, long elapsedMs, TResponse response)
    {
        // Check if response is a Result type (non-generic or generic)
        bool isSuccess = true;
        string? errorMessage = null;

        if (response is Result result)
        {
            isSuccess = result.IsSuccess;
            errorMessage = result.Error;
        }
        else if (response != null)
        {
            // Check if TResponse is Result<T> using reflection
            var responseType = response.GetType();
            if (responseType.IsGenericType &&
                responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var isSuccessProperty = responseType.GetProperty(nameof(Result.IsSuccess));
                var errorProperty = responseType.GetProperty(nameof(Result.Error));

                if (isSuccessProperty != null)
                {
                    isSuccess = (bool)isSuccessProperty.GetValue(response)!;
                }

                if (errorProperty != null)
                {
                    errorMessage = errorProperty.GetValue(response) as string;
                }
            }
        }

        if (isSuccess)
        {
            _logger.LogInformation(
                "Completed request {RequestName} in {ElapsedMs}ms (Success)",
                requestName,
                elapsedMs);
        }
        else
        {
            _logger.LogWarning(
                "Request {RequestName} failed after {ElapsedMs}ms: {ErrorMessage}",
                requestName,
                elapsedMs,
                errorMessage ?? "Unknown error");
        }
    }
}
