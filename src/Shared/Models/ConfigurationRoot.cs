using System.Text.Json;
using System.Text.Json.Serialization;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Root configuration file wrapper (config.json).
/// Preserves non-TenSecondTom sections (e.g., Serilog) when saving/loading configuration.
/// The TenSecondTom section is stored as a raw JsonElement and mapped to/from ConfigurationSettings.
/// </summary>
public sealed class ConfigurationRoot
{
    /// <summary>
    /// The TenSecondTom configuration section as raw JSON.
    /// Used for advanced JSON operations and configuration inspection.
    /// </summary>
    [JsonPropertyName("TenSecondTom")]
    public JsonElement TenSecondTom { get; set; }

    /// <summary>
    /// Extension data to preserve other configuration sections (like Serilog, Logging, etc.)
    /// when roundtripping configuration through save/load cycles.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
