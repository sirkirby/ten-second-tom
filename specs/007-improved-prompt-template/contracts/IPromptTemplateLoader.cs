/// <summary>
/// Contract for loading prompt templates from various sources (embedded resources, filesystem).
/// </summary>
/// <remarks>
/// This interface is enhanced from the existing implementation to support:
/// - Loading all templates of a specific type
/// - Filesystem-based template loading with YAML metadata
/// - Fallback mechanisms for missing templates
/// </remarks>
public interface IPromptTemplateLoader
{
    /// <summary>
    /// Loads a specific prompt template by its identifier.
    /// </summary>
    /// <param name="templateId">The unique identifier of the template (e.g., "daily-summary").</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A Result containing the loaded PromptTemplate on success,
    /// or a failure result with an error message if the template cannot be loaded.
    /// </returns>
    /// <remarks>
    /// Implementation should:
    /// - Check filesystem templates first (if configured)
    /// - Fall back to embedded templates if filesystem load fails
    /// - Parse YAML front matter from template files
    /// - Validate template structure and metadata
    /// - Log warnings for invalid templates
    /// </remarks>
    Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all available templates for a specific template type.
    /// </summary>
    /// <param name="templateType">The type of templates to load (Daily or Weekly).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A Result containing a list of available templates for the specified type.
    /// Returns an empty list if no templates are found (not a failure).
    /// Returns a failure result only if template discovery itself fails.
    /// </returns>
    /// <remarks>
    /// Implementation should:
    /// - Discover all .md files in the templates directory
    /// - Parse YAML front matter to determine template type
    /// - Filter templates by the specified type
    /// - Include both filesystem and embedded templates
    /// - Skip invalid templates (log warnings) but continue with valid ones
    /// - Return templates sorted by: defaults first, then custom alphabetically
    /// </remarks>
    Task<Result<IReadOnlyList<PromptTemplate>>> LoadAllTemplatesAsync(
        TemplateType templateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the templates directory exists and is accessible.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// True if the templates directory exists and is readable; otherwise, false.
    /// </returns>
    /// <remarks>
    /// Used during configuration validation to determine if migration is needed.
    /// Does not throw exceptions - returns false on any error.
    /// </remarks>
    Task<bool> TemplatesDirectoryExistsAsync(CancellationToken cancellationToken = default);
}
