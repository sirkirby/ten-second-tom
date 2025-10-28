using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Infrastructure implementation of template provider.
/// Provides template discovery and retrieval without creating feature dependencies.
/// </summary>
public sealed class TemplateProvider : ITemplateProvider
{
    private readonly IPromptTemplateLoader _templateLoader;
    private readonly ILogger<TemplateProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateProvider"/> class.
    /// </summary>
    /// <param name="templateLoader">Template loader for accessing templates.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public TemplateProvider(
        IPromptTemplateLoader templateLoader,
        ILogger<TemplateProvider> logger)
    {
        _templateLoader = templateLoader ?? throw new ArgumentNullException(nameof(templateLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<TemplateInfo>>> ListTemplatesAsync(
        TemplateType? filterByType = null,
        bool includeInvalid = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Listing templates with filter: {FilterType}, IncludeInvalid: {IncludeInvalid}",
            filterByType,
            includeInvalid);

        // Load templates based on whether we have a FileSystemTemplateLoader with filter support
        Result<List<PromptTemplate>> loadResult;

        // Try to cast to FileSystemTemplateLoader to use its enhanced LoadAllTemplatesAsync
        if (_templateLoader is FileSystemTemplateLoader fsLoader)
        {
            loadResult = await fsLoader.LoadAllTemplatesAsync(
                filterByType,
                cancellationToken);
        }
        else
        {
            // Fallback for other loaders - load all and filter manually
            loadResult = await _templateLoader.LoadAllTemplatesAsync(cancellationToken);

            if (loadResult.IsSuccess && filterByType.HasValue)
            {
                var filteredTemplates = loadResult.Value
                    .Where(t => t.TemplateType == filterByType.Value)
                    .ToList();
                loadResult = Result<List<PromptTemplate>>.Success(filteredTemplates);
            }
        }

        if (!loadResult.IsSuccess)
        {
            _logger.LogWarning("Failed to load templates: {Error}", loadResult.Error);
            return Result<IReadOnlyList<TemplateInfo>>.Failure(
                loadResult.Error ?? "Failed to load templates");
        }

        var templates = loadResult.Value;
        var invalidCount = 0;
        var validTemplates = new List<TemplateInfo>();

        // Process each template
        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Validate template if needed
            if (!IsValidTemplate(template))
            {
                invalidCount++;
                _logger.LogDebug("Skipping invalid template: {TemplateId}", template.TemplateId);

                if (!includeInvalid)
                    continue;
            }

            // Map to TemplateInfo
            var templateInfo = MapToTemplateInfo(template);
            validTemplates.Add(templateInfo);
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
            templates.Count,
            invalidCount);

        return Result<IReadOnlyList<TemplateInfo>>.Success(validTemplates.AsReadOnly());
    }

    /// <inheritdoc/>
    public async Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading template: {TemplateId}", templateId);

        var result = await _templateLoader.LoadTemplateAsync(templateId, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogDebug("Successfully loaded template: {TemplateId}", templateId);
        }
        else
        {
            _logger.LogWarning("Failed to load template {TemplateId}: {Error}",
                templateId, result.Error);
        }

        return result;
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
    /// Maps a PromptTemplate to a TemplateInfo for infrastructure-level usage.
    /// Determines title, description, and default status from metadata or template properties.
    /// </summary>
    /// <param name="template">The template to map.</param>
    /// <returns>A TemplateInfo suitable for cross-feature usage.</returns>
    private static TemplateInfo MapToTemplateInfo(PromptTemplate template)
    {
        // Determine title - use metadata title if available, otherwise template ID
        string title = template.Metadata?.Title ?? template.TemplateId;

        // Determine description - use metadata description if available, otherwise template description or empty
        string description = template.Metadata?.Description
            ?? template.Description
            ?? string.Empty;

        // Check if this is a default template
        bool isDefault = TemplateConstants.IsDefaultTemplate(template.TemplateId);

        return new TemplateInfo(
            TemplateId: template.TemplateId,
            Title: title,
            Description: description,
            TemplateType: template.TemplateType,
            Source: template.Source ?? TemplateSource.FileSystem,
            IsDefault: isDefault);
    }
}
