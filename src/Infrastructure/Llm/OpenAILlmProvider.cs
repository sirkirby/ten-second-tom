using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// OpenAI LLM provider implementation using the official OpenAI .NET SDK.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging calls, delegate overhead not justified")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top-level handler converts all exceptions to Result")]
public sealed class OpenAILlmProvider : ILlmProvider
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAILlmProvider> _logger;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAILlmProvider"/> class.
    /// </summary>
    /// <param name="chatClient">The OpenAI chat client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="model">The model name to use (e.g., "gpt-4").</param>
    public OpenAILlmProvider(
        ChatClient chatClient,
        ILogger<OpenAILlmProvider> logger,
        string model)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc/>
    public string ProviderName => "OpenAI";

    /// <inheritdoc/>
    public async Task<Result<string>> GenerateCompletionAsync(
        string prompt,
        CancellationToken cancellationToken,
        int? maxTokens = null,
        double? temperature = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result<string>.Failure("Prompt cannot be empty");
        }

        try
        {
            _logger.LogDebug(
                "Calling OpenAI API with model {Model}, maxTokens: {MaxTokens}, temperature: {Temperature}",
                _model,
                maxTokens,
                temperature);

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = maxTokens,
                Temperature = temperature.HasValue ? (float)temperature.Value : null
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                options,
                cancellationToken).ConfigureAwait(false);

            string responseText = string.Join("", completion.Content.Select(c => c.Text));

            _logger.LogInformation(
                "OpenAI API call successful. Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                completion.Usage?.InputTokenCount ?? 0,
                completion.Usage?.OutputTokenCount ?? 0);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return Result<string>.Failure("OpenAI returned an empty response");
            }

            return Result<string>.Success(responseText);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OpenAI API call was cancelled");
            return Result<string>.Failure("Operation was cancelled");
        }
        catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "OpenAI rate limit exceeded");
            return Result<string>.Failure("Rate limit exceeded. Please try again later.");
        }
        catch (Exception ex) when (ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("api key", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "OpenAI authentication failed");
            return Result<string>.Failure("Authentication failed. Please check your API key.");
        }
        catch (Exception ex) when (ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                                   ex is HttpRequestException)
        {
            _logger.LogError(ex, "Network error calling OpenAI API");
            return Result<string>.Failure("Network error occurred. Please check your connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling OpenAI API");
            return Result<string>.Failure($"Failed to generate completion: {ex.Message}");
        }
    }
}
