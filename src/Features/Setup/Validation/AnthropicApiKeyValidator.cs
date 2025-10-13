using System.Diagnostics;
using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Validation;

/// <summary>
/// Validates Anthropic API keys
/// Format: ^sk-ant-[a-zA-Z0-9\-_]{32,}$
/// Network: Minimal API call to verify key works
/// </summary>
public sealed partial class AnthropicApiKeyValidator : IApiKeyValidator
{
    private readonly ILogger<AnthropicApiKeyValidator> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    [GeneratedRegex(@"^sk-ant-[a-zA-Z0-9\-_]{32,}$", RegexOptions.Compiled)]
    private static partial Regex AnthropicKeyPattern();

    public AnthropicApiKeyValidator(
        ILogger<AnthropicApiKeyValidator> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public LlmProvider Provider => LlmProvider.Anthropic;

    public Task<ApiValidationResult> ValidateFormatAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(ApiValidationResult.FormatFailure("API key cannot be empty"));
        }

        var isValid = AnthropicKeyPattern().IsMatch(apiKey);

        if (!isValid)
        {
            _logger.LogWarning("Anthropic API key format validation failed");
            return Task.FromResult(ApiValidationResult.FormatFailure(
                "Invalid Anthropic API key format. Expected format: sk-ant-[32+ alphanumeric/hyphen/underscore characters]"));
        }

        _logger.LogDebug("Anthropic API key format validation passed");
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

        _logger.LogInformation("Starting Anthropic API key network validation (max {MaxRetries} retries)", maxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Anthropic API key validation cancelled");
                stopwatch.Stop();
                return ApiValidationResult.NetworkFailure(
                    "Validation was cancelled",
                    stopwatch.Elapsed,
                    retryCount);
            }

            try
            {
                _logger.LogDebug("Anthropic API key validation attempt {Attempt} of {MaxAttempts}", 
                    attempt + 1, maxRetries + 1);

                // Create Anthropic client with minimal request to test key
                var client = new AnthropicClient(new APIAuthentication(apiKey));
                
                // Make a minimal request to validate the key
                var parameters = new MessageParameters
                {
                    Messages = [new Message
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = "test" }]
                    }],
                    MaxTokens = 1,
                    Model = "claude-3-5-sonnet-20241022",
                    Stream = false
                };

                var response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken).ConfigureAwait(false);

                if (response != null)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("Anthropic API key validation successful after {Attempts} attempts", attempt + 1);
                    return ApiValidationResult.Success(stopwatch.Elapsed, retryCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                retryCount++;
                _logger.LogWarning(ex, "Anthropic API key validation attempt {Attempt} failed", attempt + 1);

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
                        $"Anthropic API key validation failed after {maxRetries + 1} attempts: {ex.Message}",
                        stopwatch.Elapsed,
                        retryCount);
                }
            }
        }

        stopwatch.Stop();
        return ApiValidationResult.NetworkFailure(
            "Anthropic API key validation failed: Unknown error",
            stopwatch.Elapsed,
            retryCount);
    }
}
