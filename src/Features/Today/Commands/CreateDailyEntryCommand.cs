using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Commands;

/// <summary>
/// Marker interface for request/response pattern.
/// Indicates this command returns a specific response type.
/// </summary>
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Command to create a daily reflection entry.
/// Captures user's daily content and generates structured summary via LLM.
/// </summary>
public sealed record CreateDailyEntryCommand : IRequest<Result<DailyEntry>>
{
    /// <summary>
    /// Gets the user's daily reflection content.
    /// Can be free-form text, multiple lines, or structured as the user prefers.
    /// Must not be null, empty, or whitespace-only.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the optional template name to use for processing the daily entry.
    /// If specified, the handler will attempt to load this template.
    /// If the template is not found, falls back to the default template with a warning.
    /// </summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use the default template.
    /// When true, bypasses template selection UI and uses the default daily summary template directly.
    /// Useful for non-interactive scenarios or when the user prefers the default template.
    /// </summary>
    public bool UseDefaultTemplate { get; init; }

    /// <summary>
    /// Gets the optional LLM provider override.
    /// If not specified, uses the default provider from configuration.
    /// Valid values: "OpenAI", "Anthropic".
    /// </summary>
    public string? LlmProviderOverride { get; init; }
}
