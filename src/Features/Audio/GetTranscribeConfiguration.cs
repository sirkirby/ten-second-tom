using MediatR;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// CQRS query for retrieving transcription configuration.
/// Provides read-only access to TranscribeOptions for other features without direct coupling.
/// </summary>
public static class GetTranscribeConfiguration
{
    /// <summary>
    /// Query to retrieve current transcription configuration.
    /// </summary>
    public sealed record Query : IRequest<Result<TranscribeOptions>>;

    /// <summary>
    /// Handler for GetTranscribeConfiguration query (auto-discovered by MediatR).
    /// Retrieves the latest transcription configuration from the configuration store.
    /// </summary>
    public sealed class Handler(IConfigurationSectionStore sectionStore)
        : IRequestHandler<Query, Result<TranscribeOptions>>
    {
        public async Task<Result<TranscribeOptions>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var storedConfig = await sectionStore.ReadSectionAsync<TranscribeOptions>(
                TranscribeOptions.SectionPath,
                cancellationToken).ConfigureAwait(false);

            if (!storedConfig.IsSuccess)
            {
                return Result<TranscribeOptions>.Failure(storedConfig.Error ?? "Failed to load transcription configuration.");
            }

            return Result<TranscribeOptions>.Success(storedConfig.Value ?? new TranscribeOptions());
        }
    }
}
