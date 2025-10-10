using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Interface for storing and loading configuration
/// Supports multiple storage backends with fallback
/// </summary>
public interface IConfigurationStorageService
{
    /// <summary>
    /// Saves configuration to storage
    /// </summary>
    /// <param name="settings">Configuration to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result<string>> SaveAsync(ConfigurationSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Loads configuration from storage
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration settings if found</returns>
    Task<Result<ConfigurationSettings>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current storage location being used
    /// </summary>
    string GetStorageLocation();
}
