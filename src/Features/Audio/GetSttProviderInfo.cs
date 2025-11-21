using MediatR;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Retrieves information about available Speech-to-Text providers.
/// Provides display names, descriptions, and provider IDs for use in setup wizards and UI.
/// </summary>
public static class GetSttProviderInfo
{
    /// <summary>
    /// Query to retrieve STT provider information.
    /// </summary>
    public sealed record Query : IRequest<Result<IReadOnlyList<SttProviderInfo>>>;

    /// <summary>
    /// Handler that returns information about available STT providers.
    /// </summary>
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<SttProviderInfo>>>
    {
        public Task<Result<IReadOnlyList<SttProviderInfo>>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var providers = new List<SttProviderInfo>
            {
                new(
                    ProviderId: SttProviders.WhisperCpp,
                    DisplayName: "whisper.cpp (Local, free)",
                    Description: "Local speech-to-text using whisper.cpp. No API key required.",
                    RequiresApiKey: false,
                    IsCloud: false),
                new(
                    ProviderId: SttProviders.OpenAI,
                    DisplayName: "OpenAI Whisper API (Cloud, requires key)",
                    Description: "Cloud-based OpenAI Whisper API. Requires OpenAI API key.",
                    RequiresApiKey: true,
                    IsCloud: true)
            };

            return Task.FromResult(Result<IReadOnlyList<SttProviderInfo>>.Success(providers));
        }
    }
}
