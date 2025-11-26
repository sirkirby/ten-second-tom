using MediatR;
using TenSecondTom.Shared.Options;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// CQRS command for updating transcription configuration.
/// Saves transcription configuration to the configuration file using IConfigurationSectionStore.
/// </summary>
public static class UpdateTranscribeConfiguration
{
    /// <summary>
    /// Command to update transcription configuration.
    /// </summary>
    /// <param name="Config">The new transcription configuration to save.</param>
    public sealed record Command(TranscribeOptions Config) : IRequest<Result<string>>;

    /// <summary>
    /// Handler for UpdateTranscribeConfiguration command (auto-discovered by MediatR).
    /// Saves the transcription configuration to the TenSecondTom:Transcribe configuration section.
    /// </summary>
    public sealed class Handler(IConfigurationSectionStore sectionStore)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            return await sectionStore.WriteSectionAsync(
                TranscribeOptions.SectionPath,
                request.Config,
                cancellationToken);
        }
    }
}
