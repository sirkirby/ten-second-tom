using System.Linq;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.LocalAi;
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
        ILocalAiEngine localAiEngine,
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
                Provider = LlmProvider.OpenAI // Default provider for first-time setup
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
                var currentModel = currentLlmConfig.GetModel();
                logger.LogInformation("LLM already configured and not forced, skipping interactive setup");
                setupWizard.ShowSuccess($"✓ LLM already configured: {GetProviderName(currentLlmConfig.Provider)} - {currentModel}");

                // Return existing configuration
                var existingConfig = new LlmConfiguration
                {
                    Provider = currentLlmConfig.Provider,
                    ApiKey = currentLlmConfig.GetApiKey(),
                    Model = currentModel,
                    MaxInputTokens = currentLlmConfig.GetMaxInputTokens() ?? (currentLlmConfig.Provider == LlmProvider.Anthropic
                        ? LlmConstants.DefaultMaxInputTokensAnthropic
                        : LlmConstants.DefaultMaxInputTokensOpenAI)
                };

                return Result<LlmConfiguration>.Success(existingConfig);
            }

            logger.LogInformation("Starting interactive LLM configuration");

            // Pre-calculate if we'll need API key prompt (needed to show accurate step count)
            // We'll need API key if it's missing - provider change check happens after selection
            bool hasApiKey = !string.IsNullOrWhiteSpace(currentLlmConfig.GetApiKey());

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
            string? apiKey = currentLlmConfig.GetApiKey();
            string? baseUrl = null;


            if (selectedProvider.Value == LlmProvider.LocalOpenAiCompatible)
            {
                // Local LLM Configuration with inline verification
                setupWizard.ShowStepHeader(2, 3, "Local LLM Configuration");

                // Get current config for defaults using accessor methods
                string? currentBaseUrl = currentLlmConfig.GetBaseUrl(LlmProvider.LocalOpenAiCompatible);
                string? currentModel = currentLlmConfig.GetModel(LlmProvider.LocalOpenAiCompatible);

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

                // Local providers don't need API keys - clear any carried over from cloud providers
                apiKey = null;
            }
            else if (selectedProvider.Value == LlmProvider.BuiltInLocal)
            {
                // Built-in Local LLM Configuration (Microsoft AI Foundry Local SDK)
                setupWizard.ShowStepHeader(2, 2, "Model Selection");
                setupWizard.ShowStatus("Fetching available models from AI Foundry catalog...");

                var availableModels = (await localAiEngine.ListAvailableModelsAsync(cancellationToken)).ToList();

                if (availableModels.Count == 0)
                {
                    setupWizard.ShowWarning("No models found in the AI Foundry catalog.");
                    setupWizard.ShowStatus("The built-in local engine requires models to be available in the catalog.");
                    setupWizard.ShowStatus("This may indicate a network issue or the SDK is not properly initialized.");
                    return Result<LlmConfiguration>.Failure("No models available for built-in local provider.");
                }

                // Get current model for highlighting (from this provider's config)
                string? currentModel = currentLlmConfig.GetModel(LlmProvider.BuiltInLocal);

                var selectedModel = await setupWizard.PromptForSelectionAsync(
                    "Select a model:",
                    availableModels,
                    m => m, // Model IDs are already display-friendly
                    cancellationToken);

                if (string.IsNullOrEmpty(selectedModel))
                {
                    logger.LogInformation("Built-in local model selection cancelled by user");
                    return Result<LlmConfiguration>.Failure("Model selection cancelled. No changes were made.");
                }

                modelId = selectedModel;

                // Ensure the model is downloaded with progress bar
                Result? downloadResult = null;
                await setupWizard.RunWithProgressAsync(
                    $"Downloading model '{modelId}'...",
                    async progress =>
                    {
                        downloadResult = await localAiEngine.EnsureModelAvailableAsync(
                            modelId,
                            progress,
                            cancellationToken);
                    },
                    cancellationToken);

                if (downloadResult?.IsSuccess != true)
                {
                    setupWizard.ShowError($"Failed to download model: {downloadResult?.Error ?? "Unknown error"}");
                    return Result<LlmConfiguration>.Failure($"Failed to ensure model is available: {downloadResult?.Error ?? "Unknown error"}");
                }

                setupWizard.ShowSuccess($"✓ Model '{modelId}' is ready");

                // Local providers don't need API keys - clear any carried over from cloud providers
                apiKey = null;
            }
            else
            {
                // OpenAI / Anthropic Configuration

                // Determine total steps based on whether we'll need API key prompt
                // Check if selected provider already has an API key configured
                var selectedProviderApiKeyForSteps = currentLlmConfig.GetApiKey(selectedProvider.Value);
                bool willNeedApiKey = string.IsNullOrWhiteSpace(selectedProviderApiKeyForSteps);
                int totalSteps = willNeedApiKey ? 3 : 2;

                // Step 2: Prompt for model selection
                setupWizard.ShowStepHeader(2, totalSteps, "Model Selection");

                // Get current model for this provider (uses accessor for provider-specific config)
                var currentModelId = currentLlmConfig.GetModel(selectedProvider.Value);

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

                // Step 3: Prompt for API key if the selected provider doesn't have one configured
                // Check if the *selected* provider has an API key (not just current active provider)
                var selectedProviderApiKey = currentLlmConfig.GetApiKey(selectedProvider.Value);
                bool needsApiKey = string.IsNullOrWhiteSpace(selectedProviderApiKey);

                if (needsApiKey)
                {
                    setupWizard.ShowStepHeader(3, 3, "API Key Configuration");
                    setupWizard.ShowStatus($"Please provide your {GetProviderName(selectedProvider.Value)} API key.");

                    var newApiKey = await setupWizard.PromptForApiKeyAsync(
                        selectedProvider.Value,
                        selectedProviderApiKey, // Show existing key for this provider if any
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
                else
                {
                    // Use existing API key for this provider
                    apiKey = selectedProviderApiKey;
                }
            }

            // Create updated LLM configuration - store provider-specific settings under Providers
            var updatedLlmConfig = new LlmOptions
            {
                Provider = selectedProvider.Value,
                Providers = currentLlmConfig.Providers ?? new Dictionary<string, Dictionary<string, string>>()
            };

            // Save provider-specific config
            updatedLlmConfig.SetProviderConfig(selectedProvider.Value, "Model", modelId);

            // Handle API key - save for cloud providers, remove for local providers
            if (selectedProvider.Value == LlmProvider.LocalOpenAiCompatible ||
                selectedProvider.Value == LlmProvider.BuiltInLocal)
            {
                // Explicitly remove API key from local providers (cleans up any stale config)
                updatedLlmConfig.SetProviderConfig(selectedProvider.Value, "ApiKey", null);
            }
            else if (!string.IsNullOrWhiteSpace(apiKey))
            {
                updatedLlmConfig.SetProviderConfig(selectedProvider.Value, "ApiKey", apiKey);
            }

            var maxTokens = selectedProvider.Value == LlmProvider.Anthropic
                ? LlmConstants.DefaultMaxInputTokensAnthropic
                : LlmConstants.DefaultMaxInputTokensOpenAI;
            updatedLlmConfig.SetProviderConfig(selectedProvider.Value, "MaxInputTokens", maxTokens.ToString());

            // Save BaseUrl for LocalOpenAiCompatible
            if (selectedProvider.Value == LlmProvider.LocalOpenAiCompatible && !string.IsNullOrEmpty(baseUrl))
            {
                updatedLlmConfig.SetProviderConfig(selectedProvider.Value, "BaseUrl", baseUrl);
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
                MaxInputTokens = maxTokens
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
                LlmProvider.BuiltInLocal => "Built-in Local (AI Foundry)",
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
            // Get model from override, or from target provider's config, or from legacy top-level
            var model = (request.ModelOverride ?? currentConfig.GetModel(provider))?.Trim();

            if (string.IsNullOrWhiteSpace(model))
            {
                return Result<LlmConfiguration>.Failure("Model is required when configuring LLM settings.");
            }

            // Skip validation for local providers as models are dynamic
            if (provider != LlmProvider.LocalOpenAiCompatible &&
                provider != LlmProvider.BuiltInLocal &&
                !ModelRegistry.IsValid(model, provider))
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

            // Get api key from override, or from target provider's config
            var apiKey = request.ApiKeyOverride ?? currentConfig.GetApiKey(provider);
            var maxTokens = request.MaxInputTokensOverride
                ?? currentConfig.GetMaxInputTokens(provider)
                ?? GetDefaultMaxTokens(provider);

            // Cloud providers require API key
            if ((provider == LlmProvider.OpenAI || provider == LlmProvider.Anthropic) &&
                string.IsNullOrWhiteSpace(apiKey))
            {
                return Result<LlmConfiguration>.Failure($"{GetProviderName(provider)} provider requires an API key.");
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

            // Create updated config - store provider-specific settings under Providers
            var updatedConfig = new LlmOptions
            {
                Provider = provider,
                Providers = currentConfig.Providers ?? new Dictionary<string, Dictionary<string, string>>()
            };

            // Save provider-specific config
            updatedConfig.SetProviderConfig(provider, "Model", model);

            // Handle API key - save for cloud providers, remove for local providers
            if (provider == LlmProvider.LocalOpenAiCompatible || provider == LlmProvider.BuiltInLocal)
            {
                // Explicitly remove API key from local providers (cleans up any stale config)
                updatedConfig.SetProviderConfig(provider, "ApiKey", null);
            }
            else if (!string.IsNullOrWhiteSpace(apiKey))
            {
                updatedConfig.SetProviderConfig(provider, "ApiKey", apiKey);
            }

            updatedConfig.SetProviderConfig(provider, "MaxInputTokens", maxTokens.ToString());

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
