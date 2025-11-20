using MediatR;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;
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
            // Convert AudioOptions to deprecated AudioConfiguration for validation
            // TODO: Update validator to work directly with AudioOptions once migration is complete
            var deprecatedConfig = new AudioConfiguration
            {
                SttProvider = _audioOptions.SttProvider,
                SttApiKey = _audioOptions.SttApiKey,
                SttFallbackEnabled = _audioOptions.SttFallbackEnabled,
                SttFallbackProvider = _audioOptions.SttFallbackProvider,
                SttFallbackApiKey = _audioOptions.SttFallbackApiKey,
                SttBinaryPath = _audioOptions.SttBinaryPath,
                SttModel = _audioOptions.SttModel,
                SttFallbackBinaryPath = _audioOptions.SttFallbackBinaryPath,
                SttFallbackModel = _audioOptions.SttFallbackModel,
                KeepFiles = _audioOptions.KeepFiles,
                Recorder = new RecorderConfiguration
                {
                    FfmpegPath = _audioOptions.Recorder.FfmpegPath,
                    InputVolume = _audioOptions.Recorder.InputVolume,
                    EnableNoiseReduction = _audioOptions.Recorder.EnableNoiseReduction,
                    EnableFrequencyFilters = _audioOptions.Recorder.EnableFrequencyFilters
                },
                Preprocessing = new PreprocessingConfiguration
                {
                    RemoveSilence = _audioOptions.Preprocessing.RemoveSilence,
                    SilenceThresholdDb = _audioOptions.Preprocessing.SilenceThresholdDb,
                    MinimumSilenceDurationMs = _audioOptions.Preprocessing.MinimumSilenceDurationMs
                },
                Timeouts = new RecordingTimeoutsConfiguration
                {
                    TodaySeconds = _audioOptions.Timeouts.TodaySeconds,
                    RecordSeconds = _audioOptions.Timeouts.RecordSeconds
                }
            };

            var isConfigured = validator.IsAudioConfigured(deprecatedConfig);
            var missingItems = isConfigured
                ? Array.Empty<string>()
                : validator.GetMissingConfiguration(deprecatedConfig);

            var response = new AudioValidationResult
            {
                IsConfigured = isConfigured,
                MissingItems = missingItems
            };

            return Task.FromResult(Result<AudioValidationResult>.Success(response));
        }
    }
}
