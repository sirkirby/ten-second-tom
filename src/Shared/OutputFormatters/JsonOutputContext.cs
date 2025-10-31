using System.Text.Json.Serialization;

namespace TenSecondTom.Shared.OutputFormatters;

/// <summary>
/// JSON serialization context for CLI output.
/// Enables source generation for trimmed/AOT scenarios.
/// </summary>
[JsonSerializable(typeof(JsonOutputFormatter.JsonOutput))]
[JsonSerializable(typeof(TenSecondTom.Features.Search.Models.SearchResultData))]
[JsonSerializable(typeof(TenSecondTom.Features.Search.Models.SearchResultEntry))]
[JsonSerializable(typeof(List<TenSecondTom.Features.Search.Models.SearchResultEntry>))]
internal partial class JsonOutputContext : JsonSerializerContext
{
}
