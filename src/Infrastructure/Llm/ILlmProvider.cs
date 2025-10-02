using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// Defines the contract for LLM (Large Language Model) providers.
/// Abstracts different LLM services (OpenAI, Anthropic, etc.) behind a common interface.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public interface ILlmProvider
{
    /// <summary>
    /// Gets the name of the LLM provider (e.g., "OpenAI", "Anthropic").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Generates a text completion from the LLM based on the provided prompt.
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <param name="maxTokens">Optional maximum number of tokens to generate.</param>
    /// <param name="temperature">Optional temperature parameter (0.0-1.0) controlling randomness.</param>
    /// <returns>Result containing the completion text on success, or error message on failure.</returns>
    Task<Result<string>> GenerateCompletionAsync(
        string prompt,
        CancellationToken cancellationToken,
        int? maxTokens = null,
        double? temperature = null);
}
