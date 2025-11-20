using MediatR;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// CQRS query for retrieving audio configuration.
/// Provides read-only access to AudioOptions for other features without direct coupling.
/// </summary>
public static class GetAudioConfiguration
{
    /// <summary>
    /// Query to retrieve current audio configuration.
    /// </summary>
    public sealed record Query : IRequest<Result<AudioOptions>>;

    /// <summary>
    /// Handler for GetAudioConfiguration query (auto-discovered by MediatR).
    /// Retrieves the latest audio configuration from the configuration store.
    /// </summary>
    public sealed class Handler(IConfigurationSectionStore sectionStore)
        : IRequestHandler<Query, Result<AudioOptions>>
    {
        public async Task<Result<AudioOptions>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var storedConfig = await sectionStore.ReadSectionAsync<AudioOptions>(
                AudioOptions.SectionPath,
                cancellationToken).ConfigureAwait(false);

            if (!storedConfig.IsSuccess)
            {
                return Result<AudioOptions>.Failure(storedConfig.Error ?? "Failed to load audio configuration.");
            }

            return Result<AudioOptions>.Success(storedConfig.Value ?? new AudioOptions());
        }
    }
}
