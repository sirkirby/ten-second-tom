using System.Reflection;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Loads prompt templates from embedded resources with support for user overrides.
/// User overrides in {baseDirectory}/templates/ take precedence over embedded resources.
/// </summary>
public sealed class EmbeddedPromptTemplateLoader : IPromptTemplateLoader
{
    private const string EmbeddedResourcePrefix = "TenSecondTom.Infrastructure.Prompts.Templates";
    private readonly string? _baseDirectory;
    private readonly YamlFrontMatterParser _yamlParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedPromptTemplateLoader"/> class.
    /// </summary>
    /// <param name="baseDirectory">
    /// The base directory to search for user template overrides.
    /// If null, uses the default .memory directory from environment.
    /// </param>
    /// <param name="yamlParser">
    /// The YAML parser to use for extracting metadata from templates.
    /// </param>
    public EmbeddedPromptTemplateLoader(string? baseDirectory = null, YamlFrontMatterParser? yamlParser = null)
    {
        _baseDirectory = baseDirectory;
        _yamlParser = yamlParser ?? throw new ArgumentNullException(nameof(yamlParser));
    }

    /// <inheritdoc />
    public async Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        // Validate template ID
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Result<PromptTemplate>.Failure("Template ID cannot be null or empty");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // First, check for user override
            Result<PromptTemplate>? userOverride = await TryLoadUserOverrideAsync(
                templateId,
                cancellationToken).ConfigureAwait(false);

            if (userOverride is not null)
            {
                return userOverride.Value;
            }

            // Fallback to embedded resource
            return await LoadEmbeddedTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types - we want to handle all exceptions gracefully
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Result<PromptTemplate>.Failure(
                $"Failed to load template '{templateId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to load a user override template from the filesystem.
    /// User overrides take precedence over embedded resources.
    /// </summary>
    /// <param name="templateId">The template ID to load.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A successful result containing the user override template if found,
    /// or null if no override exists or loading fails (to trigger fallback to embedded).
    /// </returns>
    private async Task<Result<PromptTemplate>?> TryLoadUserOverrideAsync(
        string templateId,
        CancellationToken cancellationToken)
    {
        if (_baseDirectory is null)
        {
            return null;
        }

        try
        {
            string templatePath = Path.Combine(_baseDirectory, "templates", $"{templateId}.md");

            if (!File.Exists(templatePath))
            {
                return null;
            }

            string content = await File.ReadAllTextAsync(templatePath, cancellationToken)
                .ConfigureAwait(false);

            TemplateType templateType = DetermineTemplateType(templateId);

            PromptTemplate template = new()
            {
                TemplateId = templateId,
                Content = content,
                TemplateType = templateType,
                Description = $"User override template from {templatePath}"
            };

            return Result<PromptTemplate>.Success(template);
        }
#pragma warning disable CA1031 // Do not catch general exception types - intentional fallback behavior
        catch (Exception)
#pragma warning restore CA1031
        {
            // If user override fails to load, fall back to embedded resource
            return null;
        }
    }

    /// <summary>
    /// Loads a template from embedded assembly resources.
    /// Parses YAML front matter if present to extract metadata.
    /// </summary>
    /// <param name="templateId">The template ID to load.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A result containing the embedded template,
    /// or a failure result if the template is not found in embedded resources.
    /// </returns>
    private async Task<Result<PromptTemplate>> LoadEmbeddedTemplateAsync(
        string templateId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Assembly assembly = typeof(EmbeddedPromptTemplateLoader).Assembly;
        string resourceName = $"{EmbeddedResourcePrefix}.{templateId}.md";

        using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);

        if (resourceStream is null)
        {
            return Result<PromptTemplate>.Failure(
                $"Template '{templateId}' not found in embedded resources");
        }

        using StreamReader reader = new(resourceStream);
        string rawContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        // Parse YAML front matter if present
        var parseResult = _yamlParser.Parse(rawContent);
        string content;
        TemplateMetadata? metadata = null;

        if (parseResult.IsSuccess)
        {
            var parsed = parseResult.Value;
            content = parsed.Content;
            metadata = parsed.Metadata;
        }
        else
        {
            // No YAML front matter or parse failed - use raw content
            content = rawContent;
        }

        TemplateType templateType = metadata?.TemplateType ?? DetermineTemplateType(templateId);

        PromptTemplate template = new()
        {
            TemplateId = templateId,
            Content = content,
            TemplateType = templateType,
            Description = metadata?.Description ?? $"Embedded template: {templateId}",
            Source = TemplateSource.Embedded,
            Metadata = metadata
        };

        return Result<PromptTemplate>.Success(template);
    }

    /// <inheritdoc />
    public async Task<Result<List<PromptTemplate>>> LoadAllTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            List<PromptTemplate> templates = new();
            Assembly assembly = typeof(EmbeddedPromptTemplateLoader).Assembly;

