using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Provides functionality to load prompt templates from embedded resources or user overrides.
/// User overrides in .memory/templates/ take precedence over embedded resources.
/// </summary>
public interface IPromptTemplateLoader
{
    /// <summary>
    /// Loads a prompt template by its identifier.
    /// Checks for user overrides in .memory/templates/ before falling back to embedded resources.
    /// </summary>
    /// <param name="templateId">The unique identifier of the template to load (e.g., "daily-summary").</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the loaded <see cref="PromptTemplate"/> on success,
    /// or a failure result with an error message if the template cannot be loaded.
    /// </returns>
    Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all available prompt templates.
    /// For embedded loaders, returns all embedded templates.
    /// For filesystem loaders, returns all templates from the templates directory.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing a list of all templates on success,
    /// or a failure result with an error message if templates cannot be loaded.
    /// </returns>
    /// <remarks>
    /// The specific behavior depends on the implementation:
    /// - <see cref="EmbeddedPromptTemplateLoader"/>: Returns embedded resources only
    /// - <see cref="FileSystemTemplateLoader"/>: Returns filesystem templates with optional filtering
    /// </remarks>
    Task<Result<List<PromptTemplate>>> LoadAllTemplatesAsync(
        CancellationToken cancellationToken = default);
}
