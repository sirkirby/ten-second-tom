using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// LLM provider implementation for local OpenAI-compatible APIs (e.g., llama.cpp, Ollama, LM Studio).
/// Sends standard OpenAI Chat Completion JSON payloads to a configurable endpoint.
/// </summary>
public sealed class LocalOpenAiCompatibleLlmProvider : ILlmProvider, ISupportsModelManagement
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LocalOpenAiCompatibleLlmProvider> _logger;
    private readonly string _model;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalOpenAiCompatibleLlmProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="model">The model name to use.</param>
    /// <param name="baseUrl">The base URL of the local server (e.g., "http://127.0.0.1:8080/v1").</param>
    public LocalOpenAiCompatibleLlmProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<LocalOpenAiCompatibleLlmProvider> logger,
        string model,
        string baseUrl)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    /// <inheritdoc/>
    public string ProviderName => "LocalOpenAiCompatible";

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
            var requestUrl = $"{_baseUrl}/chat/completions";

            _logger.LogDebug(
                "Calling Local LLM API at {Url} with model {Model}, maxTokens: {MaxTokens}",
                requestUrl,
                _model,
                maxTokens);

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = maxTokens,
                temperature = temperature
            };

            // Create HttpClient with 15-minute timeout for long-running operations
            // Note: HttpClient from IHttpClientFactory is managed by the factory and must not be disposed by consumer
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(15);

            using var response = await httpClient.PostAsJsonAsync(requestUrl, requestBody, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Local LLM API returned error status {StatusCode}: {Content}", response.StatusCode, errorContent);
                return Result<LlmResponse>.Failure($"Local LLM API error ({response.StatusCode}): {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(cancellationToken: cancellationToken);

            if (result?.Choices is null || result.Choices.Length == 0)
            {
                return Result<LlmResponse>.Failure("Local LLM returned an empty response (no choices)");
            }

            var responseText = result.Choices[0].Message?.Content ?? string.Empty;
            int inputTokens = result.Usage?.PromptTokens ?? 0;
            int outputTokens = result.Usage?.CompletionTokens ?? 0;

            _logger.LogInformation(
                "Local LLM API call successful. Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                inputTokens,
                outputTokens);

            return Result<LlmResponse>.Success(new LlmResponse
            {
                Content = responseText,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling Local LLM API");
            return Result<LlmResponse>.Failure($"Network error: Unable to reach Local LLM at {_baseUrl}. Is the server running?");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local LLM API call failed");
            return Result<LlmResponse>.Failure($"Local LLM error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUrl = $"{_baseUrl}/models";

            _logger.LogDebug("Fetching available models from {Url}", requestUrl);

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            using var response = await httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to fetch models from {Url}: {StatusCode} - {Content}", requestUrl, response.StatusCode, errorContent);
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiModelsResponse>(cancellationToken: cancellationToken);

            if (result?.Data is null)
            {
                _logger.LogWarning("Models endpoint returned empty response");
                return [];
            }

            var modelIds = result.Data.Select(m => m.Id ?? "unknown").ToList();
            _logger.LogDebug("Found {Count} models: {Models}", modelIds.Count, string.Join(", ", modelIds));

            return modelIds;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error fetching models from {BaseUrl}", _baseUrl);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list models");
            return [];
        }
    }

    /// <inheritdoc/>
    public Task<Result> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // OpenAI-compatible APIs don't support model downloads via API
        // Users should use their provider's native tools (Ollama CLI, LM Studio UI, etc.)
        _logger.LogInformation(
            "Model download not supported for LocalOpenAiCompatible provider. Use your provider's native tools to download '{ModelId}'.",
            modelId);

        return Task.FromResult(Result.Failure(
            $"Model downloads are not supported for OpenAI-compatible APIs. " +
            $"Use your provider's native tools to download '{modelId}':\n" +
            $"  • Ollama: ollama pull {modelId}\n" +
            $"  • LM Studio: Use the Models tab in the UI"));
    }

    // Internal DTOs for JSON deserialization
    private sealed class OpenAiChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }

    // DTOs for /v1/models endpoint
    private sealed class OpenAiModelsResponse
    {
        [JsonPropertyName("data")]
        public ModelData[]? Data { get; set; }
    }

    private sealed class ModelData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
