using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates.Services;

/// <summary>
/// Validates prompt templates for structure, content, and business rules.
/// Ensures custom templates meet requirements for filename format, metadata, and content.
/// </summary>
public sealed partial class TemplateValidator(ILogger<TemplateValidator> logger)
{
    private readonly ILogger<TemplateValidator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Validates a template filename for custom templates.
    /// Ensures kebab-case format with no path separators or invalid characters.
    /// </summary>
    /// <param name="filename">The filename to validate (without .md extension).</param>
    /// <returns>
    /// A <see cref="Result{T}"/> with bool indicating success or failure with validation error message.
    /// </returns>
    public Result<bool> ValidateFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return Result<bool>.Failure("Filename cannot be null or empty");
        }

        // Check for path separators
        if (filename.Contains('/') || filename.Contains('\\'))
        {
            _logger.LogWarning("Invalid filename contains path separators: {Filename}", filename);
            return Result<bool>.Failure("Filename cannot contain path separators (/ or \\)");
        }

        // Check for parent directory references
        if (filename.Contains(".."))
        {
            _logger.LogWarning("Invalid filename contains parent directory reference: {Filename}", filename);
            return Result<bool>.Failure("Filename cannot contain '..' (parent directory reference)");
        }

        // Check length
        if (filename.Length > TemplateConstants.MaxFilenameLength)
        {
            _logger.LogWarning("Filename too long: {Length} characters", filename.Length);
            return Result<bool>.Failure($"Filename must be {TemplateConstants.MaxFilenameLength} characters or less");
        }

        // Validate kebab-case pattern (lowercase letters, numbers, hyphens)
        if (!KebabCaseRegex().IsMatch(filename))
        {
            _logger.LogWarning("Filename does not match kebab-case pattern: {Filename}", filename);
            return Result<bool>.Failure("Filename must be kebab-case (lowercase letters, numbers, and hyphens only)");
        }

        // Check for reserved names
        if (IsReservedFilename(filename))
        {
            _logger.LogWarning("Filename uses reserved name: {Filename}", filename);
            return Result<bool>.Failure($"Filename '{filename}' is reserved for system use");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validates template content for size, encoding, and structure.
    /// </summary>
    /// <param name="content">The template content to validate.</param>
    /// <param name="templateId">The template identifier for logging.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> with bool indicating success or failure with validation error message.
    /// </returns>
    public Result<bool> ValidateContent(string content, string templateId)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        // Check content is not empty
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Template {TemplateId} has empty content", templateId);
            return Result<bool>.Failure("Template content cannot be empty");
        }

        // Check content size
        var contentBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        if (contentBytes > TemplateConstants.MaxContentLength)
        {
            _logger.LogWarning(
                "Template {TemplateId} exceeds size limit: {Size} bytes > {MaxSize} bytes",
                templateId,
                contentBytes,
                TemplateConstants.MaxContentLength);
            return Result<bool>.Failure($"Template content exceeds size limit of {TemplateConstants.MaxContentLength / 1_048_576}MB");
        }

        // Check for very long lines (warning only)
        var lines = content.Split('\n');
        var longLines = lines.Where(line => line.Length > TemplateConstants.MaxLineLength).ToList();
        if (longLines.Count > 0)
        {
            _logger.LogWarning(
                "Template {TemplateId} has {Count} lines exceeding {MaxLength} characters (may indicate formatting issue)",
                templateId,
                longLines.Count,
                TemplateConstants.MaxLineLength);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validates template metadata structure and required fields.
    /// </summary>
    /// <param name="metadata">The template metadata to validate.</param>
    /// <param name="templateId">The template identifier for logging.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> with bool indicating success or failure with validation error message.
    /// </returns>
    public Result<bool> ValidateMetadata(TemplateMetadata? metadata, string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        // Metadata is optional, but if present, must be valid
        if (metadata == null)
        {
            _logger.LogDebug("Template {TemplateId} has no metadata (will use defaults)", templateId);
            return Result<bool>.Success(true);
        }

        // Use built-in validation from TemplateMetadata
        var validationErrors = metadata.Validate();
        if (validationErrors.Count > 0)
        {
            var errorMessage = string.Join("; ", validationErrors);
            _logger.LogWarning(
                "Template {TemplateId} has invalid metadata: {Errors}",
                templateId,
                errorMessage);
            return Result<bool>.Failure($"Invalid template metadata: {errorMessage}");
        }

        // Warn if version doesn't follow semantic versioning (advisory only)
        if (!string.IsNullOrWhiteSpace(metadata.Version) && !IsSemanticVersion(metadata.Version))
        {
            _logger.LogWarning(
                "Template {TemplateId} version '{Version}' does not follow semantic versioning format (advisory)",
                templateId,
                metadata.Version);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validates a complete prompt template including filename, content, and metadata.
    /// </summary>
    /// <param name="template">The template to validate.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> with bool indicating success or failure with validation error message.
    /// </returns>
    public Result<bool> ValidateTemplate(PromptTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        // Validate template ID (filename)
        var filenameResult = ValidateFilename(template.TemplateId);
        if (!filenameResult.IsSuccess)
        {
            return filenameResult;
        }

        // Validate content
        var contentResult = ValidateContent(template.Content, template.TemplateId);
        if (!contentResult.IsSuccess)
        {
            return contentResult;
        }

        // Validate metadata if present
        var metadataResult = ValidateMetadata(template.Metadata, template.TemplateId);
        if (!metadataResult.IsSuccess)
        {
            return metadataResult;
        }

        // Validate TemplateType consistency between metadata and template
        if (template.Metadata != null && template.TemplateType != template.Metadata.TemplateType)
        {
            _logger.LogWarning(
                "Template {TemplateId} has mismatched TemplateType: property={PropertyType}, metadata={MetadataType}",
                template.TemplateId,
                template.TemplateType,
                template.Metadata.TemplateType);
            return Result<bool>.Failure(
                $"Template type mismatch: property specifies {template.TemplateType} but metadata specifies {template.Metadata.TemplateType}");
        }

        // Warn if filesystem template is missing metadata (advisory)
        if (template.Source == TemplateSource.FileSystem && template.Metadata == null)
        {
            _logger.LogWarning(
                "Template {TemplateId} from filesystem has no metadata (recommended for custom templates)",
                template.TemplateId);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validates file size before reading content.
    /// </summary>
    /// <param name="fileSizeBytes">The file size in bytes.</param>
    /// <param name="filePath">The file path for logging.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> with bool indicating success or failure with validation error message.
    /// </returns>
    public Result<bool> ValidateFileSize(long fileSizeBytes, string filePath)
    {
        if (fileSizeBytes > TemplateConstants.MaxFileSizeBytes)
        {
            _logger.LogWarning(
                "Template file exceeds size limit: {FilePath}, {Size} bytes > {MaxSize} bytes",
                filePath,
                fileSizeBytes,
                TemplateConstants.MaxFileSizeBytes);
            return Result<bool>.Failure($"Template file exceeds size limit of {TemplateConstants.MaxFileSizeBytes / 1_048_576}MB");
        }

        return Result<bool>.Success(true);
    }

    private static bool IsReservedFilename(string filename)
    {
        // Reserved filenames for system use
        var reserved = new[]
        {
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        return reserved.Contains(filename.ToLowerInvariant());
    }

    private static bool IsSemanticVersion(string version)
    {
        // Simple semantic versioning check (MAJOR.MINOR.PATCH)
        // Allows optional pre-release and build metadata
        return SemanticVersionRegex().IsMatch(version);
    }

    /// <summary>
    /// Regex for validating kebab-case filenames.
    /// Allows lowercase letters, numbers, and hyphens.
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex KebabCaseRegex();

    /// <summary>
    /// Regex for validating semantic version format.
    /// Matches: MAJOR.MINOR.PATCH with optional pre-release and build metadata.
    /// </summary>
    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?(\+[a-zA-Z0-9.-]+)?$", RegexOptions.Compiled)]
    private static partial Regex SemanticVersionRegex();
}
