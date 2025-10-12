using FluentAssertions;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Setup.Models;

/// <summary>
/// Unit tests for the SupportedModel record to ensure proper validation and behavior.
/// </summary>
public sealed class SupportedModelTests
{
    [Fact]
    public void SupportedModel_WithValidProperties_ShouldCreateSuccessfully()
    {
        // Arrange & Act
        var model = new SupportedModel(
            Id: "gpt-4o",
            DisplayName: "GPT-4 Optimized",
            Provider: LlmProvider.OpenAI,
            CostTier: "Premium",
            Description: "Most capable OpenAI model",
            IsDefault: true
        );

        // Assert
        model.Should().NotBeNull();
        model.Id.Should().Be("gpt-4o");
        model.DisplayName.Should().Be("GPT-4 Optimized");
        model.Provider.Should().Be(LlmProvider.OpenAI);
        model.CostTier.Should().Be("Premium");
        model.Description.Should().Be("Most capable OpenAI model");
        model.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void SupportedModel_WithNonDefaultModel_ShouldHaveIsDefaultFalse()
    {
        // Arrange & Act
        var model = new SupportedModel(
            Id: "gpt-4o-mini",
            DisplayName: "GPT-4 Optimized Mini",
            Provider: LlmProvider.OpenAI,
            CostTier: "Balanced",
            Description: "Fast and cost-effective",
            IsDefault: false
        );

        // Assert
        model.IsDefault.Should().BeFalse();
    }

    [Theory]
    [InlineData("Budget")]
    [InlineData("Balanced")]
    [InlineData("Premium")]
    public void SupportedModel_WithDifferentCostTiers_ShouldRetainCostTier(string tier)
    {
        // Arrange & Act
        var model = new SupportedModel(
            Id: "test-model",
            DisplayName: "Test Model",
            Provider: LlmProvider.OpenAI,
            CostTier: tier,
            Description: "Test description",
            IsDefault: false
        );

        // Assert
        model.CostTier.Should().Be(tier);
    }

    [Theory]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Anthropic)]
    public void SupportedModel_WithDifferentProviders_ShouldRetainProvider(LlmProvider provider)
    {
        // Arrange & Act
        var model = new SupportedModel(
            Id: "test-model",
            DisplayName: "Test Model",
            Provider: provider,
            CostTier: "Balanced",
            Description: "Test description",
            IsDefault: false
        );

        // Assert
        model.Provider.Should().Be(provider);
    }

    [Fact]
    public void SupportedModel_RecordEquality_ShouldWorkCorrectly()
    {
        // Arrange
        var model1 = new SupportedModel(
            Id: "gpt-4o",
            DisplayName: "GPT-4 Optimized",
            Provider: LlmProvider.OpenAI,
            CostTier: "Premium",
            Description: "Most capable OpenAI model",
            IsDefault: true
        );

        var model2 = new SupportedModel(
            Id: "gpt-4o",
            DisplayName: "GPT-4 Optimized",
            Provider: LlmProvider.OpenAI,
            CostTier: "Premium",
            Description: "Most capable OpenAI model",
            IsDefault: true
        );

        // Act & Assert
        model1.Should().Be(model2);
        (model1 == model2).Should().BeTrue();
    }

    [Fact]
    public void SupportedModel_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var model1 = new SupportedModel(
            Id: "gpt-4o",
            DisplayName: "GPT-4 Optimized",
            Provider: LlmProvider.OpenAI,
            CostTier: "Premium",
            Description: "Most capable OpenAI model",
            IsDefault: true
        );

        var model2 = new SupportedModel(
            Id: "gpt-4o-mini",
            DisplayName: "GPT-4 Optimized",
            Provider: LlmProvider.OpenAI,
            CostTier: "Premium",
            Description: "Most capable OpenAI model",
            IsDefault: true
        );

        // Act & Assert
        model1.Should().NotBe(model2);
        (model1 == model2).Should().BeFalse();
    }
}
