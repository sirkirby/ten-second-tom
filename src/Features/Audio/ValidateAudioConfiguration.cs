using MediatR;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Validates audio configuration completeness.
/// Provides cross-feature access to audio validation via CQRS pattern.
/// </summary>
public static class ValidateAudioConfiguration
{
    /// <summary>
    /// Query to validate audio configuration.
    /// Returns AudioValidationResult from Shared/Models for cross-feature compatibility.
    /// </summary>
    public sealed record Query : IRequest<Result<AudioValidationResult>>;

    /// <summary>
    /// Handler for audio configuration validation query.
    /// </summary>
    public sealed class Handler(
        IOptions<AudioOptions> audioOptions,
        IAudioConfigurationValidator validator) : IRequestHandler<Query, Result<AudioValidationResult>>
    {
        private readonly AudioOptions _audioOptions = audioOptions.Value;

        public Task<Result<AudioValidationResult>> Handle(Query request, CancellationToken cancellationToken)
        {
            var isConfigured = validator.IsAudioConfigured(_audioOptions);
            var missingItems = isConfigured
                ? Array.Empty<string>()
                : validator.GetMissingConfiguration(_audioOptions);

            var response = new AudioValidationResult
            {
                IsConfigured = isConfigured,
                MissingItems = missingItems
            };

            return Task.FromResult(Result<AudioValidationResult>.Success(response));
        }
    }
}
