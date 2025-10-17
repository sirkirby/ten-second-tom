namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// Represents a response from an LLM provider including content and usage metadata.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Gets the generated text content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the number of tokens used in the input/prompt.
    /// </summary>
    public required int InputTokens { get; init; }

    /// <summary>
    /// Gets the number of tokens generated in the output.
    /// </summary>
    public required int OutputTokens { get; init; }

    /// <summary>
    /// Gets the total number of tokens used (input + output).
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
