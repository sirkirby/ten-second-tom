using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Setup;

namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration test for Scenario 7: Configuration Validation
/// Tests config command validation for completeness
/// Validates detection of missing or invalid settings
/// Ensures helpful error messages for invalid configurations
/// </summary>
public sealed class ConfigurationValidationTests : IDisposable
{
    private readonly TemporaryTestDirectory _testDirectory;
    private readonly ServiceProvider _serviceProvider;

    public ConfigurationValidationTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _serviceProvider = BuildTestServiceProvider();
    }

    [Fact]
    public async Task ConfigValidation_WithCompleteConfiguration_ReturnsValid()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        
        var completeConfig = new ConfigurationSettings
        {
            RootDirectory = _testDirectory.BasePath,
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = 30, LogLevel = LogLevel.Information },
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsMonitor to return this config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(completeConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("validation should pass for complete config");
        result.Value.Should().NotBeNull();
        result.Value.IsValid().Should().BeTrue();
    }

    [Fact]
    public async Task ConfigValidation_WithMissingSshKey_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        var incompleteConfig = new ConfigurationSettings
        {
            RootDirectory = _testDirectory.BasePath,
            Ssh = new SshConfiguration { KeyPath = null }, // Missing SSH key
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsMonitor to return this config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(incompleteConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail for missing SSH key");
        result.Error.Should().Contain("failed", "error message should indicate validation failure");
    }

    [Fact]
    public async Task ConfigValidation_WithMissingApiKey_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        var incompleteConfig = new ConfigurationSettings
        {
            RootDirectory = _testDirectory.BasePath,
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = null }, // Missing API key
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsMonitor to return this config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(incompleteConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail for missing API key");
    }

    [Fact]
    public async Task ConfigValidation_WithMissingMemoryDirectory_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        var incompleteConfig = new ConfigurationSettings
        {
            RootDirectory = "", // Missing directory
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsMonitor to return this config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(incompleteConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail for missing memory directory");
    }

    [Fact]
    public async Task ConfigValidation_WithInvalidRetentionDays_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        var incompleteConfig = new ConfigurationSettings
        {
            RootDirectory = _testDirectory.BasePath,
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = -5 }, // Invalid retention (only -1 is valid for unlimited)
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsMonitor to return this config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(incompleteConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail for negative retention days other than -1");
    }

    [Fact]
    public async Task ConfigValidation_WithUnlimitedRetentionDays_ReturnsValid()
    {
        // Arrange - Test that -1 (unlimited) is valid
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        var configWithUnlimited = new ConfigurationSettings
        {
            RootDirectory = _testDirectory.BasePath,
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = -1 }, // -1 means unlimited
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsMonitor to return this config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(configWithUnlimited);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("validation should pass for -1 (unlimited) retention days");
        result.Value.Should().NotBeNull();
        result.Value.IsValid().Should().BeTrue();
    }

    [Fact]
    public async Task ConfigValidation_WithNoConfiguration_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        // Create an empty/default config (simulates missing configuration)
        var emptyConfig = new ConfigurationSettings
        {
            RootDirectory = string.Empty,
            Ssh = new SshConfiguration { KeyPath = null },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = null },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsSnapshot to return empty config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(emptyConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail when no configuration exists");
        result.Error.Should().Contain("setup", "error should provide actionable guidance");
    }

    [Fact]
    public async Task ConfigValidation_ProvidesHelpfulErrorMessages()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();

        // Create an empty/default config (simulates missing configuration)
        var emptyConfig = new ConfigurationSettings
        {
            RootDirectory = string.Empty,
            Ssh = new SshConfiguration { KeyPath = null },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = null },
            Storage = new StorageConfiguration(),
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        // Setup IOptionsSnapshot to return empty config
        var optionsMonitor = _serviceProvider.GetRequiredService<IOptionsMonitor<ConfigurationSettings>>();
        var mockMonitor = Mock.Get(optionsMonitor);
        mockMonitor.Setup(c => c.CurrentValue).Returns(emptyConfig);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty("error message should be provided");
        (result.Error!.Contains("Config.", StringComparison.OrdinalIgnoreCase) ||
         result.Error.Contains("configuration", StringComparison.OrdinalIgnoreCase) ||
         result.Error.Contains("setup", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue("error should be descriptive");
    }

    private static ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Mock configuration storage
        var mockStorage = new Mock<IConfigurationStorageService>();
        services.AddSingleton(mockStorage.Object);

        // Mock app settings storage
        var mockAppSettingsStorage = new Mock<IAppSettingsStorageService>();
        services.AddSingleton(mockAppSettingsStorage.Object);

        // Add IConfiguration with empty configuration (no overrides)
        var configBuilder = new ConfigurationBuilder();
        services.AddSingleton<IConfiguration>(configBuilder.Build());

        // Mock IOptionsMonitor<ConfigurationSettings> (used by HandleShowAsync)
        var mockOptionsMonitor = new Mock<IOptionsMonitor<ConfigurationSettings>>();
        services.AddSingleton(mockOptionsMonitor.Object);

        // Mock ISetupWizardUI (required by ConfigCommandHandler)
        var mockWizard = new Mock<ISetupWizardUI>();
        services.AddSingleton(mockWizard.Object);

        // Mock API key validators (empty collection for now)
        services.AddSingleton<IEnumerable<IApiKeyValidator>>(
            new List<IApiKeyValidator>());

        // Add handler
        services.AddSingleton<ConfigCommandHandler>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _testDirectory?.Dispose();
    }
}
