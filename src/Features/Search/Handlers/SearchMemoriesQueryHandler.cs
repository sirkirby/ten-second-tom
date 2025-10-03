using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Search.Queries;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Search.Handlers;

/// <summary>
/// Marker interface for CQRS request handlers.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
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

/// <summary>
/// Handler for SearchMemoriesQuery that performs text search across memory entries.
/// Implements CQRS pattern with authentication and storage integration.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public sealed class SearchMemoriesQueryHandler : IRequestHandler<SearchMemoriesQuery, Result<IReadOnlyList<MemoryEntry>>>
{
    private readonly IMemoryStorageProvider _storageProvider;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<SearchMemoriesQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchMemoriesQueryHandler"/> class.
    /// </summary>
    /// <param name="storageProvider">The storage provider for memory entries.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="logger">The logger instance.</param>
    public SearchMemoriesQueryHandler(
        IMemoryStorageProvider storageProvider,
        IAuthenticationService authService,
        ILogger<SearchMemoriesQueryHandler> logger)
    {
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the search memories query.
    /// </summary>
    /// <param name="request">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing matching memory entries or error.</returns>
    public async Task<Result<IReadOnlyList<MemoryEntry>>> Handle(
        SearchMemoriesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate query
        var validationResult = ValidateQuery(request.Query);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        // Check authentication
        var isAuthenticated = await _authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!isAuthenticated)
        {
            _logger.LogWarning("Search attempted without authentication");
            return Result<IReadOnlyList<MemoryEntry>>.Failure("Authentication required. Please authenticate first.");
        }

        _logger.LogInformation("Searching memories with query: {Query}, StartDate: {StartDate}, EndDate: {EndDate}",
            request.Query, request.StartDate, request.EndDate);

        // Perform search
        var searchResult = await _storageProvider.SearchEntriesAsync(
            request.Query,
            request.StartDate,
            request.EndDate,
            cancellationToken).ConfigureAwait(false);

        if (!searchResult.IsSuccess)
        {
            _logger.LogError("Search failed: {Error}", searchResult.Error);
            return Result<IReadOnlyList<MemoryEntry>>.Failure(searchResult.Error ?? "Unknown search error");
        }

        _logger.LogInformation("Search completed. Found {Count} entries", searchResult.Value.Count);
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
