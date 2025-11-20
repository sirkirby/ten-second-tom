using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Abstractions.Templates;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Templates;

/// <summary>
/// Infrastructure implementation that installs bundled prompt templates
/// to the filesystem without introducing feature-layer dependencies.
/// </summary>
public sealed class TemplateInstaller(
    IFileSystem fileSystem,
    EmbeddedPromptTemplateLoader embeddedTemplateLoader,
    ILogger<TemplateInstaller> logger) : ITemplateInstaller
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly EmbeddedPromptTemplateLoader _embeddedTemplateLoader =
        embeddedTemplateLoader ?? throw new ArgumentNullException(nameof(embeddedTemplateLoader));
    private readonly ILogger<TemplateInstaller> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<Result<TemplateInstallationResult>> InstallDefaultsAsync(
        string targetDirectory,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Installing default templates to {TargetDirectory} (OverwriteExisting={OverwriteExisting})",
            targetDirectory,
            overwriteExisting);

        // Ensure target directory exists
        try
        {
            if (!_fileSystem.Directory.Exists(targetDirectory))
            {
                _fileSystem.Directory.CreateDirectory(targetDirectory);
                _logger.LogDebug("Created templates directory: {Directory}", targetDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create templates directory: {Directory}", targetDirectory);
            return Result<TemplateInstallationResult>.Failure(
                $"Failed to create templates directory: {ex.Message}");
        }

        // Discover embedded templates using shared loader
        var discoveryResult = await _embeddedTemplateLoader.LoadAllTemplatesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!discoveryResult.IsSuccess)
        {
            _logger.LogWarning("Failed to discover embedded templates: {Error}", discoveryResult.Error);
            return Result<TemplateInstallationResult>.Failure(
                $"Failed to discover embedded templates: {discoveryResult.Error}");
        }

        var templates = discoveryResult.Value;

        if (templates.Count == 0)
        {
            _logger.LogWarning("No embedded templates found to install");
            return Result<TemplateInstallationResult>.Success(new TemplateInstallationResult
            {
                TemplatesInstalled = 0,
                TemplatesSkipped = 0,
                TemplatesFailed = 0,
                InstalledTemplateIds = Array.Empty<string>()
            });
        }

        int installed = 0;
        int skipped = 0;
        int failed = 0;
        var installedIds = new List<string>();

        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string templateId = template.TemplateId;
            string fileName = $"{templateId}.md";
            string filePath = _fileSystem.Path.Combine(targetDirectory, fileName);

            try
            {
                bool fileExists = _fileSystem.File.Exists(filePath);

                if (fileExists && !overwriteExisting)
                {
                    _logger.LogDebug("Skipping existing template: {TemplateId}", templateId);
                    skipped++;
                    continue;
                }

                var rawContentResult = await _embeddedTemplateLoader.LoadRawTemplateContentAsync(
                        templateId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!rawContentResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "Failed to load raw content for template {TemplateId}: {Error}",
                        templateId,
                        rawContentResult.Error);
                    failed++;
                    continue;
                }

                await _fileSystem.File.WriteAllTextAsync(filePath, rawContentResult.Value, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Installed template: {TemplateId} to {FilePath} (Overwritten={Overwritten})",
                    templateId,
                    filePath,
                    fileExists);

                installed++;
                installedIds.Add(templateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install template: {TemplateId}", templateId);
                failed++;
            }
        }

        _logger.LogInformation(
            "Template installation complete: {Installed} installed, {Skipped} skipped, {Failed} failed",
            installed,
            skipped,
            failed);

        return Result<TemplateInstallationResult>.Success(new TemplateInstallationResult
        {
            TemplatesInstalled = installed,
            TemplatesSkipped = skipped,
            TemplatesFailed = failed,
            InstalledTemplateIds = installedIds.AsReadOnly()
        });
    }
}

