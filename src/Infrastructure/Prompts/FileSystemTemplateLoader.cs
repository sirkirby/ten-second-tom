using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Loads prompt templates from the filesystem with YAML front matter support.
/// </summary>
public sealed class FileSystemTemplateLoader : IPromptTemplateLoader
{
    private const int RetryDelayMs = 100;

    private readonly string _templatesDirectory;
    private readonly YamlFrontMatterParser _parser;
    private readonly ILogger<FileSystemTemplateLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemTemplateLoader"/> class.
    /// </summary>
    /// <param name="templatesDirectory">Directory path where templates are stored.</param>
    /// <param name="parser">YAML front matter parser.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public FileSystemTemplateLoader(
        string templatesDirectory,
        YamlFrontMatterParser parser,
        ILogger<FileSystemTemplateLoader> logger)
    {
        _templatesDirectory = templatesDirectory ?? throw new ArgumentNullException(nameof(templatesDirectory));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Result<PromptTemplate>.Failure("Invalid template ID: cannot be null or empty");
        }

        // Prevent path traversal attacks
        if (templateId.Contains('/') || templateId.Contains('\\') || templateId.Contains(".."))
        {
            return Result<PromptTemplate>.Failure("Invalid template ID: contains invalid characters");
        }

        var filePath = Path.Combine(_templatesDirectory, $"{templateId}.md");

        // Security: Verify resolved path stays within templates directory (prevents path traversal)
        var fullTemplatePath = Path.GetFullPath(filePath);
        var fullTemplatesDirectory = Path.GetFullPath(_templatesDirectory);
        if (!fullTemplatePath.StartsWith(fullTemplatesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Path traversal attempt detected: template path {TemplatePath} is outside templates directory {TemplatesDir}",
                fullTemplatePath,
                fullTemplatesDirectory);
            return Result<PromptTemplate>.Failure("Invalid template ID: path traversal detected");
        }

        _logger.LogDebug("Loading template from path: {FilePath}", filePath);

        // Check if file exists
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Template file not found: {FilePath}", filePath);
            return Result<PromptTemplate>.Failure($"Template not found: {templateId}");
        }

        // Check file size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > TemplateConstants.MaxFileSizeBytes)
        {
            _logger.LogWarning("Template file exceeds size limit: {Size} bytes > {MaxSize} bytes",
                fileInfo.Length, TemplateConstants.MaxFileSizeBytes);
            return Result<PromptTemplate>.Failure(
                $"Template file exceeds size limit of {TemplateConstants.MaxFileSizeBytes / 1_048_576}MB");
        }

        // Read file with retry logic for concurrent access
        string content;
        try
        {
            content = await ReadFileWithRetryAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read template file: {FilePath}", filePath);
            return Result<PromptTemplate>.Failure($"Failed to read template: {ex.Message}");
        }

        // Parse YAML front matter
        var parseResult = _parser.Parse(content);
        if (!parseResult.IsSuccess)
        {
            _logger.LogWarning("Failed to parse YAML for template {TemplateId}: {Error}",
                templateId, parseResult.Error);
            return Result<PromptTemplate>.Failure($"Invalid YAML in template: {parseResult.Error}");
        }

        var parsed = parseResult.Value;

        // Validate content is not empty
        if (string.IsNullOrWhiteSpace(parsed.Content))
        {
            _logger.LogWarning("Template {TemplateId} has empty content after YAML parsing", templateId);
            return Result<PromptTemplate>.Failure("Template content is empty after parsing");
        }

        // Validate metadata if present
        if (parsed.Metadata != null)
        {
            var validationErrors = parsed.Metadata.Validate();
            if (validationErrors.Count > 0)
            {
                var errorMessage = string.Join("; ", validationErrors);
                _logger.LogWarning("Template {TemplateId} failed validation: {Errors}",
                    templateId, errorMessage);
                return Result<PromptTemplate>.Failure($"Invalid template metadata: {errorMessage}");
            }
        }
        else
        {
            // Metadata is required for Id
            _logger.LogWarning("Template {TemplateId} is missing metadata (YAML front matter)", templateId);
            return Result<PromptTemplate>.Failure("Template is missing metadata (YAML front matter)");
        }

        // Use Id from metadata, not filename
        var id = parsed.Metadata.Id!;

        // Determine if this is a custom template (not a default)
        var isCustomTemplate = !TemplateConstants.IsDefaultTemplate(id);

        // Create PromptTemplate
        var template = new PromptTemplate
        {
            TemplateId = id,
            Content = parsed.Content,
            TemplateType = parsed.Metadata.TemplateType,
            Description = parsed.Metadata.Description,
            Source = TemplateSource.FileSystem,
            Metadata = parsed.Metadata
        };

        // Log success with appropriate level and context
        if (isCustomTemplate)
        {
            _logger.LogInformation(
                "Successfully loaded custom template: {TemplateId} (Type: {Type}, Title: {Title})",
                templateId,
                template.TemplateType,
                template.Metadata?.Title ?? templateId);
        }
        else
        {
            _logger.LogDebug("Successfully loaded default template: {TemplateId} (Type: {Type})",
                templateId, template.TemplateType);
        }

        return Result<PromptTemplate>.Success(template);
    }

    /// <inheritdoc/>
    public async Task<Result<List<PromptTemplate>>> LoadAllTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await LoadAllTemplatesAsync(null, cancellationToken);
    }

    /// <summary>
    /// Loads all templates from the filesystem, optionally filtering by type.
    /// Validates each template and logs custom template discoveries.
    /// </summary>
    /// <param name="filterByType">
    /// Optional filter to return only templates of a specific type.
    /// If null, returns all template types found.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A result containing a list of valid templates that match the filter criteria,
    /// or an empty list if the templates directory doesn't exist.
    /// </returns>
    /// <remarks>
    /// This method:
    /// - Scans the templates directory for .md files
    /// - Validates each template and skips invalid ones
    /// - Logs custom template discoveries at appropriate levels
    /// - Sorts templates with defaults first, then alphabetically
    /// - Returns detailed statistics about loaded templates
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    public async Task<Result<List<PromptTemplate>>> LoadAllTemplatesAsync(
        TemplateType? filterByType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading all templates from directory: {Directory}", _templatesDirectory);

        if (!Directory.Exists(_templatesDirectory))
        {
            _logger.LogWarning("Templates directory does not exist: {Directory}", _templatesDirectory);
            return Result<List<PromptTemplate>>.Success([]);
        }

        var templateFiles = Directory.GetFiles(_templatesDirectory, "*.md", SearchOption.TopDirectoryOnly);

        if (templateFiles.Length == 0)
        {
            _logger.LogInformation("No template files found in directory");
            return Result<List<PromptTemplate>>.Success([]);
        }

        var templates = new List<PromptTemplate>();
        var invalidCount = 0;
        var customTemplatesDiscovered = 0;
        var defaultTemplatesFound = 0;

        foreach (var filePath in templateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var templateId = Path.GetFileNameWithoutExtension(filePath);
            var isCustomTemplate = !TemplateConstants.IsDefaultTemplate(templateId);

            // Log discovery of new custom templates
            if (isCustomTemplate)
            {
                _logger.LogDebug("Discovered custom template: {TemplateId} at {FilePath}",
                    templateId, filePath);
            }

            var result = await LoadTemplateAsync(templateId, cancellationToken);

            if (result.IsSuccess)
            {
                var template = result.Value;

                // Apply filter if specified
                if (!filterByType.HasValue || template.TemplateType == filterByType.Value)
                {
                    templates.Add(template);

                    if (isCustomTemplate)
                    {
                        customTemplatesDiscovered++;
                    }
                    else
                    {
                        defaultTemplatesFound++;
                    }
                }
            }
            else
            {
                invalidCount++;

                // Log detailed failure reason for custom templates
                if (isCustomTemplate)
                {
                    _logger.LogWarning(
                        "Custom template {TemplateId} failed validation and will be skipped. Reason: {Error}",
                        templateId,
                        result.Error);
                }
                else
                {
                    _logger.LogWarning("Skipping invalid template {TemplateId}: {Error}",
                        templateId, result.Error);
                }
            }
        }

        // Sort templates: defaults first (daily-summary, weekly-review), then alphabetical
        templates.Sort((a, b) =>
        {
            var aIsDefault = TemplateConstants.IsDefaultTemplate(a.TemplateId);
            var bIsDefault = TemplateConstants.IsDefaultTemplate(b.TemplateId);

            if (aIsDefault && !bIsDefault) return -1;
            if (!aIsDefault && bIsDefault) return 1;

            return string.Compare(a.TemplateId, b.TemplateId, StringComparison.OrdinalIgnoreCase);
        });

        // Log summary with breakdown of template types
        if (customTemplatesDiscovered > 0)
        {
            _logger.LogInformation(
                "Loaded {Count} templates: {DefaultCount} default, {CustomCount} custom (skipped {InvalidCount} invalid)",
                templates.Count,
                defaultTemplatesFound,
                customTemplatesDiscovered,
                invalidCount);
        }
        else
        {
            _logger.LogInformation("Loaded {Count} templates (skipped {InvalidCount} invalid)",
                templates.Count, invalidCount);
        }

        return Result<List<PromptTemplate>>.Success(templates);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> LoadRawTemplateContentAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Result<string>.Failure("Invalid template ID: cannot be null or empty");
        }

        // Prevent path traversal attacks
        if (templateId.Contains('/') || templateId.Contains('\\') || templateId.Contains(".."))
        {
            return Result<string>.Failure("Invalid template ID: contains invalid characters");
        }

        var filePath = Path.Combine(_templatesDirectory, $"{templateId}.md");

        // Security: Verify resolved path stays within templates directory (prevents path traversal)
        var fullTemplatePath = Path.GetFullPath(filePath);
        var fullTemplatesDirectory = Path.GetFullPath(_templatesDirectory);
        if (!fullTemplatePath.StartsWith(fullTemplatesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Path traversal attempt detected: template path {TemplatePath} is outside templates directory {TemplatesDir}",
                fullTemplatePath,
                fullTemplatesDirectory);
            return Result<string>.Failure("Invalid template ID: path traversal detected");
        }

        // Check if file exists
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Template file not found: {FilePath}", filePath);
            return Result<string>.Failure($"Template not found: {templateId}");
        }

        // Check file size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > TemplateConstants.MaxFileSizeBytes)
        {
            _logger.LogWarning("Template file exceeds size limit: {Size} bytes > {MaxSize} bytes",
                fileInfo.Length, TemplateConstants.MaxFileSizeBytes);
            return Result<string>.Failure(
                $"Template file exceeds size limit of {TemplateConstants.MaxFileSizeBytes / 1_048_576}MB");
        }

        // Read raw file content with retry logic for concurrent access
        try
        {
            string rawContent = await ReadFileWithRetryAsync(filePath, cancellationToken);
            return Result<string>.Success(rawContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read template file: {FilePath}", filePath);
            return Result<string>.Failure($"Failed to read template: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads a file with retry logic to handle concurrent access issues.
    /// </summary>
    /// <param name="filePath">The path to the file to read.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The file contents as a string.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be read after retry.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    private async Task<string> ReadFileWithRetryAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            // First attempt with FileShare.Read for concurrent access
            return await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "First read attempt failed, retrying after {Delay}ms", RetryDelayMs);

            // Wait and retry once
            await Task.Delay(RetryDelayMs, cancellationToken);

            try
            {
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            catch (IOException retryEx)
            {
                _logger.LogError(retryEx, "Retry failed for file: {FilePath}", filePath);
                throw;
            }
        }
    }

}