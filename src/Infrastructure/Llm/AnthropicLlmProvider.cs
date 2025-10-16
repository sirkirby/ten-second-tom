using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// Anthropic LLM provider implementation using the Anthropic.SDK.
/// </summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AnthropicLlmProvider> _logger;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicLlmProvider"/> class.
    /// </summary>
    /// <param name="client">The Anthropic client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="model">The model name to use (e.g., "claude-3-sonnet-20240229").</param>
    public AnthropicLlmProvider(
        AnthropicClient client,
        ILogger<AnthropicLlmProvider> logger,
        string model)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc/>
    public string ProviderName => "Anthropic";

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
                "Calling Anthropic API with model {Model}, maxTokens: {MaxTokens}, temperature: {Temperature}",
                _model,
                maxTokens,
                temperature);

            var parameters = new MessageParameters
            {
                Messages =
                [
                    new Message
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = prompt }]
                    }
                ],
                MaxTokens = maxTokens ?? 2000, // Anthropic requires max_tokens
                Model = _model,
                Stream = false,
                Temperature = temperature.HasValue ? (decimal)temperature.Value : null
            };

            MessageResponse response = await _client.Messages.GetClaudeMessageAsync(
                parameters,
                cancellationToken).ConfigureAwait(false);

            string responseText = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;

            _logger.LogInformation(
                "Anthropic API call successful. Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                response.Usage?.InputTokens ?? 0,
                response.Usage?.OutputTokens ?? 0);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return Result<string>.Failure("Anthropic returned an empty response");
            }

            return Result<string>.Success(responseText);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Anthropic API call was cancelled");
            return Result<string>.Failure("Operation was cancelled");
        }
        catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("429", StringComparison.Ordinal))
        {
            _logger.LogError(ex, "Anthropic rate limit exceeded");
            return Result<string>.Failure("Rate limit exceeded. Please try again later.");
        }
        catch (Exception ex) when (ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("api key", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("401", StringComparison.Ordinal))
        {
            _logger.LogError(ex, "Anthropic authentication failed");
            return Result<string>.Failure("Authentication failed. Please check your API key.");
        }
        catch (Exception ex) when (ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                                   ex is HttpRequestException)
        {
            _logger.LogError(ex, "Network error calling Anthropic API");
            return Result<string>.Failure("Network error occurred. Please check your connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Anthropic API");
            return Result<string>.Failure($"Failed to generate completion: {ex.Message}");
        }
    }
}
