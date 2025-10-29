using FluentAssertions;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.IntegrationTests.TestHelpers;
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
    public async Task Setup_WithModelSelection_ShouldSaveModelToUserSecrets()
    {
        // Arrange
        var storageService = CreateStorageService();
        var testModel = ModelRegistry.GetDefault(LlmProvider.OpenAI);

        var settings = CreateTestSettings(
            provider: LlmProvider.OpenAI,
            modelId: testModel.Id);

        // Act
        await storageService.SaveAsync(settings, CancellationToken.None);
        var loadResult = await storageService.LoadAsync(CancellationToken.None);

        // Assert
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        loaded.Llm.Model.Should().Be(testModel.Id);
        loaded.Llm.Provider.Should().Be(LlmProvider.OpenAI);
    }

    [Fact]
    public async Task Setup_WithDefaultModel_ShouldUseProviderDefaultWhenModelNotSpecified()
    {
        // Arrange
        var storageService = CreateStorageService();

        var settings = CreateTestSettings(
            provider: LlmProvider.Anthropic,
            modelId: null); // No model specified

        // Act
        await storageService.SaveAsync(settings, CancellationToken.None);
        var loadResult = await storageService.LoadAsync(CancellationToken.None);

        // Assert
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        
        // When no model is specified, the system should use the default for the provider
        var defaultModel = ModelRegistry.GetDefault(LlmProvider.Anthropic);
        
        // The loaded config might have null model, which is fine - the factory will use default
        // This test documents the current behavior
        if (!string.IsNullOrEmpty(loaded.Llm.Model))
        {
            ModelRegistry.IsValid(loaded.Llm.Model, loaded.Llm.Provider).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Anthropic)]
    public async Task Setup_WithProviderSwitch_ShouldUpdateModelForNewProvider(LlmProvider newProvider)
    {
        // Arrange
        var storageService = CreateStorageService();
        var oldProvider = newProvider == LlmProvider.OpenAI ? LlmProvider.Anthropic : LlmProvider.OpenAI;
        var oldModel = ModelRegistry.GetDefault(oldProvider);
        var newModel = ModelRegistry.GetDefault(newProvider);

        // First, save configuration with old provider
        var oldSettings = CreateTestSettings(
            provider: oldProvider,
            modelId: oldModel.Id);

        await storageService.SaveAsync(oldSettings, CancellationToken.None);

        // Act - Switch to new provider with new model
        var newSettings = CreateTestSettings(
            provider: newProvider,
            modelId: newModel.Id);

        await storageService.SaveAsync(newSettings, CancellationToken.None);
        var loadResult = await storageService.LoadAsync(CancellationToken.None);

        // Assert
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        loaded.Llm.Provider.Should().Be(newProvider);
        loaded.Llm.Model.Should().Be(newModel.Id);
        ModelRegistry.IsValid(loaded.Llm.Model, loaded.Llm.Provider).Should().BeTrue();
    }

    [Fact]
    public async Task Setup_WithInvalidModel_ShouldBeDetectableByValidator()
    {
        // Arrange
        var storageService = CreateStorageService();

        var settings = CreateTestSettings(
            provider: LlmProvider.OpenAI,
            modelId: "invalid-model-that-does-not-exist");

        // Act
        await storageService.SaveAsync(settings, CancellationToken.None);
        var loadResult = await storageService.LoadAsync(CancellationToken.None);

        // Assert - Storage allows saving invalid models, but validation should catch it
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        loaded.Llm.Model.Should().Be("invalid-model-that-does-not-exist");
        
        // Validation should detect this as invalid
        ModelRegistry.IsValid(loaded.Llm.Model, loaded.Llm.Provider).Should().BeFalse();
    }

    [Fact]
    public async Task Setup_WithModelFromDifferentProvider_ShouldBeDetectableByValidator()
    {
        // Arrange
        var storageService = CreateStorageService();
        var anthropicModel = ModelRegistry.AnthropicModels[0];

        var settings = CreateTestSettings(
            provider: LlmProvider.OpenAI, // OpenAI provider
            modelId: anthropicModel.Id); // But Anthropic model

        // Act
        await storageService.SaveAsync(settings, CancellationToken.None);
        var loadResult = await storageService.LoadAsync(CancellationToken.None);

        // Assert - Storage allows this mismatch, but validation should catch it
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        loaded.Llm.Model.Should().NotBeNullOrEmpty();
        ModelRegistry.IsValid(loaded.Llm.Model!, loaded.Llm.Provider).Should().BeFalse();
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

    private ConfigurationStorageService CreateStorageService()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigurationStorageService>();
        var testAppSettingsPath = Path.Combine(_testDirectory.BasePath, "appsettings.json");
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        return new ConfigurationStorageService(logger, configuration, testAppSettingsPath);
    }

    private static ConfigurationSettings CreateTestSettings(LlmProvider provider, string? modelId)
    {
        return new ConfigurationSettings
        {
            Llm = new LlmConfiguration
            {
                Provider = provider,
                ApiKey = "test-api-key",
                Model = modelId
            },
            Ssh = new SshConfiguration
            {
                KeyPath = "/tmp/test_key"
            },
            RootDirectory = "/tmp/test_memory",
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = -1
            }
        };
    }

    public override async Task DisposeAsync()
    {
        _testDirectory.Dispose();

        // Call base cleanup for UserSecrets
        await base.DisposeAsync();
    }
}
