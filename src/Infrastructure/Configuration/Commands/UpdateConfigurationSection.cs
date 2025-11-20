using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration.Commands;

/// <summary>
/// CQRS command for updating a configuration section and persisting to storage.
/// Provides abstraction over configuration file manipulation, allowing features to
/// update configuration without direct file system knowledge.
/// </summary>
/// <remarks>
/// This infrastructure command bridges the gap between feature logic (what to configure)
/// and storage mechanism (how to persist). Features send commands to update configuration;
/// this handler coordinates with IConfigurationSectionStore to persist changes.
///
/// Example usage from a feature:
/// <code>
/// var command = new UpdateConfigurationSection.Command(
///     "TenSecondTom:Audio",
///     audioConfig);
/// var result = await mediator.Send(command, cancellationToken);
/// </code>
///
/// Thread safety: IConfigurationSectionStore handles concurrent access internally.
/// Atomic writes: Configuration updates are atomic (temp file + move).
/// Section preservation: Updating one section preserves all other sections.
/// </remarks>
public static class UpdateConfigurationSection
{
    /// <summary>
    /// Command to update a specific configuration section.
    /// </summary>
    /// <param name="SectionPath">The configuration section path using colon notation (e.g., "TenSecondTom:Audio").</param>
    /// <param name="Configuration">The configuration object to serialize and write.</param>
    public sealed record Command(
        string SectionPath,
        object Configuration) : IRequest<Result<string>>;

    /// <summary>
    /// Validator ensures section path is valid.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SectionPath)
                .NotEmpty()
                .WithMessage("Section path is required");

            RuleFor(x => x.Configuration)
                .NotNull()
                .WithMessage("Configuration object cannot be null");
        }
    }

    /// <summary>
    /// Handler persists configuration changes to storage via IConfigurationSectionStore.
    /// </summary>
    /// <remarks>
    /// This handler is intentionally generic and feature-agnostic. It delegates to
    /// IConfigurationSectionStore which handles JSON serialization, atomic writes,
    /// and section preservation.
    ///
    /// Error handling:
    /// - Returns Success with config file path on successful write
    /// - Returns Failure if section path is invalid or write operation fails
    /// - Logs all operations for diagnostics
    /// </remarks>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            logger.LogDebug(
                "Processing UpdateConfigurationSection command for section {SectionPath}",
                request.SectionPath);

            try
            {
                // Delegate to IConfigurationSectionStore for type-safe, atomic write
                // The store handles JSON serialization, section navigation, and atomic file operations
                var result = await sectionStore.WriteSectionAsync(
                    request.SectionPath,
                    request.Configuration,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    logger.LogInformation(
                        "Successfully updated configuration section {SectionPath}",
                        request.SectionPath);
                }
                else
                {
                    logger.LogWarning(
                        "Failed to update configuration section {SectionPath}: {Error}",
                        request.SectionPath,
                        result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled exception while updating configuration section {SectionPath}",
                    request.SectionPath);

                return Result<string>.Failure($"Failed to update configuration: {ex.Message}");
            }
        }
    }
}
