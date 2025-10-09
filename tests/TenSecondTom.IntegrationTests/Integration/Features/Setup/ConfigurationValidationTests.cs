using FluentAssertions;
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
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration { MemoryDirectory = _testDirectory.BasePath },
            Optional = new OptionalConfiguration { RetentionDays = 30, LogLevel = LogLevel.Information },
            CreatedAt = DateTime.UtcNow
        };

        // Setup storage to return this config
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(completeConfig));

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
            Ssh = new SshConfiguration { KeyPath = null }, // Missing SSH key
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration { MemoryDirectory = _testDirectory.BasePath },
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(incompleteConfig));

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
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = null }, // Missing API key
            Storage = new StorageConfiguration { MemoryDirectory = _testDirectory.BasePath },
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(incompleteConfig));

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
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration { MemoryDirectory = "" }, // Missing directory
            Optional = new OptionalConfiguration { RetentionDays = 30 },
            CreatedAt = DateTime.UtcNow
        };

        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(incompleteConfig));

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
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-valid-key" },
            Storage = new StorageConfiguration { MemoryDirectory = _testDirectory.BasePath },
            Optional = new OptionalConfiguration { RetentionDays = -1 }, // Invalid retention
            CreatedAt = DateTime.UtcNow
        };

        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(incompleteConfig));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail for negative retention days");
    }

    [Fact]
    public async Task ConfigValidation_WithNoConfiguration_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Failure("No configuration found"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Validate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail when no configuration exists");
        (result.Error?.Contains("not", StringComparison.OrdinalIgnoreCase) ?? false)
            .Should().BeTrue("error should indicate configuration not found");
    }

    [Fact]
    public async Task ConfigValidation_ProvidesHelpfulErrorMessages()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<ConfigCommandHandler>();
        
        var storageService = _serviceProvider.GetRequiredService<IConfigurationStorageService>();
        var mockStorage = Mock.Get(storageService);
        mockStorage.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Failure("No configuration found"));

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
