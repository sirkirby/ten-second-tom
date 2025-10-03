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

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedPromptTemplateLoader"/> class.
    /// </summary>
    /// <param name="baseDirectory">
    /// The base directory to search for user template overrides.
    /// If null, uses the default .memory directory from environment.
    /// </param>
    public EmbeddedPromptTemplateLoader(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory;
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

    private static async Task<Result<PromptTemplate>> LoadEmbeddedTemplateAsync(
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
        string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        TemplateType templateType = DetermineTemplateType(templateId);

        PromptTemplate template = new()
        {
            TemplateId = templateId,
            Content = content,
            TemplateType = templateType,
            Description = $"Embedded template: {templateId}"
        };

        return Result<PromptTemplate>.Success(template);
    }

    private static TemplateType DetermineTemplateType(string templateId)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase - template IDs are conventionally lowercase
        return templateId.ToLowerInvariant() switch
#pragma warning restore CA1308
        {
            "daily-summary" => TemplateType.DailySummary,
            "weekly-review" => TemplateType.WeeklySummary,
            _ => TemplateType.SystemPrompt
        };
    }
}
