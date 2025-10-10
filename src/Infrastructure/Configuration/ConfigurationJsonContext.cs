using System.Text.Json.Serialization;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// JSON serialization context for configuration storage.
/// Enables source generation for trimmed/AOT scenarios.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class ConfigurationJsonContext : JsonSerializerContext
{
}
