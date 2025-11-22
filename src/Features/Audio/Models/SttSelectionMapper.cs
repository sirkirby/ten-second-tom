using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Helper methods for translating CLI STT preferences to strongly typed options.
/// </summary>
public static class SttSelectionMapper
{
    private const string ValidOptions = "Valid options: auto, local, openai";

    /// <summary>
    /// Attempts to parse user input into a <see cref="SttSelection"/> value.
    /// Defaults to <see cref="SttSelection.Auto"/> when no CLI value is supplied.
    /// </summary>
    public static bool TryParse(string? cliValue, out SttSelection selection, out string? error)
    {
        if (string.IsNullOrWhiteSpace(cliValue))
        {
            selection = SttSelection.Auto;
            error = null;
            return true;
        }

        if (Enum.TryParse(cliValue, ignoreCase: true, out selection))
        {
            error = null;
            return true;
        }

        selection = SttSelection.Auto;
        error = $"Invalid STT selection: {cliValue}. {ValidOptions}";
        return false;
    }

    /// <summary>
    /// Builds an <see cref="AudioOptions"/> instance that honors the CLI selection.
    /// </summary>
    public static AudioOptions BuildAudioOptions(SttSelection selection, AudioOptions baseOptions)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);

        return selection switch
        {
            SttSelection.Auto => new AudioOptions
            {
                SttProvider = baseOptions.SttProvider,
                SttApiKey = baseOptions.SttApiKey,
                SttFallbackEnabled = true,
                SttFallbackProvider = baseOptions.SttFallbackProvider,
                SttFallbackApiKey = baseOptions.SttFallbackApiKey,
                SttBinaryPath = baseOptions.SttBinaryPath,
                SttModel = baseOptions.SttModel,
                SttFallbackBinaryPath = baseOptions.SttFallbackBinaryPath,
                SttFallbackModel = baseOptions.SttFallbackModel,
                KeepFiles = baseOptions.KeepFiles,
                Recorder = baseOptions.Recorder,
                Preprocessing = baseOptions.Preprocessing,
                Timeouts = baseOptions.Timeouts
            },
            SttSelection.Local => new AudioOptions
            {
                SttProvider = baseOptions.SttProvider,
                SttApiKey = baseOptions.SttApiKey,
                SttFallbackEnabled = false,
                SttBinaryPath = baseOptions.SttBinaryPath,
                SttModel = baseOptions.SttModel,
                KeepFiles = baseOptions.KeepFiles,
                Recorder = baseOptions.Recorder,
                Preprocessing = baseOptions.Preprocessing,
                Timeouts = baseOptions.Timeouts
            },
            SttSelection.OpenAI => new AudioOptions
            {
                SttProvider = SttProviders.OpenAI,
                SttApiKey = baseOptions.SttApiKey,
                SttFallbackEnabled = false,
                SttBinaryPath = baseOptions.SttBinaryPath,
                SttModel = baseOptions.SttModel,
                KeepFiles = baseOptions.KeepFiles,
                Recorder = baseOptions.Recorder,
                Preprocessing = baseOptions.Preprocessing,
                Timeouts = baseOptions.Timeouts
            },
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, "Unsupported STT selection")
        };
    }
}
