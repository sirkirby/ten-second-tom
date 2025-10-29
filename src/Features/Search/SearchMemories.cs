using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Storage;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Search;

/// <summary>
/// Search memory entries by text query with optional date range filters.
/// Implements CQRS pattern for read operations.
/// </summary>
public static class SearchMemories
{
    /// <summary>
    /// Query to search memory entries.
    /// </summary>
    /// <param name="SearchQuery">The search query text to match against entries.</param>
    /// <param name="StartDate">Optional start date filter (inclusive).</param>
    /// <param name="EndDate">Optional end date filter (inclusive).</param>
    public sealed record Query(
        string SearchQuery,
        DateTime? StartDate = null,
        DateTime? EndDate = null) : IRequest<Result<IReadOnlyList<MemoryEntry>>>;

    /// <summary>
    /// Handler for SearchMemories that performs text search across memory entries.
    /// Implements CQRS pattern with authentication and storage integration.
    /// Auto-discovered by MediatR assembly scanning.
    /// </summary>
    public sealed class Handler(
        IMemoryStorageProvider storageProvider,
        IAuthenticationService authService,
        ILogger<Handler> logger) : IRequestHandler<Query, Result<IReadOnlyList<MemoryEntry>>>
    {
        /// <summary>
        /// Handles the search memories query.
        /// </summary>
        /// <param name="request">The search query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result containing matching memory entries or error.</returns>
        public async Task<Result<IReadOnlyList<MemoryEntry>>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Validate query
            var validationResult = ValidateQuery(request.SearchQuery);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // Check authentication
            var isAuthenticated = await authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
            if (!isAuthenticated)
            {
                logger.LogWarning("Search attempted without authentication");
                return Result<IReadOnlyList<MemoryEntry>>.Failure("Authentication required. Please authenticate first.");
            }

            logger.LogInformation("Searching memories with query: {Query}, StartDate: {StartDate}, EndDate: {EndDate}",
                request.SearchQuery, request.StartDate, request.EndDate);

            // Perform search
            var searchResult = await storageProvider.SearchEntriesAsync(
                request.SearchQuery,
                request.StartDate,
                request.EndDate,
                cancellationToken).ConfigureAwait(false);

            if (!searchResult.IsSuccess)
            {
                logger.LogError("Search failed: {Error}", searchResult.Error);
                return Result<IReadOnlyList<MemoryEntry>>.Failure(searchResult.Error ?? "Unknown search error");
            }

            logger.LogInformation("Search completed. Found {Count} entries", searchResult.Value.Count);
            return searchResult;
        }

        /// <summary>
        /// Validates the search query.
        /// </summary>
        /// <param name="query">The query to validate.</param>
        /// <returns>Success if valid, failure with error message otherwise.</returns>
        private static Result<IReadOnlyList<MemoryEntry>> ValidateQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Result<IReadOnlyList<MemoryEntry>>.Failure("Query cannot be empty or whitespace.");
            }

            return Result<IReadOnlyList<MemoryEntry>>.Success(new List<MemoryEntry>());
        }
    }
}
