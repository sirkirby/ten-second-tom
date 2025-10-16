using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Templates.Models;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates.Handlers;

/// <summary>
/// Handler for listing available templates with optional filtering and sorting.
/// </summary>
public sealed class ListTemplatesQueryHandler : IRequestHandler<ListTemplatesQuery, Result<ListTemplatesQueryResult>>
{
    private readonly IPromptTemplateLoader _templateLoader;
    private readonly ILogger<ListTemplatesQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListTemplatesQueryHandler"/> class.
    /// </summary>
    /// <param name="templateLoader">Template loader for accessing templates.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ListTemplatesQueryHandler(
        IPromptTemplateLoader templateLoader,
        ILogger<ListTemplatesQueryHandler> logger)
    {
        _templateLoader = templateLoader ?? throw new ArgumentNullException(nameof(templateLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the list templates query by loading, validating, and sorting templates.
    /// </summary>
    /// <param name="request">The query with optional type filter and include-invalid flag.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A result containing the list of templates, total count, and invalid count,
    /// or a failure result if templates cannot be loaded.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<Result<ListTemplatesQueryResult>> Handle(
        ListTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Listing templates with filter: {FilterType}, IncludeInvalid: {IncludeInvalid}",
            request.FilterByType,
            request.IncludeInvalid);

        // Load templates based on whether we have a FileSystemTemplateLoader with filter support
        Result<List<PromptTemplate>> loadResult;

        // Try to cast to FileSystemTemplateLoader to use its enhanced LoadAllTemplatesAsync
        if (_templateLoader is FileSystemTemplateLoader fsLoader)
        {
            loadResult = await fsLoader.LoadAllTemplatesAsync(
                request.FilterByType,
                cancellationToken);
        }
        else
        {
            // Fallback for other loaders - load all and filter manually
            loadResult = await _templateLoader.LoadAllTemplatesAsync(cancellationToken);

            if (loadResult.IsSuccess && request.FilterByType.HasValue)
            {
                var filteredTemplates = loadResult.Value
                    .Where(t => t.TemplateType == request.FilterByType.Value)
                    .ToList();
                loadResult = Result<List<PromptTemplate>>.Success(filteredTemplates);
            }
        }

        if (!loadResult.IsSuccess)
        {
            _logger.LogWarning("Failed to load templates: {Error}", loadResult.Error);
            return Result<ListTemplatesQueryResult>.Failure(loadResult.Error ?? "Failed to load templates");
        }

        var templates = loadResult.Value;
        var totalFound = templates.Count;
        var invalidCount = 0;
        var validTemplates = new List<TemplateListItem>();

        // Process each template
        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Validate template if needed
            if (!IsValidTemplate(template))
            {
                invalidCount++;
                _logger.LogDebug("Skipping invalid template: {TemplateId}", template.TemplateId);

                if (!request.IncludeInvalid)
                    continue;
            }

            // Map to TemplateListItem
            var listItem = MapToListItem(template);
            validTemplates.Add(listItem);
        }

        // Sort templates: defaults first, then alphabetically by title (case-insensitive)
        validTemplates.Sort((a, b) =>
        {
            // Default templates come first
            if (a.IsDefault && !b.IsDefault) return -1;
            if (!a.IsDefault && b.IsDefault) return 1;

            // Then sort alphabetically by title (case-insensitive)
            return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        });

        _logger.LogInformation(
            "Listed {Count} templates (Total: {Total}, Invalid: {Invalid})",
            validTemplates.Count,
            totalFound,
            invalidCount);

        return Result<ListTemplatesQueryResult>.Success(new ListTemplatesQueryResult(
            Templates: validTemplates,
            TotalFound: totalFound,
            InvalidCount: invalidCount
        ));
    }

    /// <summary>
    /// Validates a template for basic requirements (ID, content, metadata).
    /// </summary>
    /// <param name="template">The template to validate.</param>
    /// <returns>True if the template passes validation; otherwise false.</returns>
    private static bool IsValidTemplate(PromptTemplate template)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(template.TemplateId))
            return false;

        if (string.IsNullOrWhiteSpace(template.Content))
            return false;

        // If metadata exists, validate it
        if (template.Metadata != null)
        {
            var validationErrors = template.Metadata.Validate();
            if (validationErrors.Count > 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Maps a PromptTemplate to a TemplateListItem for UI display.
    /// Determines title, description, and default status from metadata or template properties.
    /// </summary>
    /// <param name="template">The template to map.</param>
    /// <returns>A TemplateListItem suitable for display in selection UI.</returns>
    private static TemplateListItem MapToListItem(PromptTemplate template)
    {
        // Determine title - use metadata title if available, otherwise template ID
        string title = template.Metadata?.Title ?? template.TemplateId;

        // Determine description - use metadata description if available, otherwise template description or empty
        string description = template.Metadata?.Description
            ?? template.Description
            ?? string.Empty;

        // Check if this is a default template
        bool isDefault = TemplateConstants.IsDefaultTemplate(template.TemplateId);

        return new TemplateListItem
        {
            TemplateId = template.TemplateId,
            Title = title,
            Description = description,
            Source = template.Source ?? TemplateSource.FileSystem,
            IsDefault = isDefault,
            TemplateType = template.TemplateType
        };
    }

}