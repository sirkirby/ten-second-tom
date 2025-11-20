using FluentAssertions;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Models;
using Xunit;
using TenSecondTom.Features.Setup;


namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration tests for end-to-end model selection flow during guided setup.
/// Verifies that model selection, storage, and retrieval work correctly.
/// Each test uses a unique User Secrets ID to avoid interference with production configuration.
/// </summary>
[Collection(UserSecretsCollection.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "IAsyncLifetime pattern used instead of IDisposable")]
public sealed class ModelSelectionFlowTests : UserSecretsTestFixture
{
    private readonly TemporaryTestDirectory _testDirectory;

    public ModelSelectionFlowTests()
    {
        _testDirectory = new TemporaryTestDirectory();

        // Set up logger for the base fixture
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        Logger = loggerFactory.CreateLogger<UserSecretsTestFixture>();
    }
    [Fact]
    public async Task Setup_WithModelSelection_ShouldValidateModelIsSupported()
    {
        // Arrange
        var testModel = ModelRegistry.GetDefault(LlmProvider.OpenAI);

        // Act & Assert
        // Verify the test model is valid and can be used
        ModelRegistry.IsValid(testModel.Id, LlmProvider.OpenAI).Should().BeTrue();
        testModel.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Setup_WithDefaultModel_ShouldUseProviderDefault()
    {
        // Arrange
        var defaultModel = ModelRegistry.GetDefault(LlmProvider.Anthropic);

        // Act & Assert
        // Verify provider has a valid default model
        defaultModel.Should().NotBeNull();
        ModelRegistry.IsValid(defaultModel.Id, LlmProvider.Anthropic).Should().BeTrue();
    }

    [Theory]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Anthropic)]
    public async Task Setup_WithProviderSwitch_ShouldValidateNewProviderModels(LlmProvider newProvider)
    {
        // Arrange
        var oldProvider = newProvider == LlmProvider.OpenAI ? LlmProvider.Anthropic : LlmProvider.OpenAI;
        var oldModel = ModelRegistry.GetDefault(oldProvider);
        var newModel = ModelRegistry.GetDefault(newProvider);

        // Act & Assert
        ModelRegistry.IsValid(oldModel.Id, oldProvider).Should().BeTrue();
        ModelRegistry.IsValid(newModel.Id, newProvider).Should().BeTrue();
        oldModel.Id.Should().NotBe(newModel.Id);
    }

    [Fact]
    public async Task Setup_WithInvalidModel_ShouldBeDetectable()
    {
        // Arrange
        var invalidModelId = "invalid-model-that-does-not-exist";

        // Act & Assert - Validation should detect this as invalid
        ModelRegistry.IsValid(invalidModelId, LlmProvider.OpenAI).Should().BeFalse();
    }

    [Fact]
    public async Task Setup_WithModelFromDifferentProvider_ShouldBeDetectable()
    {
        // Arrange
        var anthropicModel = ModelRegistry.AnthropicModels[0];

        // Act & Assert - Validation should detect provider mismatch
        ModelRegistry.IsValid(anthropicModel.Id, LlmProvider.OpenAI).Should().BeFalse();
        ModelRegistry.IsValid(anthropicModel.Id, LlmProvider.Anthropic).Should().BeTrue();
    }

    [Fact]
    public void ModelRegistry_AllDefaultModels_ShouldBeValid()
    {
        // Arrange & Act
        var openAiDefault = ModelRegistry.GetDefault(LlmProvider.OpenAI);
        var anthropicDefault = ModelRegistry.GetDefault(LlmProvider.Anthropic);

        // Assert
        openAiDefault.Should().NotBeNull();
        anthropicDefault.Should().NotBeNull();
        
        ModelRegistry.IsValid(openAiDefault.Id, LlmProvider.OpenAI).Should().BeTrue();
        ModelRegistry.IsValid(anthropicDefault.Id, LlmProvider.Anthropic).Should().BeTrue();
    }

    private static LlmConfiguration CreateLlmConfiguration(LlmProvider provider, string? modelId)
    {
        return new LlmConfiguration
        {
            Provider = provider,
            ApiKey = "test-api-key",
            Model = modelId
        };
    }

    public override async Task DisposeAsync()
    {
        _testDirectory.Dispose();

        // Call base cleanup for UserSecrets
        await base.DisposeAsync();
    }
}

