using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Models;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Setup.Handlers;

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
}
