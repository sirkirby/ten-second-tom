using FluentAssertions;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Validation;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Setup.Validation;

/// <summary>
/// Unit tests for the ModelValidator class to ensure proper model validation logic.
/// </summary>
public sealed class ModelValidatorTests
{
    [Theory]
    [InlineData("gpt-4o-mini", LlmProvider.OpenAI)]
    [InlineData("gpt-4o", LlmProvider.OpenAI)]
    [InlineData("gpt-3.5-turbo", LlmProvider.OpenAI)]
    public void Validate_WithValidOpenAIModel_ShouldReturnSuccess(string modelId, LlmProvider provider)
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(modelId, provider);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("claude-3-5-haiku-20241022", LlmProvider.Anthropic)]
    [InlineData("claude-3-5-sonnet-20241022", LlmProvider.Anthropic)]
    [InlineData("claude-3-opus-20240229", LlmProvider.Anthropic)]
    public void Validate_WithValidAnthropicModel_ShouldReturnSuccess(string modelId, LlmProvider provider)
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(modelId, provider);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithInvalidModelId_ShouldReturnFailure()
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate("invalid-model-id", LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrWhiteSpace();
        errorMessage.Should().Contain("not recognized");
    }

    [Fact]
    public void Validate_WithNullModelId_ShouldReturnSuccess()
    {
        // Act - Null/empty models are valid, defaults will be used
        var (isValid, errorMessage) = ModelValidator.Validate(null, LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithEmptyModelId_ShouldReturnSuccess()
    {
        // Act - Null/empty models are valid, defaults will be used
        var (isValid, errorMessage) = ModelValidator.Validate(string.Empty, LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithWhitespaceModelId_ShouldReturnSuccess()
    {
        // Act - Null/empty models are valid, defaults will be used
        var (isValid, errorMessage) = ModelValidator.Validate("   ", LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_WithWrongProviderForModel_ShouldReturnFailure()
    {
        // Arrange - Use a valid OpenAI model with Anthropic provider
        const string openAiModelId = "gpt-4o-mini";

        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(openAiModelId, LlmProvider.Anthropic);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrWhiteSpace();
        errorMessage.Should().Contain("belongs to");
    }

    [Fact]
    public void Validate_ErrorMessage_ShouldIncludeValidModelsForProvider()
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate("invalid-model", LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("Valid models for OpenAI");
    }

    [Fact]
    public void Validate_ErrorMessage_ShouldListAvailableModels()
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate("invalid-model", LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeFalse();
        // Error should contain at least one known OpenAI model
        errorMessage.Should().ContainAny("gpt-4o-mini", "gpt-4o", "gpt-3.5-turbo");
    }

    [Theory]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Anthropic)]
    public void Validate_WithInvalidModel_ShouldProvideProviderSpecificGuidance(LlmProvider provider)
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate("invalid-model", provider);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain(provider.ToString());
    }

    [Fact]
    public void ValidateOrThrow_WithValidModel_ShouldNotThrow()
    {
        // Arrange
        const string validModel = "gpt-4o-mini";

        // Act
        var act = () => ModelValidator.ValidateOrThrow(validModel, LlmProvider.OpenAI);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrThrow_WithInvalidModel_ShouldThrow()
    {
        // Arrange
        const string invalidModel = "invalid-model";

        // Act
        var act = () => ModelValidator.ValidateOrThrow(invalidModel, LlmProvider.OpenAI);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not recognized*");
    }

    [Fact]
    public void GetEffectiveModel_WithValidModel_ShouldReturnProvidedModel()
    {
        // Arrange
        const string validModel = "gpt-4o-mini";

        // Act
        var effectiveModel = ModelValidator.GetEffectiveModel(validModel, LlmProvider.OpenAI);

        // Assert
        effectiveModel.Should().Be(validModel);
    }

    [Fact]
    public void GetEffectiveModel_WithNullModel_ShouldReturnDefault()
    {
        // Act
        var effectiveModel = ModelValidator.GetEffectiveModel(null, LlmProvider.OpenAI);

        // Assert
        var defaultModel = ModelRegistry.GetDefault(LlmProvider.OpenAI);
        effectiveModel.Should().Be(defaultModel.Id);
    }

    [Fact]
    public void GetEffectiveModel_WithEmptyModel_ShouldReturnDefault()
    {
        // Act
        var effectiveModel = ModelValidator.GetEffectiveModel(string.Empty, LlmProvider.Anthropic);

        // Assert
        var defaultModel = ModelRegistry.GetDefault(LlmProvider.Anthropic);
        effectiveModel.Should().Be(defaultModel.Id);
    }

    [Fact]
    public void GetEffectiveModel_WithInvalidModel_ShouldThrow()
    {
        // Arrange
        const string invalidModel = "invalid-model";

        // Act
        var act = () => ModelValidator.GetEffectiveModel(invalidModel, LlmProvider.OpenAI);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
