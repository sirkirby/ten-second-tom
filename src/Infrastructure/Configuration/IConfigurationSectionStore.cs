using System.Text.Json;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Generic configuration section storage interface with NO feature knowledge.
/// Provides type-safe access to any configuration section in config.json.
/// All operations are thread-safe and support atomic writes.
/// </summary>
/// <remarks>
/// This interface is intentionally generic and feature-agnostic.
/// It operates purely on JSON section paths and types, without knowledge of
/// AudioConfiguration, SshConfiguration, or any domain models.
///
/// Section paths use colon-separated notation:
/// - "TenSecondTom:Audio" navigates to root["TenSecondTom"]["Audio"]
/// - "TenSecondTom:Ssh:KeyPath" navigates to root["TenSecondTom"]["Ssh"]["KeyPath"]
///
/// Thread safety: All methods use internal locking for concurrent access.
/// Atomic writes: Updates use temp file + File.Move to prevent corruption.
/// Section preservation: Writing one section preserves all other sections.
/// </remarks>
public interface IConfigurationSectionStore : IDisposable
{
    /// <summary>
    /// Reads a configuration section and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the section into.</typeparam>
    /// <param name="sectionPath">Colon-separated path to the section (e.g., "TenSecondTom:Audio").</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// Success with deserialized section if found, or default instance of T if section doesn't exist.
    /// Failure if file cannot be read or JSON is invalid.
    /// </returns>
    /// <remarks>
    /// If the section doesn't exist in the file, returns a default instance using new T().
    /// This allows callers to safely read configuration sections even if not yet created.
    /// </remarks>
    Task<Result<T>> ReadSectionAsync<T>(string sectionPath, CancellationToken cancellationToken = default)
        where T : new();

    /// <summary>
    /// Writes a configuration section, preserving all other sections in the file.
    /// </summary>
    /// <typeparam name="T">The type of the configuration section to write.</typeparam>
    /// <param name="sectionPath">Colon-separated path to the section (e.g., "TenSecondTom:Audio").</param>
    /// <param name="config">The configuration object to serialize and write.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// Success with the config file path on successful write.
    /// Failure if write operation fails or section path is invalid.
    /// </returns>
    /// <remarks>
    /// Write process:
    /// 1. Loads existing config.json (or creates empty root if file doesn't exist)
    /// 2. Navigates to section path, creating intermediate objects as needed
    /// 3. Replaces section value with serialized config
    /// 4. Writes to temp file atomically
    /// 5. Moves temp file over original (atomic operation)
    ///
    /// All other sections in the file are preserved.
    /// </remarks>
    Task<Result<string>> WriteSectionAsync<T>(
        string sectionPath,
        T config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the entire configuration file as a JsonDocument for advanced scenarios.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// Success with JsonDocument representing entire config file.
    /// Failure if file cannot be read or JSON is invalid.
    /// </returns>
    /// <remarks>
    /// Caller is responsible for disposing the JsonDocument.
    /// Use this method for advanced scenarios where you need to inspect
    /// the entire configuration structure or perform complex queries.
    /// </remarks>
    Task<Result<JsonDocument>> ReadFullConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple configuration sections atomically in a single operation.
    /// </summary>
    /// <param name="sections">Dictionary mapping section paths to configuration objects.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// Success with config file path if all sections written successfully.
    /// Failure if any section write fails (no partial writes - all or nothing).
    /// </returns>
    /// <remarks>
    /// All sections are written in a single atomic operation.
    /// If any section fails, the entire operation is rolled back.
    ///
    /// Example:
    /// var sections = new Dictionary&lt;string, object&gt;
    /// {
    ///     ["TenSecondTom:Audio"] = audioConfig,
    ///     ["TenSecondTom:Ssh"] = sshConfig
    /// };
    /// await store.WriteMultipleSectionsAsync(sections);
    /// </remarks>
    Task<Result<string>> WriteMultipleSectionsAsync(
        Dictionary<string, object> sections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the absolute path to the configuration file.
    /// </summary>
    /// <returns>Absolute path to config.json.</returns>
    string GetConfigPath();
}
