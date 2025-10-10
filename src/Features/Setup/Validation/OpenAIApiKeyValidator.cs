using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Validation;

/// <summary>
/// Validates OpenAI API keys
/// Format: ^sk-[a-zA-Z0-9]{48,}$
/// Network: GET /v1/models endpoint
/// </summary>
public sealed partial class OpenAIApiKeyValidator : IApiKeyValidator
{
    private readonly ILogger<OpenAIApiKeyValidator> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    [GeneratedRegex(@"^sk-[a-zA-Z0-9]{48,}$", RegexOptions.Compiled)]
    private static partial Regex OpenAIKeyPattern();

    public OpenAIApiKeyValidator(
        ILogger<OpenAIApiKeyValidator> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public LlmProvider Provider => LlmProvider.OpenAI;

    public Task<ApiValidationResult> ValidateFormatAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(ApiValidationResult.FormatFailure("API key cannot be empty"));
        }

        var isValid = OpenAIKeyPattern().IsMatch(apiKey);

        if (!isValid)
        {
            _logger.LogWarning("OpenAI API key format validation failed");
            return Task.FromResult(ApiValidationResult.FormatFailure(
                "Invalid OpenAI API key format. Expected format: sk-[48+ alphanumeric characters]"));
        }

        _logger.LogDebug("OpenAI API key format validation passed");
        return Task.FromResult(new ApiValidationResult
        {
            IsValid = true,
            FormatValid = true,
            NetworkValid = false,
            Duration = TimeSpan.Zero
        });
    }

    public async Task<ApiValidationResult> ValidateNetworkAsync(
        string apiKey,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryCount = 0;

        _logger.LogInformation("Starting OpenAI API key network validation (max {MaxRetries} retries)", maxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("OpenAI API key validation cancelled");
                stopwatch.Stop();
                return ApiValidationResult.NetworkFailure(
                    "Validation was cancelled",
                    stopwatch.Elapsed,
                    retryCount);
            }

            try
            {
                _logger.LogDebug("OpenAI API key validation attempt {Attempt} of {MaxAttempts}", 
                    attempt + 1, maxRetries + 1);

                // Create ChatClient and test with a minimal request
                var apiKeyCredential = new System.ClientModel.ApiKeyCredential(apiKey);
                var client = new ChatClient("gpt-3.5-turbo", apiKeyCredential);
                
                // Make a minimal request to validate the key
                var messages = new[]
                {
                    new SystemChatMessage("test"),
                };

                var completion = await client.CompleteChatAsync(
                    messages, 
                    new ChatCompletionOptions { MaxOutputTokenCount = 1 },
                    cancellationToken).ConfigureAwait(false);

                if (completion?.Value != null)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("OpenAI API key validation successful after {Attempts} attempts", attempt + 1);
                    return ApiValidationResult.Success(stopwatch.Elapsed, retryCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                retryCount++;
                _logger.LogWarning(ex, "OpenAI API key validation attempt {Attempt} failed", attempt + 1);

                if (attempt < maxRetries)
                {
                    // Exponential backoff: 1s, 2s, 4s, 8s...
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogDebug("Waiting {Delay}s before retry", delay.TotalSeconds);
                    
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        stopwatch.Stop();
                        return ApiValidationResult.NetworkFailure(
                            "Validation was cancelled during retry delay",
                            stopwatch.Elapsed,
                            retryCount);
                    }
                }
                else
                {
                    stopwatch.Stop();
                    return ApiValidationResult.NetworkFailure(
                        $"OpenAI API key validation failed after {maxRetries + 1} attempts: {ex.Message}",
                        stopwatch.Elapsed,
                        retryCount);
                }
            }
        }

        stopwatch.Stop();
        return ApiValidationResult.NetworkFailure(
            "OpenAI API key validation failed: Unknown error",
            stopwatch.Elapsed,
            retryCount);
    }
}
