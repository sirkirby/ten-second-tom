using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Validation;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration tests for 'tom config llm' command flow
/// Tests end-to-end interactive model selection via config command
/// </summary>
[Collection(UserSecretsCollection.Name)]
public sealed class ConfigLlmCommandTests : UserSecretsTestFixture
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ISetupWizardUI> _mockWizard;

    public ConfigLlmCommandTests()
    {
        _mockWizard = new Mock<ISetupWizardUI>();

        // Setup common UI methods that don't affect test outcomes
        _mockWizard.Setup(w => w.ShowStepHeader(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()));
        _mockWizard.Setup(w => w.ShowWarning(It.IsAny<string>()));

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddHttpClient(); // Required by API key validators
        services.AddSingleton(_mockWizard.Object);

        // Use test appsettings.json file
        var testAppSettingsPath = Path.Combine(TestDirectory.BasePath, "appsettings.json");
        services.AddSingleton<IConfigurationStorageService>(sp =>
            new ConfigurationStorageService(
                sp.GetRequiredService<ILogger<ConfigurationStorageService>>(),
                testAppSettingsPath));

        // Mock app settings storage (not tested in these LLM config tests)
        var mockAppSettingsStorage = new Mock<IAppSettingsStorageService>();
        services.AddSingleton(mockAppSettingsStorage.Object);

        // Add IConfiguration with empty configuration (no overrides)
        var configBuilder = new ConfigurationBuilder();
        services.AddSingleton<IConfiguration>(configBuilder.Build());

        // Add required validators for ConfigCommandHandler
        services.AddTransient<IApiKeyValidator, OpenAIApiKeyValidator>();
        services.AddTransient<IApiKeyValidator, AnthropicApiKeyValidator>();

        services.AddTransient<ConfigCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();

        // Set up logger for the base fixture
        Logger = _serviceProvider.GetRequiredService<ILogger<UserSecretsTestFixture>>();
    }

    [Fact]
    public async Task ConfigLlm_WithNoExistingConfiguration_ShouldReturnFailure()
    {
        // Arrange
        // Setup mock to return null for provider selection (user cancels)
        _mockWizard.Setup(w => w.PromptForLlmProviderAsync(
                It.IsAny<LlmProvider?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmProvider?)null);
        
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - When no existing config + user cancels, should return cancellation message
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
        result.Error.Should().Contain("No changes were made");
    }

    [Fact]
    public async Task ConfigLlm_WithExistingConfiguration_ShouldPromptForProviderAndModel()
    {
        // Arrange
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var initialConfig = CreateValidConfiguration();
        await storageService.SaveAsync(initialConfig, CancellationToken.None);

        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm"
        };

        // Setup wizard mocks for interactive prompts
        _mockWizard.Setup(w => w.PromptForLlmProviderAsync(
                It.IsAny<LlmProvider?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmProvider.Anthropic);

        var selectedModel = new SupportedModel(
            Id: "claude-3-5-sonnet-20241022",
            DisplayName: "Claude 3.5 Sonnet",
            Provider: LlmProvider.Anthropic,
            CostTier: "Premium",
            Description: "Best model",
            IsDefault: true
        );

        _mockWizard.Setup(w => w.PromptForModelAsync(
                LlmProvider.Anthropic, 
                It.IsAny<string?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedModel);

        // Provider is changing from OpenAI to Anthropic, so API key prompt is required
        _mockWizard.Setup(w => w.PromptForApiKeyAsync(
                LlmProvider.Anthropic,
                null, // New provider, no current key shown
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("sk-ant-test_key_1234567890abcdefghijklmnopqrstuvwxyz");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Llm.Provider.Should().Be(LlmProvider.Anthropic);
        result.Value!.Llm.Model.Should().Be("claude-3-5-sonnet-20241022");
        result.Value!.Llm.ApiKey.Should().Be("sk-ant-test_key_1234567890abcdefghijklmnopqrstuvwxyz");
    }

    [Fact]
    public async Task ConfigLlm_WhenUserCancelsProviderSelection_ShouldReturnFailure()
    {
        // Arrange
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var initialConfig = CreateValidConfiguration();
        await storageService.SaveAsync(initialConfig, CancellationToken.None);

        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm"
        };

        // Setup wizard to simulate cancellation
        _mockWizard.Setup(w => w.PromptForLlmProviderAsync(
                It.IsAny<LlmProvider?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmProvider?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ConfigLlm_WhenUserCancelsModelSelection_ShouldReturnFailure()
    {
        // Arrange
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var initialConfig = CreateValidConfiguration();
        await storageService.SaveAsync(initialConfig, CancellationToken.None);

        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm"
        };

        // Setup wizard mocks
        _mockWizard.Setup(w => w.PromptForLlmProviderAsync(
                It.IsAny<LlmProvider?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmProvider.OpenAI);

        _mockWizard.Setup(w => w.PromptForModelAsync(
                LlmProvider.OpenAI, 
                It.IsAny<string?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportedModel?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ConfigLlm_ShouldUpdateBothProviderAndModel()
    {
        // Arrange
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var initialConfig = CreateValidConfiguration() with
        {
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test",
                Model = "gpt-4"
            }
        };
        await storageService.SaveAsync(initialConfig, CancellationToken.None);

        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm"
        };

        // Setup wizard mocks for changing to Anthropic
        _mockWizard.Setup(w => w.PromptForLlmProviderAsync(
                LlmProvider.OpenAI, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmProvider.Anthropic);

        var anthropicModel = new SupportedModel(
            Id: "claude-3-5-haiku-20241022",
            DisplayName: "Claude 3.5 Haiku",
            Provider: LlmProvider.Anthropic,
            CostTier: "Budget",
            Description: "Fast and affordable",
            IsDefault: false
        );

        _mockWizard.Setup(w => w.PromptForModelAsync(
                LlmProvider.Anthropic, 
                null, // No current model when switching providers
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(anthropicModel);

        // Provider is changing, so API key prompt is required
        _mockWizard.Setup(w => w.PromptForApiKeyAsync(
                LlmProvider.Anthropic,
                null, // New provider, no current key shown
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("sk-ant-test_api_key_567890abcdefghijklmnopqrstuvwxyz");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Llm.Provider.Should().Be(LlmProvider.Anthropic);
        result.Value!.Llm.Model.Should().Be("claude-3-5-haiku-20241022");
        result.Value!.Llm.ApiKey.Should().Be("sk-ant-test_api_key_567890abcdefghijklmnopqrstuvwxyz");

        // Verify persistence
        var reloadedConfig = await storageService.LoadAsync(CancellationToken.None);
        reloadedConfig.Value!.Llm.Provider.Should().Be(LlmProvider.Anthropic);
        reloadedConfig.Value!.Llm.Model.Should().Be("claude-3-5-haiku-20241022");
        reloadedConfig.Value!.Llm.ApiKey.Should().Be("sk-ant-test_api_key_567890abcdefghijklmnopqrstuvwxyz");
    }

    [Fact]
    public async Task ConfigLlm_WithSameProvider_ShouldHighlightCurrentModel()
    {
        // Arrange
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var initialConfig = CreateValidConfiguration() with
        {
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test",
                Model = "gpt-4o"
            }
        };
        await storageService.SaveAsync(initialConfig, CancellationToken.None);

        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm"
        };

        // Setup wizard mocks - user keeps same provider
        _mockWizard.Setup(w => w.PromptForLlmProviderAsync(
                LlmProvider.OpenAI, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmProvider.OpenAI);

        // Model prompt should receive current model for highlighting
        _mockWizard.Setup(w => w.PromptForModelAsync(
                LlmProvider.OpenAI, 
                "gpt-4o", // Should pass current model
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupportedModel(
                Id: "gpt-4o-mini-2024-07-18",
                DisplayName: "GPT-4o Mini",
                Provider: LlmProvider.OpenAI,
                CostTier: "Budget",
                Description: "Affordable mini model",
                IsDefault: false
            ));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockWizard.Verify(w => w.PromptForModelAsync(
            LlmProvider.OpenAI,
            "gpt-4o",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public override async Task DisposeAsync()
    {
        // Dispose service provider
        _serviceProvider?.Dispose();

        // Call base cleanup for UserSecrets
        await base.DisposeAsync();
    }

    private static ConfigurationSettings CreateValidConfiguration()
    {
        return new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890abcdef",
                Model = "gpt-4"
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = "~/.ten-second-tom/memory",
                CreateIfMissing = true
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 30,
                EnableTelemetry = false
            },
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = null,
            ConfigurationVersion = "1.0"
        };
    }
}
