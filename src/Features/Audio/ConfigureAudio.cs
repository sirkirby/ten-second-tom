using TenSecondTom.Features.Audio.Constants;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Configures audio recording and processing settings interactively or via command-line arguments.
/// This use case handles all audio configuration logic, keeping it within the Audio feature slice.
/// </summary>
public static class ConfigureAudio
{
    /// <summary>
    /// Command to configure audio settings.
    /// Supports both interactive prompts and direct value assignment via arguments.
    /// </summary>
    public sealed record Command : IRequest<Result<AudioConfigurationResult>>
    {
        /// <summary>
        /// Gets the timeout in seconds for 'record' command.
        /// When provided, skips the interactive prompt for this setting.
        /// </summary>
        public int? RecordTimeoutSeconds { get; init; }

        /// <summary>
        /// Gets the input volume multiplier (0.0 to 2.0) for audio recording.
        /// When provided, skips the interactive prompt for this setting.
        /// </summary>
        public double? InputVolume { get; init; }

        /// <summary>
        /// Gets whether to enable noise reduction during recording.
        /// When provided, skips the interactive prompt for this setting.
        /// </summary>
        public bool? EnableNoiseReduction { get; init; }

        /// <summary>
        /// Gets whether to enable frequency filters during recording.
        /// When provided, skips the interactive prompt for this setting.
        /// </summary>
        public bool? EnableFrequencyFilters { get; init; }
    }

    /// <summary>
    /// Validator for ConfigureAudio command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RecordTimeoutSeconds)
                .InclusiveBetween(AudioConstants.MinRecordTimeoutSeconds, AudioConstants.MaxRecordTimeoutSeconds)
                .When(x => x.RecordTimeoutSeconds.HasValue)
                .WithMessage($"Record timeout must be between {AudioConstants.MinRecordTimeoutSeconds} and {AudioConstants.MaxRecordTimeoutSeconds} seconds.");

