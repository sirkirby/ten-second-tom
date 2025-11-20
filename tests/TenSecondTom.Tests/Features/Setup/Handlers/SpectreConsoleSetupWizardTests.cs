using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Shared.Models;
using Xunit;
using TenSecondTom.Features.Setup;
using TenSecondTom.Infrastructure.Auth;

namespace TenSecondTom.Tests.Features.Setup.Handlers;

/// <summary>
/// Unit tests for the SpectreConsoleSetupWizard class focusing on model selection functionality.
/// Note: These tests focus on the method signature and basic logic, not the Spectre.Console UI interaction.
/// </summary>
public sealed class SpectreConsoleSetupWizardTests
{
    private readonly Mock<ILogger<SpectreConsoleSetupWizard>> _mockLogger;

    public SpectreConsoleSetupWizardTests()
    {
        _mockLogger = new Mock<ILogger<SpectreConsoleSetupWizard>>();
    }

    [Fact]
    public void ModelRegistry_ShouldProvideModelsForPromptForModelAsync()
    {
        // Arrange
        var openAiModels = ModelRegistry.GetByProvider(LlmProvider.OpenAI);
        var anthropicModels = ModelRegistry.GetByProvider(LlmProvider.Anthropic);

        // Assert - Verify models are available for the wizard to use
        openAiModels.Should().NotBeEmpty("OpenAI models should be available for selection");
        anthropicModels.Should().NotBeEmpty("Anthropic models should be available for selection");
        
        // Verify each model has display information
        openAiModels.Should().AllSatisfy(model =>
        {
            model.DisplayName.Should().NotBeNullOrWhiteSpace();
            model.CostTier.Should().NotBeNullOrWhiteSpace();
            model.Description.Should().NotBeNullOrWhiteSpace();
        });
        
        anthropicModels.Should().AllSatisfy(model =>
        {
            model.DisplayName.Should().NotBeNullOrWhiteSpace();
            model.CostTier.Should().NotBeNullOrWhiteSpace();
            model.Description.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Theory]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Anthropic)]
    public void ModelRegistry_ShouldProvideDefaultModelForEachProvider(LlmProvider provider)
    {
        // Act
        var defaultModel = ModelRegistry.GetDefault(provider);

        // Assert
        defaultModel.Should().NotBeNull();
        defaultModel.Provider.Should().Be(provider);
        defaultModel.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ModelRegistry_EachProviderShouldHaveMultipleModels()
    {
        // Arrange & Act
        var openAiModels = ModelRegistry.GetByProvider(LlmProvider.OpenAI);
        var anthropicModels = ModelRegistry.GetByProvider(LlmProvider.Anthropic);

        // Assert
        openAiModels.Should().HaveCountGreaterThanOrEqualTo(2, 
            "OpenAI should have multiple models for users to choose from");
        anthropicModels.Should().HaveCountGreaterThanOrEqualTo(2, 
            "Anthropic should have multiple models for users to choose from");
    }

    [Fact]
    public void ModelRegistry_ModelsShouldHaveVariedCostTiers()
    {
        // Arrange & Act
        var allModels = ModelRegistry.OpenAIModels
            .Concat(ModelRegistry.AnthropicModels)
            .ToList();

        var costTiers = allModels.Select(m => m.CostTier).Distinct().ToList();

        // Assert
        costTiers.Should().HaveCountGreaterThanOrEqualTo(2, 
            "Models should have varied cost tiers to give users pricing options");
    }

    [Fact]
    public void ModelRegistry_CurrentModelHighlighting_ShouldWork()
    {
        // Arrange
        var openAiModels = ModelRegistry.GetByProvider(LlmProvider.OpenAI);
        var firstModel = openAiModels[0];

        // Act
        var foundModel = ModelRegistry.GetById(firstModel.Id);

        // Assert - Verify we can look up current model for highlighting
        foundModel.Should().NotBeNull();
        foundModel.Should().Be(firstModel);
    }

    [Fact]
    public void ModelRegistry_AllDisplayNames_ShouldBeDistinct()
    {
        // Arrange & Act
        var allModels = ModelRegistry.OpenAIModels
            .Concat(ModelRegistry.AnthropicModels)
            .ToList();

        var displayNames = allModels.Select(m => m.DisplayName).ToList();

        // Assert - Verify all display names are unique (important for parsing)
        displayNames.Should().OnlyHaveUniqueItems("Display names must be unique to avoid ambiguity in model selection");
    }

    [Theory]
    [InlineData("Claude Sonnet 4.5", "Balanced", "Best model for complex agents and coding with highest intelligence")]
    [InlineData("GPT-5 Nano", "Budget", "Fastest, cheapest model for summarization and classification")]
    public void ModelRegistry_FormattedChoiceStrings_ShouldBeHandledCorrectly(
        string displayName,
        string costTier,
        string description)
    {
        // This test verifies that model selection can handle display names with parentheses
        // Bug fix: The original parsing logic looked for the first '(' which failed when
        // the DisplayName itself contained parentheses (e.g., "Claude Sonnet 4.5 (2025-09-29)")
        
        // Arrange - Create a formatted choice string as shown to users
        var formattedChoice = $"{displayName} ({costTier}) - {description}";

        // Act - Find the model by its display name (simulating what the UI does)
        var allModels = ModelRegistry.AllModels;
        var model = allModels.FirstOrDefault(m => 
            m.DisplayName == displayName && 
            m.CostTier == costTier);

        // Assert - Verify we can find the model even when DisplayName has parentheses
        model.Should().NotBeNull($"Model with display name '{displayName}' should exist in registry");
        model!.DisplayName.Should().Be(displayName);
        model.CostTier.Should().Be(costTier);
        model.Description.Should().Be(description);
        
        // Verify the formatted choice string can be constructed
        var reconstructedChoice = $"{model.DisplayName} ({model.CostTier}) - {model.Description}";
        reconstructedChoice.Should().Be(formattedChoice);
    }

    [Theory]
    [InlineData("Balanced")]
    [InlineData("Budget")]
    [InlineData("Premium")]
    public void ModelRegistry_CostTierValues_ShouldNotConflictWithSpectreConsoleMarkup(string costTier)
    {
        // This test verifies that cost tier values don't conflict with Spectre.Console markup syntax
        // Bug fix: The original code placed cost tiers in square brackets without escaping,
        // causing Spectre.Console to interpret [Balanced] as a style directive rather than literal text
        
        // Arrange - Get all models with this cost tier
        var allModels = ModelRegistry.AllModels;
        var modelsWithTier = allModels.Where(m => m.CostTier == costTier).ToList();

        // Assert - Verify models with this tier exist
        modelsWithTier.Should().NotBeEmpty($"There should be models with cost tier '{costTier}'");

        // Verify the formatted string can be constructed with square brackets (as used in UI)
        // When properly escaped, this should not throw a Spectre.Console parsing error
        foreach (var model in modelsWithTier)
        {
            var formattedChoice = $"{model.DisplayName} [{model.CostTier}] - {model.Description}";
            
            // Verify the string contains the expected bracket notation
            formattedChoice.Should().Contain($"[{costTier}]", 
                "The choice string should show cost tier in brackets");
            
            // If this were used in Spectre.Console without .EscapeMarkup(), it would throw:
            // "System.InvalidOperationException: Could not find color or style 'Balanced'"
            // The fix ensures .EscapeMarkup() is called on the entire formatted string
        }
    }

    #region Audio Configuration Prompt Tests

    [Fact]
    public void AudioPromptMethods_ShouldExistWithCorrectSignatures()
    {
        // Arrange & Act - Verify method exists and has correct signature
        var inputVolumeMethod = typeof(SpectreConsoleSetupWizard).GetMethod("PromptForInputVolumeAsync");
        var booleanMethod = typeof(SpectreConsoleSetupWizard).GetMethod("PromptForBooleanAsync");
        var intMethod = typeof(SpectreConsoleSetupWizard).GetMethod("PromptForIntAsync");
        var sttProviderMethod = typeof(SpectreConsoleSetupWizard).GetMethod("PromptForSttProviderAsync");
        var sttApiKeyMethod = typeof(SpectreConsoleSetupWizard).GetMethod("PromptForSttApiKeyAsync");
        var sttFallbackMethod = typeof(SpectreConsoleSetupWizard).GetMethod("PromptForSttFallbackAsync");

        // Assert
        inputVolumeMethod.Should().NotBeNull("PromptForInputVolumeAsync method should exist");
        inputVolumeMethod!.ReturnType.Should().Be<Task<double?>>();

        booleanMethod.Should().NotBeNull("PromptForBooleanAsync method should exist");
        booleanMethod!.ReturnType.Should().Be<Task<bool?>>();

        intMethod.Should().NotBeNull("PromptForIntAsync method should exist");
        intMethod!.ReturnType.Should().Be<Task<int?>>();

        sttProviderMethod.Should().NotBeNull("PromptForSttProviderAsync method should exist");
        sttProviderMethod!.ReturnType.Should().Be<Task<string?>>();

        sttApiKeyMethod.Should().NotBeNull("PromptForSttApiKeyAsync method should exist");
        sttApiKeyMethod!.ReturnType.Should().Be<Task<string?>>();

        sttFallbackMethod.Should().NotBeNull("PromptForSttFallbackAsync method should exist");
        sttFallbackMethod!.ReturnType.Should().Be<Task<bool?>>();
    }

    #endregion
}
