using System.Text.Json.Serialization;

namespace TenSecondTom.Shared.OutputFormatters;

/// <summary>
/// JSON serialization context for CLI output.
/// Enables source generation for trimmed/AOT scenarios.
/// </summary>
[JsonSerializable(typeof(JsonOutputFormatter.JsonOutput))]
[JsonSerializable(typeof(TenSecondTom.Infrastructure.Cli.SearchResultData))]
[JsonSerializable(typeof(TenSecondTom.Infrastructure.Cli.SearchResultEntry))]
[JsonSerializable(typeof(List<TenSecondTom.Infrastructure.Cli.SearchResultEntry>))]
internal partial class JsonOutputContext : JsonSerializerContext
{
}
