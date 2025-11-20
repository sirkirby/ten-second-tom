using MediatR;
using TenSecondTom.Shared.Options;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// CQRS command for updating audio configuration.
/// Saves audio configuration to the configuration file using IConfigurationSectionStore.
/// </summary>
public static class UpdateAudioConfiguration
{
    /// <summary>
    /// Command to update audio configuration.
    /// </summary>
    /// <param name="Config">The new audio configuration to save.</param>
    public sealed record Command(AudioOptions Config) : IRequest<Result<string>>;

    /// <summary>
    /// Handler for UpdateAudioConfiguration command (auto-discovered by MediatR).
    /// Saves the audio configuration to the TenSecondTom:Audio configuration section.
    /// </summary>
    public sealed class Handler(IConfigurationSectionStore sectionStore)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            // Save audio configuration directly to the TenSecondTom:Audio section
            return await sectionStore.WriteSectionAsync(
                "TenSecondTom:Audio",
                request.Config,
                cancellationToken);
        }
    }
}
