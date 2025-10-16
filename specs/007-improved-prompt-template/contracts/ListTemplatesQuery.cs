/// <summary>
/// Query to retrieve all available templates for a specific type.
/// </summary>
/// <remarks>
/// Used by:
/// - Template selection UI (to show user available templates)
/// - Validation (to ensure at least one template exists)
/// - Tests (to verify template discovery)
///
/// Returns templates sorted by:
/// 1. Default templates first (from embedded resources)
/// 2. Custom templates alphabetically by title
/// </remarks>
/// <param name="TemplateType">
/// The type of templates to retrieve (Daily or Weekly).
/// Only templates matching this type will be returned.
/// </param>
/// <param name="IncludeInvalid">
/// If true, includes invalid templates in results with error information.
/// If false (default), skips invalid templates silently (warnings logged).
/// Default: false
/// </param>
public sealed record ListTemplatesQuery(
    TemplateType TemplateType,
    bool IncludeInvalid = false
) : IRequest<Result<ListTemplatesQueryResult>>;

/// <summary>
/// Result of listing templates.
/// </summary>
/// <param name="Templates">
/// List of available templates, sorted appropriately for display.
/// Empty list if no templates found (not an error condition).
/// </param>
/// <param name="TotalFound">
/// Total number of template files found (including invalid ones).
/// </param>
/// <param name="InvalidCount">
/// Number of templates that failed validation and were skipped.
/// </param>
public sealed record ListTemplatesQueryResult(
    IReadOnlyList<TemplateListItem> Templates,
    int TotalFound,
    int InvalidCount
);