            // Get all embedded template resources
            string[] resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(EmbeddedResourcePrefix, StringComparison.Ordinal) &&
                              name.EndsWith(".md", StringComparison.Ordinal))
                .ToArray();

            foreach (string resourceName in resourceNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract template ID from resource name
                // Format: TenSecondTom.Infrastructure.Prompts.Templates.{templateId}.md
                string templateId = resourceName
                    .Replace($"{EmbeddedResourcePrefix}.", string.Empty, StringComparison.Ordinal)
                    .Replace(".md", string.Empty, StringComparison.Ordinal);

                using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream is null)
                {
                    continue; // Skip if resource can't be loaded
                }

                using StreamReader reader = new(resourceStream);
                string rawContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                // Parse YAML front matter if present
                var parseResult = _yamlParser.Parse(rawContent);
                string content;
                TemplateMetadata? metadata = null;

                if (parseResult.IsSuccess)
                {
                    var parsed = parseResult.Value;
                    content = parsed.Content;
                    metadata = parsed.Metadata;
                }
                else
                {
                    // No YAML front matter or parse failed - use raw content
                    content = rawContent;
                }

                TemplateType templateType = metadata?.TemplateType ?? DetermineTemplateType(templateId);

                templates.Add(new PromptTemplate
                {
                    TemplateId = templateId,
                    Content = content,
                    TemplateType = templateType,
                    Description = metadata?.Description ?? $"Embedded template: {templateId}",
                    Source = TemplateSource.Embedded,
                    Metadata = metadata
                });
            }

            return Result<List<PromptTemplate>>.Success(templates);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types - we want to handle all exceptions gracefully
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Result<List<PromptTemplate>>.Failure(
                $"Failed to load embedded templates: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string>> LoadRawTemplateContentAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Result<string>.Failure("Template ID cannot be null or empty");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // First, check for user override
            Result<string>? userOverrideResult = await TryLoadRawUserOverrideAsync(
                templateId,
                cancellationToken).ConfigureAwait(false);

            if (userOverrideResult is not null)
            {
                return userOverrideResult.Value;
            }

            // Fallback to embedded resource
            return await LoadRawEmbeddedTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types - we want to handle all exceptions gracefully
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Result<string>.Failure(
                $"Failed to load raw template content for '{templateId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to load raw user override template content from the filesystem.
    /// </summary>
    /// <param name="templateId">The template ID to load.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A successful result containing the raw template content if found,
    /// or null if no override exists or loading fails (to trigger fallback to embedded).
    /// </returns>
    private async Task<Result<string>?> TryLoadRawUserOverrideAsync(
        string templateId,
        CancellationToken cancellationToken)
    {
        if (_baseDirectory is null)
        {
            return null;
        }

        try
        {
            string templatePath = Path.Combine(_baseDirectory, "templates", $"{templateId}.md");

            if (!File.Exists(templatePath))
            {
                return null;
            }

            string rawContent = await File.ReadAllTextAsync(templatePath, cancellationToken)
                .ConfigureAwait(false);

            return Result<string>.Success(rawContent);
        }
#pragma warning disable CA1031 // Do not catch general exception types - intentional fallback behavior
        catch (Exception)
#pragma warning restore CA1031
        {
            // If user override fails to load, fall back to embedded resource
            return null;
        }
    }

    /// <summary>
    /// Loads raw template content from embedded assembly resources.
    /// Returns the full file content including YAML front matter.
    /// </summary>
    /// <param name="templateId">The template ID to load.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A result containing the raw embedded template content (with YAML front matter),
    /// or a failure result if the template is not found in embedded resources.
    /// </returns>
    private static async Task<Result<string>> LoadRawEmbeddedTemplateAsync(
        string templateId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Assembly assembly = typeof(EmbeddedPromptTemplateLoader).Assembly;
        string resourceName = $"{EmbeddedResourcePrefix}.{templateId}.md";

        using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);

        if (resourceStream is null)
        {
            return Result<string>.Failure(
                $"Template '{templateId}' not found in embedded resources");
        }

        using StreamReader reader = new(resourceStream);
        string rawContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        return Result<string>.Success(rawContent);
    }

    /// <summary>
    /// Determines the template type from a template ID.
    /// Maps known template IDs to their corresponding types.
    /// </summary>
    /// <param name="templateId">The template ID to analyze.</param>
    /// <returns>
    /// The template type: Daily for "daily-summary", Weekly for "weekly-review",
    /// BusinessMeeting for "business-meeting", or SystemPrompt for all other templates.
    /// </returns>
    private static TemplateType DetermineTemplateType(string templateId)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase - template IDs are conventionally lowercase
        return templateId.ToLowerInvariant() switch
#pragma warning restore CA1308
        {
            "daily-summary" => TemplateType.Daily,
            "weekly-review" => TemplateType.Weekly,
            "business-meeting" => TemplateType.BusinessMeeting,
            _ => TemplateType.SystemPrompt
        };
    }
}
