using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration.Commands;

/// <summary>
/// CQRS query for reading a configuration section from storage.
/// Provides abstraction over configuration file access, allowing features to
/// read configuration without direct file system knowledge.
/// </summary>
/// <remarks>
/// This infrastructure query bridges the gap between feature logic (what to read)
/// and storage mechanism (how to retrieve). Features send queries to read configuration;
/// this handler coordinates with IConfigurationSectionStore to fetch sections.
///
/// Example usage from a feature:
/// <code>
/// var query = new ReadConfigurationSection&lt;AudioOptions&gt;.Query("TenSecondTom:Audio");
/// var result = await mediator.Send(query, cancellationToken);
/// if (result.IsSuccess)
/// {
///     var audioConfig = result.Value;
/// }
/// </code>
///
/// Default handling: If section doesn't exist, returns new T() (default instance).
/// Thread safety: IConfigurationSectionStore handles concurrent access internally.
/// </remarks>
public static class ReadConfigurationSection<T> where T : new()
{
    /// <summary>
    /// Query to read a specific configuration section.
    /// </summary>
    /// <param name="SectionPath">The configuration section path using colon notation (e.g., "TenSecondTom:Audio").</param>
    public sealed record Query(string SectionPath) : IRequest<Result<T>>;

    /// <summary>
    /// Validator ensures section path is valid.
    /// </summary>
    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.SectionPath)
                .NotEmpty()
                .WithMessage("Section path is required");
        }
    }

    /// <summary>
    /// Handler reads configuration section from storage via IConfigurationSectionStore.
    /// </summary>
    /// <remarks>
    /// This handler is intentionally generic and feature-agnostic. It delegates to
    /// IConfigurationSectionStore which handles JSON deserialization and section navigation.
    ///
    /// Default behavior:
    /// - If section doesn't exist, returns new T() (allows graceful handling of missing config)
    /// - If file doesn't exist, returns new T() (first-run scenario)
    ///
    /// Error handling:
    /// - Returns Success with deserialized section or default instance
    /// - Returns Failure if file cannot be read or JSON is invalid
    /// - Logs all operations for diagnostics
    /// </remarks>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ILogger<Handler> logger) : IRequestHandler<Query, Result<T>>
    {
        public async Task<Result<T>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            logger.LogDebug(
                "Processing ReadConfigurationSection query for section {SectionPath} as type {Type}",
                request.SectionPath,
                typeof(T).Name);

            try
            {
                // Delegate to IConfigurationSectionStore for type-safe read
                // The store handles JSON deserialization, section navigation, and default instances
                var result = await sectionStore.ReadSectionAsync<T>(
                    request.SectionPath,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    logger.LogDebug(
                        "Successfully read configuration section {SectionPath} as type {Type}",
                        request.SectionPath,
                        typeof(T).Name);
                }
                else
                {
                    logger.LogWarning(
                        "Failed to read configuration section {SectionPath}: {Error}",
                        request.SectionPath,
                        result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled exception while reading configuration section {SectionPath}",
                    request.SectionPath);

                return Result<T>.Failure($"Failed to read configuration: {ex.Message}");
            }
        }
    }
}
