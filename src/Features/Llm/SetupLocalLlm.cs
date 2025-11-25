using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Llm;

/// <summary>
/// Checks for local LLM prerequisites and connectivity.
/// </summary>
public static class SetupLocalLlm
{
    /// <summary>
    /// Command to verify local LLM setup.
    /// </summary>
    /// <summary>
    /// Command to verify local LLM setup.
    /// </summary>
    public sealed record Command : IRequest<Result>
    {
        public string? BaseUrlOverride { get; init; }
        public string? ModelNameOverride { get; init; }
    }

    /// <summary>
    /// Handler for SetupLocalLlm command.
    /// </summary>
    public sealed class Handler(
        IOptionsSnapshot<LlmOptions> llmOptions,
        IHttpClientFactory httpClientFactory,
        ISetupWizardUI setupWizard,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        private readonly LlmOptions _llmOptions = llmOptions.Value;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            setupWizard.ShowStatus("Checking local LLM connectivity...");

            // Determine Base URL: Override > Configured > Default
            string? baseUrl = request.BaseUrlOverride;
            
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (_llmOptions.Providers != null && 
                    _llmOptions.Providers.TryGetValue("LocalOpenAiCompatible", out var localConfig) &&
                    localConfig.TryGetValue("BaseUrl", out var configUrl))
                {
                    baseUrl = configUrl;
                }
                else
                {
                    baseUrl = "http://127.0.0.1:8080/v1";
                }
            }

            // Determine Model Name
            string modelName = request.ModelNameOverride ?? _llmOptions.GetModel() ?? "local-model";

            try
            {
                // Check connectivity by hitting the /models endpoint
                // Most OpenAI compatible servers support this
                using var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5); // Short timeout for check

                // Ensure base URL doesn't end with slash for cleaner concatenation
                baseUrl = baseUrl.TrimEnd('/');
                
                // Handle /v1 suffix if present or missing - standard OpenAI is /v1/models
                string modelsUrl = baseUrl.EndsWith("/v1") 
                    ? $"{baseUrl}/models" 
                    : $"{baseUrl}/v1/models";

                logger.LogInformation("Verifying local LLM at {Url}", modelsUrl);

                using var response = await client.GetAsync(modelsUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    setupWizard.ShowSuccess($"✓ Successfully connected to local LLM at {baseUrl}");
                    return Result.Success();
                }
                else
                {
                    var error = $"Failed to connect to local LLM. Status: {response.StatusCode}";
                    setupWizard.ShowWarning($"⚠ {error}");
                    setupWizard.ShowWarning("  Please ensure your local server (Ollama, LM Studio, etc.) is running.");
                    return Result.Failure(error);
                }
            }
            catch (Exception ex)
            {
                var error = $"Could not connect to local LLM at {baseUrl}: {ex.Message}";
                logger.LogWarning(ex, "Local LLM connectivity check failed");
                setupWizard.ShowWarning($"⚠ {error}");
                setupWizard.ShowWarning("  Please ensure your local server is running and accessible.");
                return Result.Failure(error);
            }
        }
    }
}
