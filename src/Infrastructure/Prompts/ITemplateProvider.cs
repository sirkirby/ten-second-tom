using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Abstraction for template discovery and retrieval.
/// Decouples features from Templates feature implementation.
/// </summary>
/// <remarks>
/// This interface provides infrastructure-level access to template operations
/// without creating direct dependencies on the Templates feature.
/// Features requiring template access should depend on this abstraction
/// rather than directly referencing Templates feature handlers or services.
/// </remarks>
public interface ITemplateProvider
{
    /// <summary>
    /// Lists available templates, optionally filtered by type.
    /// </summary>
    /// <param name="filterByType">Optional filter to return only templates of a specific type (Daily, Weekly, etc.).</param>
    /// <param name="includeInvalid">Whether to include templates that failed validation. Default is false.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A result containing a list of templates that passed validation and match the query filter,
    /// or a failure result if templates cannot be loaded.
    /// </returns>
    /// <remarks>
    /// Templates are sorted with default templates first, then alphabetically by title.
    /// Invalid templates are excluded unless <paramref name="includeInvalid"/> is true.
    /// </remarks>
    Task<Result<IReadOnlyList<TemplateInfo>>> ListTemplatesAsync(
        TemplateType? filterByType = null,
        bool includeInvalid = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific template by its identifier.
    /// </summary>
    /// <param name="templateId">The unique identifier of the template to load.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A result containing the loaded template on success,
    /// or a failure result if the template cannot be loaded.
    /// </returns>
    Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight template information for template selection and display.
/// Infrastructure DTO that avoids exposing Templates feature models.
/// </summary>
/// <param name="TemplateId">The unique identifier for the template.</param>
/// <param name="Title">The display name for the template.</param>
/// <param name="Description">The description of the template. Empty string if no description available.</param>
/// <param name="TemplateType">The type of template (Daily, Weekly, etc.).</param>
/// <param name="Source">Where the template came from (embedded or filesystem).</param>
/// <param name="IsDefault">Whether this is a default template.</param>
public sealed record TemplateInfo(
    string TemplateId,
    string Title,
    string Description,
    TemplateType TemplateType,
    TemplateSource Source,
    bool IsDefault);
