using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.ThisWeek.Commands;

/// <summary>
/// Command to create a weekly review entry by aggregating daily entries.
/// </summary>
public sealed record CreateWeeklyReviewCommand : IRequest<Result<WeeklyEntry>>
{
    /// <summary>
    /// Gets the optional custom date range for the weekly review.
    /// If not specified, defaults to the last 7 days.
    /// </summary>
    public DateRange? CustomDateRange { get; init; }

    /// <summary>
    /// Gets the optional LLM provider override (e.g., "OpenAI", "Anthropic").
    /// If not specified, uses the default provider from configuration.
    /// </summary>
    public string? LlmProviderOverride { get; init; }
}

/// <summary>
/// Marker interface for command requests.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequest<TResponse>
{
}

/// <summary>
/// Handler interface for processing requests.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
