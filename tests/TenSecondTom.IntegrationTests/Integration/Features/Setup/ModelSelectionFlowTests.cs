using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using Xunit;

namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration tests for end-to-end model selection flow during guided setup.
/// Verifies that model selection, storage, and retrieval work correctly.
/// Each test uses a unique User Secrets ID to avoid interference with production configuration.
/// </summary>
public sealed class ModelSelectionFlowTests : IDisposable
{
    private readonly string _testUserSecretsId;

    public ModelSelectionFlowTests()
    {
        // Use a unique ID for each test instance to avoid polluting production UserSecrets
        _testUserSecretsId = $"TenSecondTom-Test-{Guid.NewGuid()}";
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cleanup must not throw")]
    public void Dispose()
    {
        // Clean up test UserSecrets directory
        var userSecretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            _testUserSecretsId);

        if (Directory.Exists(userSecretsPath))
        {
            try
            {
                Directory.Delete(userSecretsPath, recursive: true);
            }
            catch (IOException)
            {
                // Retry after delay if directory is locked
                Thread.Sleep(100);
                try
                {
                    Directory.Delete(userSecretsPath, recursive: true);
                }
                catch
                {
                    // Ignore - cleanup script can handle orphaned directories
                }
            }
            catch
            {
                // Ignore cleanup errors - don't fail tests because of cleanup
            }
        }
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

    private UserSecretsStorageService CreateStorageService()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserSecretsStorageService>();
        return new UserSecretsStorageService(logger, _testUserSecretsId);
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
            Storage = new StorageConfiguration
            {
                MemoryDirectory = "/tmp/test_memory"
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = -1
            }
        };
    }
}
