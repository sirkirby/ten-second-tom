using TenSecondTom.Features.Audio.Constants;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Audio.Models;
using static TenSecondTom.Features.Audio.Constants.SttProviders;

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
    public sealed record Command : IRequest<Result<AudioOptions>>
    {
        /// <summary>
        /// Gets the timeout in seconds for 'today --voice' recording.
        /// When provided, skips the interactive prompt for this setting.
        /// </summary>
        public int? TodayTimeoutSeconds { get; init; }

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
            RuleFor(x => x.TodayTimeoutSeconds)
                .InclusiveBetween(AudioConstants.MinTodayTimeoutSeconds, AudioConstants.MaxTodayTimeoutSeconds)
                .When(x => x.TodayTimeoutSeconds.HasValue)
                .WithMessage($"Today timeout must be between {AudioConstants.MinTodayTimeoutSeconds} and {AudioConstants.MaxTodayTimeoutSeconds} seconds.");

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
        : IRequestHandler<Command, Result<AudioOptions>>
    {
        public async Task<Result<AudioOptions>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting audio configuration");

            // Read current audio configuration directly from config.json to get the latest saved values
            // Cannot use IOptions/IOptionsSnapshot as they read from appsettings.json, not the user's config.json
            var currentConfigResult = await sectionStore.ReadSectionAsync<AudioOptions>(
                AudioOptions.SectionPath,
                cancellationToken);

            if (!currentConfigResult.IsSuccess)
            {
                return Result<AudioOptions>.Failure($"Failed to load current audio configuration: {currentConfigResult.Error}");
            }

            var currentAudio = currentConfigResult.Value;

            if (HasCommandLineOverrides(request))
            {
                return await ApplyCommandLineOverridesAsync(request, currentAudio, cancellationToken);
            }

            const int totalSteps = 9;

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
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
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
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
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
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
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
                return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
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
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
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
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                }
                minSilenceDurationMs = duration.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(6, totalSteps, "Minimum Silence Duration");
                setupWizard.ShowStatus("Skipped (silence removal disabled)");
            }

            // Step 7: Speech-to-Text Provider
            setupWizard.ShowStepHeader(7, totalSteps, "Speech-to-Text Provider");
            var sttProvider = await setupWizard.PromptForSttProviderAsync(
                currentAudio.SttProvider,
                cancellationToken);

            if (sttProvider == null)
            {
                return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
            }

            // Step 7a: STT API Key (if provider requires it)
            string? sttApiKey = currentAudio.SttApiKey;
            if (sttProvider == SttProviders.OpenAI)
            {
                sttApiKey = await setupWizard.PromptForSttApiKeyAsync(
                    sttProvider,
                    currentAudio.SttApiKey,
                    cancellationToken);

                if (sttApiKey == null)
                {
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Step 7b: STT Fallback Provider
            bool sttFallbackEnabled = currentAudio.SttFallbackEnabled;
            string? sttFallbackProvider = currentAudio.SttFallbackProvider;
            string? sttFallbackApiKey = currentAudio.SttFallbackApiKey;

            if (sttProvider == SttProviders.WhisperCpp)
            {
                var fallback = await setupWizard.PromptForSttFallbackAsync(
                    currentAudio.SttFallbackEnabled,
                    cancellationToken);

                if (!fallback.HasValue)
                {
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                }

                sttFallbackEnabled = fallback.Value;

                // If fallback is enabled, prompt for provider and API key
                if (sttFallbackEnabled)
                {
                    // Prompt for fallback provider
                    var fallbackProvider = await setupWizard.PromptForSttFallbackProviderAsync(
                        currentAudio.SttFallbackProvider,
                        cancellationToken);

                    if (fallbackProvider == null)
                    {
                        return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                    }

                    sttFallbackProvider = fallbackProvider;

                    // Prompt for fallback API key
                    setupWizard.ShowStatus($"Fallback provider '{fallbackProvider}' requires an API key.");

                    var fallbackApiKey = await setupWizard.PromptForSttApiKeyAsync(
                        fallbackProvider,
                        currentAudio.SttFallbackApiKey,
                        cancellationToken);

                    if (fallbackApiKey == null)
                    {
                        return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                    }

                    sttFallbackApiKey = fallbackApiKey;
                }
                else
                {
                    // Fallback is disabled, clear the provider and API key
                    sttFallbackProvider = null;
                    sttFallbackApiKey = null;
                }
            }

            // Step 8: Today Voice Timeout
            int? todayTimeout;
            if (request.TodayTimeoutSeconds.HasValue)
            {
                setupWizard.ShowStepHeader(8, totalSteps, "Today Voice Recording Timeout");
                setupWizard.ShowStatus($"Using provided value: {request.TodayTimeoutSeconds.Value} seconds");
                todayTimeout = request.TodayTimeoutSeconds.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(8, totalSteps, "Today Voice Recording Timeout");
                setupWizard.ShowStatus("When this duration is reached, you'll be prompted to continue or finish recording.");
                todayTimeout = await setupWizard.PromptForIntAsync(
                    $"Time before prompting to continue 'today --voice' (seconds, {AudioConstants.MinTodayTimeoutSeconds}-{AudioConstants.MaxTodayTimeoutSeconds}):",
                    currentAudio.Timeouts.TodaySeconds,
                    AudioConstants.MinTodayTimeoutSeconds,
                    AudioConstants.MaxTodayTimeoutSeconds,
                    cancellationToken);

                if (!todayTimeout.HasValue)
                {
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Step 9: Record Command Timeout
            int? recordTimeout;
            if (request.RecordTimeoutSeconds.HasValue)
            {
                setupWizard.ShowStepHeader(9, totalSteps, "Record Command Timeout");
                setupWizard.ShowStatus($"Using provided value: {request.RecordTimeoutSeconds.Value} seconds");
                recordTimeout = request.RecordTimeoutSeconds.Value;
            }
            else
            {
                setupWizard.ShowStepHeader(9, totalSteps, "Record Command Timeout");
                setupWizard.ShowStatus("When this duration is reached, you'll be prompted to continue or finish recording.");
                recordTimeout = await setupWizard.PromptForIntAsync(
                    $"Time before prompting to continue 'record' (seconds, {AudioConstants.MinRecordTimeoutSeconds}-{AudioConstants.MaxRecordTimeoutSeconds}):",
                    currentAudio.Timeouts.RecordSeconds,
                    AudioConstants.MinRecordTimeoutSeconds,
                    AudioConstants.MaxRecordTimeoutSeconds,
                    cancellationToken);

                if (!recordTimeout.HasValue)
                {
                    return Result<AudioOptions>.Failure("Audio configuration cancelled. No changes were made.");
                }
            }

            // Build updated audio configuration using AudioOptions
            var updatedAudio = new AudioOptions
            {
                SttProvider = sttProvider,
                SttApiKey = sttApiKey,
                SttFallbackEnabled = sttFallbackEnabled,
                SttFallbackProvider = sttFallbackProvider,
                SttFallbackApiKey = sttFallbackApiKey,
                SttBinaryPath = currentAudio.SttBinaryPath,
                SttModel = currentAudio.SttModel,
                SttFallbackBinaryPath = currentAudio.SttFallbackBinaryPath,
                SttFallbackModel = currentAudio.SttFallbackModel,
                KeepFiles = currentAudio.KeepFiles,
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
                    TodaySeconds = todayTimeout.Value,
                    RecordSeconds = recordTimeout.Value
                }
            };

            // Save configuration directly to config.json
            var saveResult = await sectionStore.WriteSectionAsync(
                AudioOptions.SectionPath,
                updatedAudio,
                cancellationToken);

            if (!saveResult.IsSuccess)
            {
                return Result<AudioOptions>.Failure($"Failed to save audio configuration: {saveResult.Error}. Changes were not applied.");
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
            var sttProviderDisplay = sttProvider == WhisperCpp ? "whisper.cpp (local)" : "OpenAI Whisper API (cloud)";
            setupWizard.ShowStatus($"  • STT provider: {sttProviderDisplay}");
            if (sttFallbackEnabled)
            {
                setupWizard.ShowStatus($"  • STT fallback provider: Enabled ({sttFallbackProvider})");
            }
            setupWizard.ShowStatus($"  • Today timeout: {todayTimeout.Value}s");
            setupWizard.ShowStatus($"  • Record timeout: {recordTimeout.Value}s");

            return Result<AudioOptions>.Success(updatedAudio);
        }

        private static bool HasCommandLineOverrides(Command request)
        {
            return request.TodayTimeoutSeconds.HasValue ||
                   request.RecordTimeoutSeconds.HasValue ||
                   request.InputVolume.HasValue ||
                   request.EnableNoiseReduction.HasValue ||
                   request.EnableFrequencyFilters.HasValue;
        }

        private async Task<Result<AudioOptions>> ApplyCommandLineOverridesAsync(
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
                return Result<AudioOptions>.Failure($"Failed to save audio configuration: {saveResult.Error}. Changes were not applied.");
            }

            logger.LogInformation("Audio configuration updated via CLI arguments");
            ShowOverrideSummary(request);

            return Result<AudioOptions>.Success(updatedAudio);
        }

        private static AudioOptions BuildOverriddenAudioOptions(AudioOptions currentAudio, Command request)
        {
            return new AudioOptions
            {
                SttProvider = currentAudio.SttProvider,
                SttApiKey = currentAudio.SttApiKey,
                SttFallbackEnabled = currentAudio.SttFallbackEnabled,
                SttFallbackProvider = currentAudio.SttFallbackProvider,
                SttFallbackApiKey = currentAudio.SttFallbackApiKey,
                SttBinaryPath = currentAudio.SttBinaryPath,
                SttModel = currentAudio.SttModel,
                SttFallbackBinaryPath = currentAudio.SttFallbackBinaryPath,
                SttFallbackModel = currentAudio.SttFallbackModel,
                KeepFiles = currentAudio.KeepFiles,
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
                    TodaySeconds = request.TodayTimeoutSeconds ?? currentAudio.Timeouts.TodaySeconds,
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

            if (request.TodayTimeoutSeconds.HasValue)
            {
                setupWizard.ShowStatus($"  • Today timeout: {request.TodayTimeoutSeconds.Value}s");
            }

            if (request.RecordTimeoutSeconds.HasValue)
            {
                setupWizard.ShowStatus($"  • Record timeout: {request.RecordTimeoutSeconds.Value}s");
            }
        }
    }
}

