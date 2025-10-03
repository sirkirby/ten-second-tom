using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Commands;

/// <summary>
/// Marker interface for request/response pattern.
/// Indicates this command returns a specific response type.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker interface for CQRS pattern")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Command to create a daily reflection entry.
/// Captures user responses to daily prompts and generates structured summary via LLM.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public sealed record CreateDailyEntryCommand : IRequest<Result<DailyEntry>>
{
    /// <summary>
    /// Gets the user's responses to daily reflection prompts.
    /// Keys are question text, values are user answers.
    /// Must contain 3-5 key-value pairs with non-empty values.
    /// </summary>
    public required Dictionary<string, string> Responses { get; init; }

    /// <summary>
    /// Gets the optional LLM provider override.
    /// If not specified, uses the default provider from configuration.
    /// Valid values: "OpenAI", "Anthropic".
    /// </summary>
    public string? LlmProviderOverride { get; init; }
}
