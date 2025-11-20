using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.Configuration;

/// <summary>
/// Abstraction for executing configuration operations (show, set, validate)
/// without requiring infrastructure callers to reference feature handlers directly.
/// </summary>
public interface IConfigOperationService
{
    /// <summary>
    /// Executes a configuration operation and returns the resulting display payload.
    /// </summary>
    /// <param name="action">Action to perform (Show, Set, Validate).</param>
    /// <param name="settingName">Optional setting name (Set only).</param>
    /// <param name="settingValue">Optional setting value (Set only).</param>
    /// <param name="showSecrets">Whether to display secrets (Show only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result containing the configuration display or error.</returns>
    Task<Result<ConfigDisplay>> ExecuteAsync(
        ConfigAction action,
        string? settingName,
        string? settingValue,
        bool showSecrets,
        CancellationToken cancellationToken);
}

