using System.Linq;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Abstractions.Validation;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Llm;

/// <summary>
/// Configures LLM provider and model settings interactively.
/// This use case handles all LLM configuration logic within the Llm feature slice.
/// </summary>
public static class ConfigureLlm
{
    /// <summary>
    /// Command to configure LLM provider and model settings.
    /// </summary>
    public sealed record Command : IRequest<Result<LlmConfiguration>>
    {
        /// <summary>
        /// Gets whether to force reconfiguration even if already configured.
        /// When true, always runs interactive prompts (user explicitly wants to change config).
        /// When false, skips if already configured (idempotent behavior for setup wizard).
        /// </summary>
        public bool Force { get; init; }

        /// <summary>
        /// Optional provider override for non-interactive configuration.
        /// </summary>
        public LlmProvider? ProviderOverride { get; init; }

        /// <summary>
        /// Optional model override for non-interactive configuration.
        /// </summary>
        public string? ModelOverride { get; init; }

        /// <summary>
        /// Optional API key override for non-interactive configuration.
        /// </summary>
        public string? ApiKeyOverride { get; init; }

        /// <summary>
        /// Optional max input tokens override for non-interactive configuration.
        /// </summary>
        public int? MaxInputTokensOverride { get; init; }
    }

    /// <summary>
    /// Validator for ConfigureLlm command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // No validation needed - command has no parameters
        }
    }

    /// <summary>
    /// Handler for ConfigureLlm command (auto-discovered by MediatR).
    /// Orchestrates interactive LLM provider and model selection.
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ISetupWizardUI setupWizard,
        IHttpClientFactory httpClientFactory,
        IEnumerable<IApiKeyValidator> apiKeyValidators,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<LlmConfiguration>>
    {
        public async Task<Result<LlmConfiguration>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            // Load current LLM configuration from Llm section
            // During first-time setup, no config exists yet - use defaults
            var loadResult = await sectionStore.ReadSectionAsync<LlmOptions>(
                LlmOptions.SectionPath,
                cancellationToken).ConfigureAwait(false);

            var currentLlmConfig = loadResult.Value ?? new LlmOptions
            {
                Provider = LlmProvider.OpenAI, // Default provider for first-time setup
                ApiKey = null,
                Model = null,
                MaxInputTokens = LlmConstants.DefaultMaxInputTokensOpenAI
            };

            if (HasCommandLineOverrides(request))
            {
                return await ApplyCommandLineOverridesAsync(
                    request,
                    currentLlmConfig,
                    cancellationToken).ConfigureAwait(false);
            }

            // Smart handler: If already configured AND not forced, skip interactive prompts (idempotent)
            // When Force=true (direct user invocation), always run interactive prompts
            // When Force=false (setup wizard), skip if already configured
            if (!request.Force && currentLlmConfig.IsConfigured())
            {
                logger.LogInformation("LLM already configured and not forced, skipping interactive setup");
                setupWizard.ShowSuccess($"✓ LLM already configured: {GetProviderName(currentLlmConfig.Provider)} - {currentLlmConfig.Model}");

                // Return existing configuration
                var existingConfig = new LlmConfiguration
                {
                    Provider = currentLlmConfig.Provider,
                    ApiKey = currentLlmConfig.ApiKey,
                    Model = currentLlmConfig.Model,
                    MaxInputTokens = currentLlmConfig.MaxInputTokens ?? (currentLlmConfig.Provider == LlmProvider.Anthropic
                        ? LlmConstants.DefaultMaxInputTokensAnthropic
                        : LlmConstants.DefaultMaxInputTokensOpenAI)
                };

                return Result<LlmConfiguration>.Success(existingConfig);
            }

            logger.LogInformation("Starting interactive LLM configuration");

            // Pre-calculate if we'll need API key prompt (needed to show accurate step count)
            // We'll need API key if it's missing - provider change check happens after selection
            bool hasApiKey = !string.IsNullOrWhiteSpace(currentLlmConfig.ApiKey);

            // Step 1: Prompt for LLM provider
            // Show max possible steps (3) - will adjust totalSteps after provider selection
            setupWizard.ShowStepHeader(1, 3, "LLM Provider Selection");
            var selectedProvider = await setupWizard.PromptForLlmProviderAsync(
                currentLlmConfig.Provider,
                cancellationToken);

            if (!selectedProvider.HasValue)
            {
                logger.LogInformation("LLM configuration cancelled by user");
                return Result<LlmConfiguration>.Failure("LLM configuration cancelled. No changes were made.");
            }

            // Step 2 & 3: Provider-specific configuration
            string modelId;
            string? apiKey = currentLlmConfig.ApiKey;
            string? baseUrl = null;


            if (selectedProvider.Value == LlmProvider.LocalOpenAiCompatible)
            {
                // Local LLM Configuration with inline verification
                setupWizard.ShowStepHeader(2, 3, "Local LLM Configuration");

                // Get current config for defaults
                string? currentBaseUrl = null;
                string? currentModel = null;
                
                if (currentLlmConfig.Providers.TryGetValue("LocalOpenAiCompatible", out var localConfig) &&
                    localConfig.TryGetValue("BaseUrl", out var configUrl))
                {
                    currentBaseUrl = configUrl;
                }

                if (currentLlmConfig.Provider == LlmProvider.LocalOpenAiCompatible)
                {
                    currentModel = currentLlmConfig.Model;
                }

                // Use new inline verification method
                var config = await setupWizard.PromptForLocalLlmConfigurationAsync(
                    currentBaseUrl,
                    currentModel,
                    httpClientFactory,
                    cancellationToken);

                if (config == null)
                {
                    logger.LogInformation("Local LLM configuration cancelled by user");
                    return Result<LlmConfiguration>.Failure("Local LLM configuration cancelled.");
                }

                (baseUrl, modelId) = config.Value;
            }
            else
            {
                // OpenAI / Anthropic Configuration
                
                // Determine total steps based on whether we'll need API key prompt
                bool providerWillChange = selectedProvider.Value != currentLlmConfig.Provider;
                bool willNeedApiKey = providerWillChange || !hasApiKey;
                int totalSteps = willNeedApiKey ? 3 : 2;

                // Step 2: Prompt for model selection
                setupWizard.ShowStepHeader(2, totalSteps, "Model Selection");

                // Pass current model only if staying with same provider
                var currentModelId = selectedProvider.Value == currentLlmConfig.Provider
                    ? currentLlmConfig.Model
                    : null;

                var selectedModel = await setupWizard.PromptForModelAsync(
                    selectedProvider.Value,
                    currentModelId,
                    cancellationToken);

                if (selectedModel == null)
                {
                    logger.LogInformation("Model selection cancelled by user");
                    return Result<LlmConfiguration>.Failure("Model selection cancelled. No changes were made.");
                }
                
                modelId = selectedModel.Id;

                // Step 3: Prompt for API key if provider changed OR no API key exists
                bool providerChanged = selectedProvider.Value != currentLlmConfig.Provider;
                bool needsApiKey = providerChanged || string.IsNullOrWhiteSpace(apiKey);

                if (needsApiKey)
                {
                    setupWizard.ShowStepHeader(3, 3, "API Key Configuration");

                    if (providerChanged)
                    {
                        setupWizard.ShowWarning($"Provider changed from {currentLlmConfig.Provider} to {selectedProvider.Value}. A new API key is required.");
                    }
                    else
                    {
                        setupWizard.ShowStatus($"Please provide your {GetProviderName(selectedProvider.Value)} API key.");
                    }

                    var newApiKey = await setupWizard.PromptForApiKeyAsync(
                        selectedProvider.Value,
                        providerChanged ? null : apiKey, // Show current key only if same provider
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(newApiKey))
                    {
                        logger.LogInformation("API key entry cancelled by user");
                        return Result<LlmConfiguration>.Failure("API key is required. Configuration not updated.");
                    }

                    // Validate the API key format
                    var validator = apiKeyValidators.FirstOrDefault(v => v.Provider == selectedProvider.Value);
                    if (validator != null)
                    {
                        var validationResult = await validator.ValidateFormatAsync(newApiKey);
                        if (!validationResult.IsValid)
                        {
                            return Result<LlmConfiguration>.Failure($"Invalid API key format: {validationResult.ErrorMessage}");
                        }
                    }

                    apiKey = newApiKey;
                }
            }

            // Create updated LLM configuration
            var updatedLlmConfig = new LlmOptions
            {
                Provider = selectedProvider.Value,
                Model = modelId,
                ApiKey = apiKey,
                MaxInputTokens = selectedProvider.Value == LlmProvider.Anthropic
                    ? LlmConstants.DefaultMaxInputTokensAnthropic
                    : LlmConstants.DefaultMaxInputTokensOpenAI,
                Providers = currentLlmConfig.Providers ?? new Dictionary<string, Dictionary<string, string>>()
            };

            // Save Local specific config
            if (selectedProvider.Value == LlmProvider.LocalOpenAiCompatible && !string.IsNullOrEmpty(baseUrl))
            {
                if (!updatedLlmConfig.Providers.TryGetValue("LocalOpenAiCompatible", out var providerConfig))
                {
                    providerConfig = new Dictionary<string, string>();
                    updatedLlmConfig.Providers["LocalOpenAiCompatible"] = providerConfig;
                }
                providerConfig["BaseUrl"] = baseUrl;
            }
            
            // Save to Llm section (canonical location)
            var saveResult = await sectionStore.WriteSectionAsync(
                LlmOptions.SectionPath,
                updatedLlmConfig,
                cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<LlmConfiguration>.Failure($"Failed to save configuration: {saveResult.Error}. Changes were not applied. Try again or check file permissions.");
            }

            logger.LogInformation(
                "LLM configuration updated successfully: Provider={Provider}, Model={Model}",
                selectedProvider.Value,
                modelId);

            // Display success message
            var providerName = GetProviderName(selectedProvider.Value);
            setupWizard.ShowSuccess($"✓ LLM configuration updated: {providerName} - {modelId}");

            // Return the configuration as LlmConfiguration model for compatibility
            var resultConfig = new LlmConfiguration
            {
                Provider = selectedProvider.Value,
                ApiKey = apiKey,
                Model = modelId,
                MaxInputTokens = updatedLlmConfig.MaxInputTokens
            };

            return Result<LlmConfiguration>.Success(resultConfig);
        }

        /// <summary>
        /// Gets the display name for an LLM provider.
        /// </summary>
        private static string GetProviderName(LlmProvider provider)
        {
            return provider switch
            {
                LlmProvider.OpenAI => "OpenAI",
                LlmProvider.Anthropic => "Anthropic",
                LlmProvider.LocalOpenAiCompatible => "Local (OpenAI Compatible)",
                _ => provider.ToString()
            };
        }

        private static bool HasCommandLineOverrides(Command request) =>
            request.ProviderOverride.HasValue ||
            !string.IsNullOrWhiteSpace(request.ModelOverride) ||
            !string.IsNullOrWhiteSpace(request.ApiKeyOverride) ||
            request.MaxInputTokensOverride.HasValue;

        private async Task<Result<LlmConfiguration>> ApplyCommandLineOverridesAsync(
            Command request,
            LlmOptions currentConfig,
            CancellationToken cancellationToken)
        {
            var provider = request.ProviderOverride ?? currentConfig.Provider;
            var model = (request.ModelOverride ?? currentConfig.Model)?.Trim();

            if (string.IsNullOrWhiteSpace(model))
            {
                return Result<LlmConfiguration>.Failure("Model is required when configuring LLM settings.");
            }

            // Skip validation for LocalOpenAiCompatible as models are dynamic
            if (provider != LlmProvider.LocalOpenAiCompatible && !ModelRegistry.IsValid(model, provider))
            {
                var providerName = GetProviderName(provider);
                var validModels = ModelRegistry.GetByProvider(provider);
                var validModelList = validModels.Count > 0
                    ? string.Join(", ", validModels.Select(m => m.Id))
                    : "None registered";

                var guidance = validModels.Count > 0
                    ? $"Valid {providerName} models: {validModelList}. Run 'tom config llm' without overrides to see details."
                    : $"No models are currently registered for {providerName}.";

                return Result<LlmConfiguration>.Failure(
                    $"Model '{model}' is not available for {providerName}. {guidance}");
            }

            var apiKey = request.ApiKeyOverride ?? currentConfig.ApiKey;
            var maxTokens = request.MaxInputTokensOverride
                ?? currentConfig.MaxInputTokens
                ?? GetDefaultMaxTokens(provider);

            if (provider == LlmProvider.OpenAI && string.IsNullOrWhiteSpace(apiKey))
            {
                return Result<LlmConfiguration>.Failure("OpenAI provider requires an API key.");
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var validator = apiKeyValidators.FirstOrDefault(v => v.Provider == provider);
                if (validator != null)
                {
                    var validationResult = await validator.ValidateFormatAsync(apiKey).ConfigureAwait(false);
                    if (!validationResult.IsValid)
                    {
                        return Result<LlmConfiguration>.Failure(
                            $"Invalid API key format: {validationResult.ErrorMessage ?? "Unknown error"}");
                    }
                }
            }

            var updatedConfig = new LlmOptions
            {
                Provider = provider,
                Model = model,
                ApiKey = apiKey,
                MaxInputTokens = maxTokens,
                Providers = currentConfig.Providers ?? new Dictionary<string, Dictionary<string, string>>()
            };

            var saveResult = await sectionStore.WriteSectionAsync(
                LlmOptions.SectionPath,
                updatedConfig,
                cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<LlmConfiguration>.Failure($"Failed to save LLM configuration: {saveResult.Error}");
            }

            setupWizard.ShowSuccess("✓ LLM configuration updated");
            setupWizard.ShowStatus($"  • Provider: {GetProviderName(provider)}");
            setupWizard.ShowStatus($"  • Model: {model}");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                setupWizard.ShowStatus("  • API key: provided");
            }
            setupWizard.ShowStatus($"  • Max input tokens: {maxTokens}");

            var resultConfig = new LlmConfiguration
            {
                Provider = provider,
                ApiKey = apiKey,
                Model = model,
                MaxInputTokens = maxTokens
            };

            return Result<LlmConfiguration>.Success(resultConfig);
        }

        private static int GetDefaultMaxTokens(LlmProvider provider) =>
            provider == LlmProvider.Anthropic
                ? LlmConstants.DefaultMaxInputTokensAnthropic
                : LlmConstants.DefaultMaxInputTokensOpenAI;
    }
}
