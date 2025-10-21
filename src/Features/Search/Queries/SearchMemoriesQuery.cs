using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Search.Queries;

/// <summary>
/// Query to search memory entries by text query with optional date range filters.
/// Implements CQRS pattern for read operations.
/// </summary>
/// <param name="Query">The search query text to match against entries.</param>
/// <param name="StartDate">Optional start date filter (inclusive).</param>
/// <param name="EndDate">Optional end date filter (inclusive).</param>
public sealed record SearchMemoriesQuery(
    string Query,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<Result<IReadOnlyList<MemoryEntry>>>;
