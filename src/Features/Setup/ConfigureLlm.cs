using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Configures LLM provider and model settings interactively.
/// This use case handles all LLM configuration logic, keeping it within the Setup feature slice.
/// </summary>
public static class ConfigureLlm
{
    /// <summary>
    /// Command to configure LLM provider and model settings.
    /// </summary>
    public sealed record Command : IRequest<Result<ConfigurationSettings>>;

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
        IConfigurationStorageService storageService,
        ISetupWizardUI setupWizard,
        IEnumerable<IApiKeyValidator> apiKeyValidators,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<ConfigurationSettings>>
    {
        public async Task<Result<ConfigurationSettings>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            // Load current configuration
            var loadResult = await storageService.LoadAsync(cancellationToken);

            if (!loadResult.IsSuccess)
            {
                return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to create initial configuration, then use 'tom config llm' to update LLM settings.");
            }

            var currentConfig = loadResult.Value!;

            logger.LogInformation("Starting interactive LLM configuration");

            // Determine total steps (3 if provider changes, 2 if same provider)
            bool willChangeProvider = false;

            // Step 1: Prompt for LLM provider
            setupWizard.ShowStepHeader(1, 3, "LLM Provider Selection");
            var selectedProvider = await setupWizard.PromptForLlmProviderAsync(
                currentConfig.Llm.Provider,
                cancellationToken);

            if (!selectedProvider.HasValue)
            {
                logger.LogInformation("LLM configuration cancelled by user");
                return Result<ConfigurationSettings>.Failure("LLM configuration cancelled. No changes were made.");
            }

            willChangeProvider = selectedProvider.Value != currentConfig.Llm.Provider;
            int totalSteps = willChangeProvider ? 3 : 2;

            // Step 2: Prompt for model selection
            setupWizard.ShowStepHeader(2, totalSteps, "Model Selection");

            // Pass current model only if staying with same provider
            var currentModelId = selectedProvider.Value == currentConfig.Llm.Provider
                ? currentConfig.Llm.Model
                : null;

            var selectedModel = await setupWizard.PromptForModelAsync(
                selectedProvider.Value,
                currentModelId,
                cancellationToken);

            if (selectedModel == null)
            {
                logger.LogInformation("Model selection cancelled by user");
                return Result<ConfigurationSettings>.Failure("Model selection cancelled. No changes were made.");
            }

            // Step 3: If provider changed, prompt for new API key
            string? apiKey = currentConfig.Llm.ApiKey;
            bool providerChanged = selectedProvider.Value != currentConfig.Llm.Provider;

            if (providerChanged)
            {
                setupWizard.ShowStepHeader(3, 3, "API Key Configuration");
                setupWizard.ShowWarning($"Provider changed from {currentConfig.Llm.Provider} to {selectedProvider.Value}. A new API key is required.");

                var newApiKey = await setupWizard.PromptForApiKeyAsync(
                    selectedProvider.Value,
                    null, // Don't show current key from different provider
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(newApiKey))
                {
                    logger.LogInformation("API key entry cancelled by user");
                    return Result<ConfigurationSettings>.Failure("API key is required when changing providers. Configuration not updated.");
                }

                // Validate the API key format
                var validator = apiKeyValidators.FirstOrDefault(v => v.Provider == selectedProvider.Value);
                if (validator != null)
                {
                    var validationResult = await validator.ValidateFormatAsync(newApiKey);
                    if (!validationResult.IsValid)
                    {
                        return Result<ConfigurationSettings>.Failure($"Invalid API key format: {validationResult.ErrorMessage}");
                    }
                }

                apiKey = newApiKey;
            }

            // Update configuration
            var updatedConfig = currentConfig with
            {
                Llm = currentConfig.Llm with
                {
                    Provider = selectedProvider.Value,
                    Model = selectedModel.Id,
                    ApiKey = apiKey,
                    MaxInputTokens = selectedProvider.Value == LlmProvider.Anthropic
                        ? LlmConstants.DefaultMaxInputTokensAnthropic
                        : LlmConstants.DefaultMaxInputTokensOpenAI
                }
            };

            var markedConfig = updatedConfig.MarkAsModified();

            // Save updated configuration
            var saveResult = await storageService.SaveAsync(markedConfig, cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}. Changes were not applied. Try again or check file permissions.");
            }

            logger.LogInformation(
                "LLM configuration updated successfully: Provider={Provider}, Model={Model}",
                selectedProvider.Value,
                selectedModel.Id);

            // Display success message
            var providerName = selectedProvider.Value == LlmProvider.OpenAI ? "OpenAI" : "Anthropic";
            setupWizard.ShowSuccess($"✓ LLM configuration updated: {providerName} - {selectedModel.DisplayName} [{selectedModel.CostTier}]");

            return Result<ConfigurationSettings>.Success(markedConfig);
        }
    }
}

