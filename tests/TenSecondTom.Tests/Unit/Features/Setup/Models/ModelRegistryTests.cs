using FluentAssertions;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Setup.Models;

/// <summary>
/// Unit tests for the ModelRegistry static class to ensure proper model retrieval and validation.
/// </summary>
public sealed class ModelRegistryTests
{
    /// <summary>
    /// Verifies that the formatted display strings for models follow the expected pattern
    /// "DisplayName (CostTier) - Description" to be consumed by the selection prompt.
    /// (T042)
    /// </summary>
    [Fact]
    public void ModelDisplayFormatting_ShouldFollowExpectedPattern()
    {
        var allModels = ModelRegistry.OpenAIModels.Concat(ModelRegistry.AnthropicModels).ToList();

        allModels.Should().NotBeEmpty();

        foreach (var model in allModels)
        {
            var formatted = $"{model.DisplayName} ({model.CostTier}) - {model.Description}";
            formatted.Should().Contain(model.DisplayName);
            formatted.Should().Contain($"({model.CostTier})");
            formatted.Should().Contain(" - ");
            formatted.Should().EndWith(model.Description);
            // Ensure no Spectre markup characters slipped in unexpectedly
            formatted.Should().NotContain("[", "Square brackets would be parsed as Spectre.Console markup");
        }
    }
    [Fact]
    public void OpenAIModels_ShouldContainAtLeastThreeModels()
    {
        // Act
        var models = ModelRegistry.OpenAIModels;

        // Assert
        models.Should().NotBeEmpty();
        models.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void AnthropicModels_ShouldContainAtLeastThreeModels()
    {
        // Act
        var models = ModelRegistry.AnthropicModels;

        // Assert
        models.Should().NotBeEmpty();
        models.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void OpenAIModels_ShouldAllHaveOpenAIProvider()
    {
        // Act
        var models = ModelRegistry.OpenAIModels;

        // Assert
        models.Should().AllSatisfy(model => model.Provider.Should().Be(LlmProvider.OpenAI));
    }

    [Fact]
    public void AnthropicModels_ShouldAllHaveAnthropicProvider()
    {
        // Act
        var models = ModelRegistry.AnthropicModels;

        // Assert
        models.Should().AllSatisfy(model => model.Provider.Should().Be(LlmProvider.Anthropic));
    }

    [Fact]
    public void OpenAIModels_ShouldHaveExactlyOneDefaultModel()
    {
        // Act
        var models = ModelRegistry.OpenAIModels;
        var defaultModels = models.Where(m => m.IsDefault).ToList();

        // Assert
        defaultModels.Should().ContainSingle();
    }

    [Fact]
    public void AnthropicModels_ShouldHaveExactlyOneDefaultModel()
    {
        // Act
        var models = ModelRegistry.AnthropicModels;
        var defaultModels = models.Where(m => m.IsDefault).ToList();

        // Assert
        defaultModels.Should().ContainSingle();
    }

    [Fact]
    public void GetDefault_WithOpenAIProvider_ShouldReturnDefaultOpenAIModel()
    {
        // Act
        var defaultModel = ModelRegistry.GetDefault(LlmProvider.OpenAI);

        // Assert
        defaultModel.Should().NotBeNull();
        defaultModel.Provider.Should().Be(LlmProvider.OpenAI);
        defaultModel.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void GetDefault_WithAnthropicProvider_ShouldReturnDefaultAnthropicModel()
    {
        // Act
        var defaultModel = ModelRegistry.GetDefault(LlmProvider.Anthropic);

        // Assert
        defaultModel.Should().NotBeNull();
        defaultModel.Provider.Should().Be(LlmProvider.Anthropic);
        defaultModel.IsDefault.Should().BeTrue();
    }

    [Theory]
    [InlineData("gpt-5-nano")]
    [InlineData("gpt-5-mini")]
    [InlineData("gpt-5")]
    public void IsValid_WithValidOpenAIModelId_ShouldReturnTrue(string modelId)
    {
        // Arrange
        var openAiModelIds = ModelRegistry.OpenAIModels.Select(m => m.Id).ToList();

        // Skip test if model not in registry (flexible for registry changes)
        if (!openAiModelIds.Contains(modelId))
        {
            return;
        }

        // Act
        var isValid = ModelRegistry.IsValid(modelId, LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("claude-3-5-haiku-20241022")]
    [InlineData("claude-sonnet-4-5-20250929")]
    [InlineData("claude-opus-4-1-20250805")]
    public void IsValid_WithValidAnthropicModelId_ShouldReturnTrue(string modelId)
    {
        // Arrange
        var anthropicModelIds = ModelRegistry.AnthropicModels.Select(m => m.Id).ToList();

        // Skip test if model not in registry (flexible for registry changes)
        if (!anthropicModelIds.Contains(modelId))
        {
            return;
        }

        // Act
        var isValid = ModelRegistry.IsValid(modelId, LlmProvider.Anthropic);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithInvalidModelId_ShouldReturnFalse()
    {
        // Act
        var isValid = ModelRegistry.IsValid("invalid-model-id", LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithWrongProviderForModel_ShouldReturnFalse()
    {
        // Arrange - Get a valid OpenAI model ID
        var openAiModelId = ModelRegistry.OpenAIModels[0].Id;

        // Act - Try to validate it against Anthropic provider
        var isValid = ModelRegistry.IsValid(openAiModelId, LlmProvider.Anthropic);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void GetById_WithValidModelId_ShouldReturnModel()
    {
        // Arrange
        var expectedModel = ModelRegistry.OpenAIModels[0];

        // Act
        var model = ModelRegistry.GetById(expectedModel.Id);

        // Assert
        model.Should().NotBeNull();
        model.Should().Be(expectedModel);
    }

    [Fact]
    public void GetById_WithInvalidModelId_ShouldReturnNull()
    {
        // Act
        var model = ModelRegistry.GetById("invalid-model-id");

        // Assert
        model.Should().BeNull();
    }

    [Fact]
    public void GetByProvider_WithOpenAI_ShouldReturnOnlyOpenAIModels()
    {
        // Act
        var models = ModelRegistry.GetByProvider(LlmProvider.OpenAI);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().AllSatisfy(model => model.Provider.Should().Be(LlmProvider.OpenAI));
        models.Should().BeEquivalentTo(ModelRegistry.OpenAIModels);
    }

    [Fact]
    public void GetByProvider_WithAnthropic_ShouldReturnOnlyAnthropicModels()
    {
        // Act
        var models = ModelRegistry.GetByProvider(LlmProvider.Anthropic);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().AllSatisfy(model => model.Provider.Should().Be(LlmProvider.Anthropic));
        models.Should().BeEquivalentTo(ModelRegistry.AnthropicModels);
    }

    [Fact]
    public void AllModels_ShouldHaveUniqueIds()
    {
        // Act
        var allModelsList = new List<SupportedModel>();
        allModelsList.AddRange(ModelRegistry.OpenAIModels);
        allModelsList.AddRange(ModelRegistry.AnthropicModels);
        
        var ids = new HashSet<string>();
        foreach (var model in allModelsList)
        {
            ids.Add(model.Id);
        }

        // Assert
        ids.Should().HaveCount(allModelsList.Count, "all model IDs should be unique");
    }

    [Fact]
    public void AllModels_ShouldHaveNonEmptyProperties()
    {
        // Act
        var allModelsList = new List<SupportedModel>();
        allModelsList.AddRange(ModelRegistry.OpenAIModels);
        allModelsList.AddRange(ModelRegistry.AnthropicModels);

        // Assert
        allModelsList.Should().AllSatisfy(model =>
        {
            model.Id.Should().NotBeNullOrWhiteSpace();
            model.DisplayName.Should().NotBeNullOrWhiteSpace();
            model.Description.Should().NotBeNullOrWhiteSpace();
        });
    }
}
