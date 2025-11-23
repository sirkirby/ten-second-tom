using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.Templates;
using TenSecondTom.Shared.Models;
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
    public sealed record Command : IRequest<Result<TemplateInstallationResult>>
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
    /// Deprecated: Use TemplateInstallationResult from TenSecondTom.Shared.Models instead.
    /// This nested type is kept for backward compatibility only.
    /// </summary>
    [Obsolete("Use TenSecondTom.Shared.Models.TemplateInstallationResult instead", false)]
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
    /// - Uses the template loader to discover and load embedded templates
    /// - Writes templates to disk with YAML front matter intact
    /// - Respects the OverwriteExisting flag to preserve user customizations
    /// - Logs detailed information about the installation process
    /// </remarks>
    public sealed class Handler(
        ITemplateInstaller templateInstaller,
        IMediator mediator,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<TemplateInstallationResult>>
    {
        private readonly ITemplateInstaller _templateInstaller = templateInstaller ?? throw new ArgumentNullException(nameof(templateInstaller));
        private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        private readonly ILogger<Handler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        public async Task<Result<TemplateInstallationResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.TargetDirectory))
            {
                return Result<TemplateInstallationResult>.Failure("Target directory cannot be null or empty");
            }

            _logger.LogInformation(
                "Installing default templates to {TargetDirectory} (OverwriteExisting={OverwriteExisting})",
                request.TargetDirectory,
                request.OverwriteExisting);

            var result = await _templateInstaller.InstallDefaultsAsync(
                request.TargetDirectory,
                request.OverwriteExisting,
                cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Template installation failed for {Directory}: {Error}",
                    request.TargetDirectory,
                    result.Error);

                // Send error notification (non-blocking, fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var notificationCommand = new Features.Notifications.ShowNotification.Command(
                            Title: "Template Installation Failed",
                            Message: $"Failed to install templates: {result.Error}\n\nPlease check directory permissions.",
                            Priority: NotificationPriority.High,
                            TimeoutSeconds: null,
                            Actions: null);

                        var notificationResult = await _mediator.Send(notificationCommand, CancellationToken.None);

                        if (!notificationResult.IsSuccess)
                        {
                            _logger.LogWarning(
                                "Failed to send template installation error notification: {Error}",
                                notificationResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Unexpected error sending template installation error notification (non-critical)");
                    }
                }, CancellationToken.None);

                return result;
            }

            _logger.LogInformation(
                "Template installation complete: {Installed} installed, {Skipped} skipped, {Failed} failed",
                result.Value.TemplatesInstalled,
                result.Value.TemplatesSkipped,
                result.Value.TemplatesFailed);

            // Send success notification (non-blocking, fire-and-forget)
            // Only notify if at least one template was installed
            if (result.Value.TemplatesInstalled > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var notificationCommand = new Features.Notifications.ShowNotification.Command(
                            Title: "Templates Installed",
                            Message: $"{result.Value.TemplatesInstalled} template(s) installed successfully.\n\n{string.Join(", ", result.Value.InstalledTemplateIds.Take(3))}{(result.Value.InstalledTemplateIds.Count > 3 ? "..." : "")}",
                            Priority: NotificationPriority.Low,
                            TimeoutSeconds: null,
                            Actions: null);

                        var notificationResult = await _mediator.Send(notificationCommand, CancellationToken.None);

                        if (!notificationResult.IsSuccess)
                        {
                            _logger.LogWarning(
                                "Failed to send template installation notification: {Error}",
                                notificationResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Unexpected error sending template installation notification (non-critical)");
                    }
                }, CancellationToken.None);
            }

            return result;
        }
    }
}