            RuleFor(x => x.InputVolume)
                .InclusiveBetween(0.0, 2.0)
                .When(x => x.InputVolume.HasValue)
                .WithMessage("Input volume must be between 0.0 and 2.0.");
        }
    }

    /// <summary>
    /// Handler for ConfigureAudio command (auto-discovered by MediatR).
    /// Orchestrates interactive audio configuration with optional command-line overrides.
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ISetupWizardUI setupWizard,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<AudioConfigurationResult>>
    {
        public async Task<Result<AudioConfigurationResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting audio configuration");

            // Read current audio configuration (recording settings)
            var audioConfigResult = await sectionStore.ReadSectionAsync<AudioOptions>(
                AudioOptions.SectionPath,
                cancellationToken);

            if (!audioConfigResult.IsSuccess)
            {
                return Result<AudioConfigurationResult>.Failure($"Failed to load current audio configuration: {audioConfigResult.Error}");
            }

            var currentAudio = audioConfigResult.Value;

            if (HasCommandLineOverrides(request))
            {
                return await ApplyCommandLineOverridesAsync(request, currentAudio, cancellationToken);
            }

            const int totalSteps = 7;

            // Step 1: Input Volume
            double? inputVolume;
            if (request.InputVolume.HasValue)
            {
                setupWizard.ShowStepHeader(1, totalSteps, "Input Volume");
                setupWizard.ShowStatus($"Using provided value: {request.InputVolume.Value:F1}");
                inputVolume = request.InputVolume.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(1, totalSteps, "Input Volume");
                inputVolume = await setupWizard.PromptForInputVolumeAsync(
                    currentAudio.Recorder.InputVolume,
                    cancellationToken);

                if (!inputVolume.HasValue)
                {
                    return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Step 2: Noise Reduction
            bool? noiseReduction;
            if (request.EnableNoiseReduction.HasValue)
            {
                setupWizard.ShowStepHeader(2, totalSteps, "Noise Reduction");
                setupWizard.ShowStatus($"Using provided value: {(request.EnableNoiseReduction.Value ? "Enabled" : "Disabled")}");
                noiseReduction = request.EnableNoiseReduction.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(2, totalSteps, "Noise Reduction");
                noiseReduction = await setupWizard.PromptForBooleanAsync(
                    "Enable noise reduction during recording?",
                    currentAudio.Recorder.EnableNoiseReduction,
                    cancellationToken);

                if (!noiseReduction.HasValue)
                {
                    return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Step 3: Frequency Filters
            bool? frequencyFilters;
            if (request.EnableFrequencyFilters.HasValue)
            {
                setupWizard.ShowStepHeader(3, totalSteps, "Frequency Filters");
                setupWizard.ShowStatus($"Using provided value: {(request.EnableFrequencyFilters.Value ? "Enabled" : "Disabled")}");
                frequencyFilters = request.EnableFrequencyFilters.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(3, totalSteps, "Frequency Filters");
                frequencyFilters = await setupWizard.PromptForBooleanAsync(
                    "Enable frequency filters during recording?",
                    currentAudio.Recorder.EnableFrequencyFilters,
                    cancellationToken);

                if (!frequencyFilters.HasValue)
                {
                    return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Step 4: Silence Removal
            setupWizard.ShowStepHeader(4, totalSteps, "Silence Removal");
            var removeSilence = await setupWizard.PromptForBooleanAsync(
                "Remove silence from recordings during preprocessing?",
                currentAudio.Preprocessing.RemoveSilence,
                cancellationToken);

            if (!removeSilence.HasValue)
            {
                return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
            }

            // Step 5: Silence Threshold (only if silence removal enabled)
            int silenceThresholdDb = currentAudio.Preprocessing.SilenceThresholdDb;
            if (removeSilence.Value)
            {
                setupWizard.ShowStepHeader(5, totalSteps, "Silence Detection Threshold");
                var threshold = await setupWizard.PromptForIntAsync(
                    "Silence threshold in decibels (-60 to -40):",
                    currentAudio.Preprocessing.SilenceThresholdDb,
                    -60,
                    -40,
                    cancellationToken);

                if (!threshold.HasValue)
                {
                    return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
                }
                silenceThresholdDb = threshold.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(5, totalSteps, "Silence Detection Threshold");
                setupWizard.ShowStatus("Skipped (silence removal disabled)");
            }

            // Step 6: Minimum Silence Duration (only if silence removal enabled)
            int minSilenceDurationMs = currentAudio.Preprocessing.MinimumSilenceDurationMs;
            if (removeSilence.Value)
            {
                setupWizard.ShowStepHeader(6, totalSteps, "Minimum Silence Duration");
                var duration = await setupWizard.PromptForIntAsync(
                    "Minimum silence duration to remove (ms, 100-2000):",
                    currentAudio.Preprocessing.MinimumSilenceDurationMs,
                    100,
                    2000,
                    cancellationToken);

                if (!duration.HasValue)
                {
                    return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
                }
                minSilenceDurationMs = duration.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(6, totalSteps, "Minimum Silence Duration");
                setupWizard.ShowStatus("Skipped (silence removal disabled)");
            }

            // Step 7: Record Command Timeout
            int? recordTimeout;
            if (request.RecordTimeoutSeconds.HasValue)
            {
                setupWizard.ShowStepHeader(7, totalSteps, "Record Command Timeout");
                setupWizard.ShowStatus($"Using provided value: {request.RecordTimeoutSeconds.Value} seconds");
                recordTimeout = request.RecordTimeoutSeconds.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(7, totalSteps, "Record Command Timeout");
                setupWizard.ShowStatus("When this duration is reached, you'll be prompted to continue or finish recording.");
                recordTimeout = await setupWizard.PromptForIntAsync(
                    $"Recording timeout (seconds, {AudioConstants.MinRecordTimeoutSeconds}-{AudioConstants.MaxRecordTimeoutSeconds}):",
                    currentAudio.Timeouts.RecordSeconds,
                    AudioConstants.MinRecordTimeoutSeconds,
                    AudioConstants.MaxRecordTimeoutSeconds,
                    cancellationToken);

                if (!recordTimeout.HasValue)
                {
                    return Result<AudioConfigurationResult>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Build updated audio configuration (recording settings only)
            var updatedAudio = new AudioOptions
            {
                Recorder = new RecorderOptions
                {
                    FfmpegPath = currentAudio.Recorder.FfmpegPath,
                    InputVolume = inputVolume!.Value,
                    EnableNoiseReduction = noiseReduction!.Value,
                    EnableFrequencyFilters = frequencyFilters!.Value
                },
                Preprocessing = new PreprocessingOptions
                {
                    RemoveSilence = removeSilence.Value,
                    SilenceThresholdDb = silenceThresholdDb,
                    MinimumSilenceDurationMs = minSilenceDurationMs
                },
                Timeouts = new RecordingTimeoutsOptions
                {
                    TodaySeconds = currentAudio.Timeouts.TodaySeconds, // Preserve existing value (legacy)
                    RecordSeconds = recordTimeout.Value
                }
            };

            // Save audio configuration
            var audioSaveResult = await sectionStore.WriteSectionAsync(
                AudioOptions.SectionPath,
                updatedAudio,
                cancellationToken);

            if (!audioSaveResult.IsSuccess)
            {
                return Result<AudioConfigurationResult>.Failure($"Failed to save audio configuration: {audioSaveResult.Error}. Changes were not applied.");
            }

            logger.LogInformation("Audio configuration updated successfully");

            // Display success message with summary
            setupWizard.ShowSuccess("✓ Audio configuration saved successfully");
            setupWizard.ShowStatus($"  • Input volume: {inputVolume.Value:F1}");
            setupWizard.ShowStatus($"  • Noise reduction: {(noiseReduction.Value ? "Enabled" : "Disabled")}");
            setupWizard.ShowStatus($"  • Frequency filters: {(frequencyFilters.Value ? "Enabled" : "Disabled")}");
            setupWizard.ShowStatus($"  • Silence removal: {(removeSilence.Value ? "Enabled" : "Disabled")}");
            if (removeSilence.Value)
            {
                setupWizard.ShowStatus($"  • Silence threshold: {silenceThresholdDb} dB");
                setupWizard.ShowStatus($"  • Min silence duration: {minSilenceDurationMs} ms");
            }
            setupWizard.ShowStatus($"  • Record timeout: {recordTimeout}s");

            return Result<AudioConfigurationResult>.Success(new AudioConfigurationResult
            {
                Audio = updatedAudio
            });
        }

        private static bool HasCommandLineOverrides(Command request)
        {
            return request.RecordTimeoutSeconds.HasValue ||
                   request.InputVolume.HasValue ||
                   request.EnableNoiseReduction.HasValue ||
                   request.EnableFrequencyFilters.HasValue;
        }

        private async Task<Result<AudioConfigurationResult>> ApplyCommandLineOverridesAsync(
            Command request,
            AudioOptions currentAudio,
            CancellationToken cancellationToken)
        {
            var updatedAudio = BuildOverriddenAudioOptions(currentAudio, request);

            var saveResult = await sectionStore.WriteSectionAsync(
                AudioOptions.SectionPath,
                updatedAudio,
                cancellationToken);

            if (!saveResult.IsSuccess)
            {
                return Result<AudioConfigurationResult>.Failure($"Failed to save audio configuration: {saveResult.Error}. Changes were not applied.");
            }

            logger.LogInformation("Audio configuration updated via CLI arguments");
            ShowOverrideSummary(request);

            return Result<AudioConfigurationResult>.Success(new AudioConfigurationResult
            {
                Audio = updatedAudio
            });
        }

        private static AudioOptions BuildOverriddenAudioOptions(AudioOptions currentAudio, Command request)
        {
            return new AudioOptions
            {
                Recorder = new RecorderOptions
                {
                    FfmpegPath = currentAudio.Recorder.FfmpegPath,
                    InputVolume = request.InputVolume ?? currentAudio.Recorder.InputVolume,
                    EnableNoiseReduction = request.EnableNoiseReduction ?? currentAudio.Recorder.EnableNoiseReduction,
                    EnableFrequencyFilters = request.EnableFrequencyFilters ?? currentAudio.Recorder.EnableFrequencyFilters
                },
                Preprocessing = new PreprocessingOptions
                {
                    RemoveSilence = currentAudio.Preprocessing.RemoveSilence,
                    SilenceThresholdDb = currentAudio.Preprocessing.SilenceThresholdDb,
                    MinimumSilenceDurationMs = currentAudio.Preprocessing.MinimumSilenceDurationMs
                },
                Timeouts = new RecordingTimeoutsOptions
                {
                    TodaySeconds = currentAudio.Timeouts.TodaySeconds, // Preserve existing value (legacy)
                    RecordSeconds = request.RecordTimeoutSeconds ?? currentAudio.Timeouts.RecordSeconds
                }
            };
        }

        private void ShowOverrideSummary(Command request)
        {
            setupWizard.ShowSuccess("✓ Audio configuration saved successfully");

            if (request.InputVolume.HasValue)
            {
                setupWizard.ShowStatus($"  • Input volume: {request.InputVolume.Value:F1}");
            }

            if (request.EnableNoiseReduction.HasValue)
            {
                setupWizard.ShowStatus($"  • Noise reduction: {(request.EnableNoiseReduction.Value ? "Enabled" : "Disabled")}");
            }

            if (request.EnableFrequencyFilters.HasValue)
            {
                setupWizard.ShowStatus($"  • Frequency filters: {(request.EnableFrequencyFilters.Value ? "Enabled" : "Disabled")}");
            }

            if (request.RecordTimeoutSeconds.HasValue)
            {
                setupWizard.ShowStatus($"  • Record timeout: {request.RecordTimeoutSeconds.Value}s");
            }
        }
    }
}
