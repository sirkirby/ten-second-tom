using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for storing generated outputs to the recording directory.
/// Handles file naming, overwrite behavior, and metadata embedding.
/// </summary>
public sealed class OutputStorageService : IOutputStorageService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _recordingDirectory;
    private readonly ILogger<OutputStorageService> _logger;

    public OutputStorageService(
        IFileSystem fileSystem,
        IOptions<StorageOptions> storageOptions,
        ILogger<OutputStorageService> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get memory directory and expand home directory if needed
        var memoryDirectory = (storageOptions ?? throw new ArgumentNullException(nameof(storageOptions))).Value.MemoryDirectory
            .Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        _recordingDirectory = Path.Combine(memoryDirectory, DirectoryNames.Recording);
    }

    public async Task<Result<string>> SaveOutputAsync(
        GeneratedOutput output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate directory exists
            if (!_fileSystem.Directory.Exists(_recordingDirectory))
            {
                return Result<string>.Failure(
                    $"Recording directory not found: {_recordingDirectory}");
            }

            // Build file path
            var outputPath = BuildOutputFilePath(output.RecordingBaseName, output.TemplateId);

            // Check if file already exists for logging
            var fileExists = _fileSystem.File.Exists(outputPath);

            // Format content with metadata
            var markdown = output.ToMarkdown();

            // Validate output size before writing
            var outputSizeBytes = System.Text.Encoding.UTF8.GetByteCount(markdown);
            if (outputSizeBytes > LlmConstants.MaxOutputFileSizeBytes)
            {
                var maxSizeMb = LlmConstants.MaxOutputFileSizeBytes / (1024 * 1024);
                var actualSizeMb = outputSizeBytes / (1024.0 * 1024.0);

                _logger.LogError(
                    "Output file exceeds maximum size: {ActualSize:F2} MB > {MaxSize} MB for {Recording}/{Template}",
                    actualSizeMb,
                    maxSizeMb,
                    output.RecordingBaseName,
                    output.TemplateId);

                return Result<string>.Failure(
                    $"Generated output is too large ({actualSizeMb:F2} MB). Maximum allowed size is {maxSizeMb} MB.");
            }

            // Write file (overwrite if exists)
            await _fileSystem.File.WriteAllTextAsync(outputPath, markdown, cancellationToken);

            if (fileExists)
            {
                _logger.LogInformation(
                    "Overwritten existing output at: {Path} ({Size} bytes)",
                    outputPath,
                    markdown.Length);
            }
            else
            {
                _logger.LogInformation(
                    "Saved output to: {Path} ({Size} bytes)",
                    outputPath,
                    markdown.Length);
            }

            return Result<string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save output for {Recording}/{Template}",
                output.RecordingBaseName,
                output.TemplateId);

            return Result<string>.Failure($"Failed to save output: {ex.Message}");
        }
    }

    public Task<bool> OutputExistsAsync(
        string recordingBaseName,
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var outputPath = BuildOutputFilePath(recordingBaseName, templateId);
        return Task.FromResult(_fileSystem.File.Exists(outputPath));
    }

    public string BuildOutputFilePath(string recordingBaseName, string templateId)
    {
        var fileName = $"{recordingBaseName}_{templateId}.md";
        return Path.Combine(_recordingDirectory, fileName);
    }
}
