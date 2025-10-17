using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// OpenAI LLM provider implementation using the official OpenAI .NET SDK.
/// </summary>
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
    public string ModelName => _model;

    /// <inheritdoc/>
    public async Task<Result<LlmResponse>> GenerateCompletionAsync(
        string prompt,
        CancellationToken cancellationToken,
        int? maxTokens = null,
        double? temperature = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result<LlmResponse>.Failure("Prompt cannot be empty");
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

            int inputTokens = completion.Usage?.InputTokenCount ?? 0;
            int outputTokens = completion.Usage?.OutputTokenCount ?? 0;

            _logger.LogInformation(
                "OpenAI API call successful. Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                inputTokens,
                outputTokens);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return Result<LlmResponse>.Failure("OpenAI returned an empty response");
            }

            var response = new LlmResponse
            {
                Content = responseText,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            };

            return Result<LlmResponse>.Success(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OpenAI API call was cancelled");
            return Result<LlmResponse>.Failure("Operation was cancelled");
        }
        catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "OpenAI rate limit exceeded");
            return Result<LlmResponse>.Failure("Rate limit exceeded. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed");
            return Result<LlmResponse>.Failure($"OpenAI API error: {ex.Message}");
        }
    }
}
