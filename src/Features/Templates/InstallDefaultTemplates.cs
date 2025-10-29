using System.IO.Abstractions;
using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates;

/// <summary>
/// Installs default prompt templates to the filesystem.
/// Used during initial setup, configuration migration, and self-healing.
/// </summary>
public static class InstallDefaultTemplates
{
    /// <summary>
    /// Command to install default prompt templates to the filesystem.
    /// </summary>
    /// <remarks>
    /// Used during:
    /// - Initial setup (new users)
    /// - Configuration migration (existing users)
    /// - Self-healing when templates directory is missing
    ///
    /// This command is idempotent - can be run multiple times safely.
    /// Existing templates are NOT overwritten to preserve user customizations.
    /// </remarks>
    public sealed record Command : IRequest<Result<CommandResult>>
    {
        /// <summary>
        /// The directory where templates should be installed.
        /// Typically {MemoryDirectory}/templates/
        /// Must be an absolute path.
        /// Directory will be created if it doesn't exist.
        /// </summary>
        public required string TargetDirectory { get; init; }

        /// <summary>
        /// If true, overwrites existing template files.
        /// If false (default), skips files that already exist.
        /// Default: false (preserve user customizations)
        /// </summary>
        public bool OverwriteExisting { get; init; }
    }

    /// <summary>
    /// Result of installing default templates.
    /// </summary>
    public sealed record CommandResult
    {
        /// <summary>
        /// Number of templates successfully installed.
        /// </summary>
        public required int TemplatesInstalled { get; init; }

        /// <summary>
        /// Number of templates skipped because they already existed.
        /// Only applicable when OverwriteExisting = false.
        /// </summary>
        public required int TemplatesSkipped { get; init; }

        /// <summary>
        /// Number of templates that failed to install.
        /// </summary>
        public required int TemplatesFailed { get; init; }

        /// <summary>
        /// List of template IDs that were successfully installed.
        /// Example: ["daily-summary", "weekly-review"]
        /// </summary>
        public required IReadOnlyList<string> InstalledTemplateIds { get; init; }
    }

    /// <summary>
    /// Handler for installing default prompt templates to the filesystem.
    /// Copies embedded template resources to the user's templates directory.
    /// </summary>
    /// <remarks>
    /// This handler:
    /// - Creates the templates directory if it doesn't exist
    /// - Loads embedded template resources from the assembly
    /// - Writes templates to disk with YAML front matter intact
    /// - Respects the OverwriteExisting flag to preserve user customizations
    /// - Logs detailed information about the installation process
    /// </remarks>
    public sealed class Handler(
        IFileSystem fileSystem,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<CommandResult>>
    {
        /// <summary>
        /// Handles the installation of default templates to the filesystem.
        /// Creates the target directory if needed and copies embedded templates.
        /// </summary>
        /// <param name="request">The installation command with target directory and options.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A result containing installation statistics (installed, skipped, failed counts)
        /// or a failure result with an error message.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
        public async Task<Result<CommandResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Validate input
            if (string.IsNullOrWhiteSpace(request.TargetDirectory))
            {
                return Result<CommandResult>.Failure(
                    "Target directory cannot be null or empty");
            }

            cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable CA1848 // Use LoggerMessage delegates for performance - startup/migration code, not hot path
            logger.LogInformation(
                "Installing default templates to {TargetDirectory} (OverwriteExisting={OverwriteExisting})",
                request.TargetDirectory,
                request.OverwriteExisting);
#pragma warning restore CA1848

            // Create target directory if it doesn't exist
            try
            {
                if (!fileSystem.Directory.Exists(request.TargetDirectory))
                {
                    fileSystem.Directory.CreateDirectory(request.TargetDirectory);
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                    logger.LogDebug("Created templates directory: {Directory}", request.TargetDirectory);
#pragma warning restore CA1848
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - need to handle all exceptions during migration
            catch (Exception ex)
#pragma warning restore CA1031
            {
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                logger.LogError(ex, "Failed to create templates directory: {Directory}", request.TargetDirectory);
#pragma warning restore CA1848
                return Result<CommandResult>.Failure(
                    $"Failed to create templates directory: {ex.Message}");
            }

            // Load raw embedded templates (with YAML front matter intact)
            // We load directly from embedded resources instead of using templateLoader
            // because templateLoader parses and strips the YAML front matter
            const string embeddedResourcePrefix = "TenSecondTom.Infrastructure.Prompts.Templates";
            Assembly assembly = typeof(EmbeddedPromptTemplateLoader).Assembly;

            string[] resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(embeddedResourcePrefix, StringComparison.Ordinal) &&
                              name.EndsWith(".md", StringComparison.Ordinal))
                .ToArray();

            if (resourceNames.Length == 0)
            {
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                logger.LogWarning("No embedded templates found to install");
#pragma warning restore CA1848
                return Result<CommandResult>.Success(new CommandResult
                {
                    TemplatesInstalled = 0,
                    TemplatesSkipped = 0,
                    TemplatesFailed = 0,
                    InstalledTemplateIds = Array.Empty<string>()
                });
            }

            // Install each template
            int installed = 0;
            int skipped = 0;
            int failed = 0;
            var installedIds = new List<string>();

            foreach (string resourceName in resourceNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract template ID from resource name
                string templateId = resourceName
                    .Replace($"{embeddedResourcePrefix}.", string.Empty, StringComparison.Ordinal)
                    .Replace(".md", string.Empty, StringComparison.Ordinal);

                string fileName = $"{templateId}.md";
                string filePath = fileSystem.Path.Combine(request.TargetDirectory, fileName);

                try
                {
                    // Check if file already exists
                    bool fileExists = fileSystem.File.Exists(filePath);

                    if (fileExists && !request.OverwriteExisting)
                    {
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                        logger.LogDebug("Skipping existing template: {TemplateId}", templateId);
#pragma warning restore CA1848
                        skipped++;
                        continue;
                    }

                    // Load raw content from embedded resource (with YAML front matter intact)
                    using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream is null)
                    {
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                        logger.LogWarning("Failed to load embedded resource: {ResourceName}", resourceName);
#pragma warning restore CA1848
                        failed++;
                        continue;
                    }

                    using StreamReader reader = new(resourceStream);
                    string rawContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                    // Write raw template content to file (includes YAML front matter)
                    await fileSystem.File.WriteAllTextAsync(filePath, rawContent, cancellationToken)
                        .ConfigureAwait(false);

#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                    logger.LogDebug(
                        "Installed template: {TemplateId} to {FilePath} (Overwritten={Overwritten})",
                        templateId,
                        filePath,
                        fileExists);
#pragma warning restore CA1848

                    installed++;
                    installedIds.Add(templateId);
                }
#pragma warning disable CA1031 // Do not catch general exception types - need to handle all file system errors
                catch (Exception ex)
#pragma warning restore CA1031
                {
#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                    logger.LogError(ex, "Failed to install template: {TemplateId}", templateId);
#pragma warning restore CA1848
                    failed++;
                }
            }

#pragma warning disable CA1848 // Use LoggerMessage delegates for performance
            logger.LogInformation(
                "Template installation complete: {Installed} installed, {Skipped} skipped, {Failed} failed",
                installed,
                skipped,
                failed);
#pragma warning restore CA1848

            return Result<CommandResult>.Success(new CommandResult
            {
                TemplatesInstalled = installed,
                TemplatesSkipped = skipped,
                TemplatesFailed = failed,
                InstalledTemplateIds = installedIds.AsReadOnly()
            });
        }
    }
}
