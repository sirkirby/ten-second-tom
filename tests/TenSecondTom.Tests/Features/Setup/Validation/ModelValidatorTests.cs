using FluentAssertions;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using Xunit;
using TenSecondTom.Features.Setup;

namespace TenSecondTom.Tests.Features.Setup.Validation;

/// <summary>
/// Unit tests for the ModelValidator class to ensure proper model validation logic.
/// </summary>
public sealed class ModelValidatorTests
{
    [Theory]
    [InlineData(LlmConstants.OpenAIModels.GPTMini, LlmProvider.OpenAI)]
    [InlineData(LlmConstants.OpenAIModels.GPTNano, LlmProvider.OpenAI)]
    [InlineData(LlmConstants.OpenAIModels.GPTStandard, LlmProvider.OpenAI)]
    public void Validate_WithValidOpenAIModel_ShouldReturnSuccess(string modelId, LlmProvider provider)
    {
        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(modelId, provider);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(LlmConstants.AnthropicModels.ClaudeHaiku, LlmProvider.Anthropic)]
    [InlineData(LlmConstants.AnthropicModels.ClaudeSonnet, LlmProvider.Anthropic)]
    [InlineData(LlmConstants.AnthropicModels.ClaudeOpus, LlmProvider.Anthropic)]
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
        const string openAiModelId = LlmConstants.OpenAIModels.GPTMini;

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
        errorMessage.Should().ContainAny(
            LlmConstants.OpenAIModels.GPTMini, 
            LlmConstants.OpenAIModels.GPTStandard, 
            LlmConstants.OpenAIModels.GPTNano);
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
        const string validModel = LlmConstants.OpenAIModels.GPTMini;

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
        const string validModel = LlmConstants.OpenAIModels.GPTMini;

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

    [Fact]
    public void Validate_WithDeprecatedOpenAIModel_ShouldReturnFailure()
    {
        // Arrange - Test with a deprecated OpenAI model
        const string deprecatedModel = "gpt-3.5-turbo-0301";

        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(deprecatedModel, LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("not recognized");
        errorMessage.Should().Contain("Valid models for OpenAI");
    }

    [Fact]
    public void Validate_WithDeprecatedAnthropicModel_ShouldReturnFailure()
    {
        // Arrange - Test with a deprecated Anthropic model
        const string deprecatedModel = "claude-2.1";

        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(deprecatedModel, LlmProvider.Anthropic);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("not recognized");
        errorMessage.Should().Contain("Valid models for Anthropic");
    }

    [Fact]
    public void Validate_WithProviderMismatch_ShouldSuggestConfigCommand()
    {
        // Arrange - OpenAI model with Anthropic provider
        const string openAiModel = LlmConstants.OpenAIModels.GPTMini;

        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(openAiModel, LlmProvider.Anthropic);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("tom config llm");
    }

    [Fact]
    public void Validate_WithAnthropicModelForOpenAI_ShouldProvideActionableError()
    {
        // Arrange - Anthropic model with OpenAI provider
        const string anthropicModel = LlmConstants.AnthropicModels.ClaudeHaiku;

        // Act
        var (isValid, errorMessage) = ModelValidator.Validate(anthropicModel, LlmProvider.OpenAI);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("belongs to Anthropic");
        errorMessage.Should().Contain("provider is set to OpenAI");
        errorMessage.Should().Contain("tom config llm");
    }
}
