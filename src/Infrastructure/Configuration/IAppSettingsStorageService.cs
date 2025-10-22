using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Interface for managing appsettings.json file updates.
/// Provides atomic updates to audio configuration sections.
/// </summary>
public interface IAppSettingsStorageService
{
    /// <summary>
    /// Updates the audio configuration section in appsettings.json.
    /// </summary>
    /// <param name="audioConfig">Audio configuration to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the file path where configuration was saved</returns>
    Task<Result<string>> SaveAudioConfigurationAsync(
        AudioConfiguration audioConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the current audio configuration from appsettings.json.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the audio configuration or an error</returns>
    Task<Result<AudioConfiguration>> LoadAudioConfigurationAsync(
        CancellationToken cancellationToken = default);
}
