using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates.Queries;

/// <summary>
/// Query to retrieve available templates with optional filtering by type.
/// Returns sorted list suitable for display in template selection UI.
/// </summary>
/// <param name="FilterByType">
/// Optional filter to return only templates of a specific type (Daily, Weekly, etc.).
/// If null, returns all template types.
/// </param>
/// <param name="IncludeInvalid">
/// Whether to include templates that failed validation.
/// Default is false to exclude invalid templates from the list.
/// </param>
/// <remarks>
/// Templates are sorted with default templates first, then alphabetically by title.
/// Invalid templates are counted separately in the result.
/// </remarks>
public sealed record ListTemplatesQuery(
    TemplateType? FilterByType = null,
    bool IncludeInvalid = false
) : IRequest<Result<ListTemplatesQueryResult>>;

/// <summary>
/// Result of listing templates query.
/// Contains the list of valid templates, total count found, and count of invalid templates.
/// </summary>
/// <param name="Templates">
/// List of templates that passed validation and match the query filter.
/// Sorted with default templates first, then alphabetically by title.
/// </param>
/// <param name="TotalFound">
/// Total number of templates discovered (valid + invalid), before filtering.
/// </param>
/// <param name="InvalidCount">
/// Number of templates that failed validation.
/// These templates are excluded from the Templates list unless IncludeInvalid was true.
/// </param>
public sealed record ListTemplatesQueryResult(
    List<TemplateListItem> Templates,
    int TotalFound,
    int InvalidCount
);
