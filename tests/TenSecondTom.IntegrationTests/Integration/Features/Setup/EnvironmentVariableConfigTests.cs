using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration tests for environment variable configuration precedence.
/// Tests User Story 3: Model Configuration via Environment Variables
/// Verifies that environment variables override appsettings.json in the configuration hierarchy.
/// </summary>
[Collection(UserSecretsCollection.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "IAsyncLifetime pattern used instead of IDisposable")]
public sealed class EnvironmentVariableConfigTests : UserSecretsTestFixture
{
    private readonly TemporaryTestDirectory _testDirectory;

    public EnvironmentVariableConfigTests()
    {
        _testDirectory = new TemporaryTestDirectory();

        // Set up logger for the base fixture
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        Logger = loggerFactory.CreateLogger<UserSecretsTestFixture>();
    }

    public override async Task DisposeAsync()
    {
        _testDirectory.Dispose();

        // Call base cleanup for UserSecrets
        await base.DisposeAsync();
    }

    [Fact]
    public async Task EnvironmentVariable_OverridesAppSettingsModel()
    {
        // Arrange - Save configuration with one model to appsettings.json
        var testAppSettingsPath = Path.Combine(_testDirectory.BasePath, "appsettings.json");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<ConfigurationStorageService>();
        var configuration = new ConfigurationBuilder().Build();
        var storageService = new ConfigurationStorageService(logger, configuration, testAppSettingsPath);

        var configurationWithModel = new ConfigurationSettings
        {
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test-key",
                Model = "gpt-4o-mini" // appsettings.json model
            },
            Storage = new StorageConfiguration { MemoryDirectory = _testDirectory.BasePath }
        };

        var saveResult = await storageService.SaveAsync(configurationWithModel, CancellationToken.None);
        saveResult.IsSuccess.Should().BeTrue();

        // Act - Build configuration with environment variable override
        // Load from appsettings.json, then override with environment variable
        var configWithEnvOverride = new ConfigurationBuilder()
            .AddJsonFile(testAppSettingsPath, optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "gpt-4o" // Environment variable override
            })
            .Build();

        var settings = configWithEnvOverride.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert - Environment variable should override appsettings.json
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("gpt-4o", "environment variable should override appsettings.json model");
        settings.Llm.Provider.Should().Be(LlmProvider.OpenAI);
    }

    [Fact]
    public void EnvironmentVariable_OverridesAppSettingsProvider()
    {
        // Arrange - Simulate appsettings.json with OpenAI provider
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI", // appsettings.json
                ["TenSecondTom:Llm:ApiKey"] = "sk-test-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "Anthropic", // Environment variable override
                ["TenSecondTom:Llm:Model"] = "claude-3-5-sonnet-20241022" // Also override model
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Provider.Should().Be(LlmProvider.Anthropic, "environment variable should override provider");
        settings.Llm.Model.Should().Be("claude-3-5-sonnet-20241022", "environment variable should override model");
    }

    [Fact]
    public void EnvironmentVariable_PartialOverride_PreservesOtherAppSettingsValues()
    {
        // Arrange - Only override model via environment, keep other settings from appsettings.json
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // appsettings.json layer
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "sk-appsettings-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini",
                ["TenSecondTom:Ssh:KeyPath"] = "/appsettings/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Environment variable layer - only override model
                ["TenSecondTom:Llm:Model"] = "gpt-4o"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("gpt-4o", "model should come from environment variable");
        settings.Llm.Provider.Should().Be(LlmProvider.OpenAI, "provider should come from appsettings.json");
        settings.Llm.ApiKey.Should().Be("sk-appsettings-key", "API key should come from appsettings.json");
        settings.Ssh.KeyPath.Should().Be("/appsettings/key", "SSH key path should come from appsettings.json");
    }

    [Fact]
    public void EnvironmentVariable_WithEmptyModel_OverridesAppSettingsToNull()
    {
        // Arrange - Environment variable with empty string should clear appsettings.json value
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "sk-test-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini", // appsettings.json
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "" // Environment variable with empty string
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        // Empty string from environment variable results in null binding
        settings!.Llm.Model.Should().BeNullOrEmpty("empty environment variable should clear appsettings.json value");
    }

    [Fact]
    public void ConfigurationHierarchy_ShowsCorrectPrecedence()
    {
        // Arrange - Configuration hierarchy: appsettings.json < appsettings.{env}.json < environment variables
        var configuration = new ConfigurationBuilder()
            // Layer 1: appsettings.json
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "default-model-from-appsettings",
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "default-key",
                ["TenSecondTom:Ssh:KeyPath"] = "/default/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            // Layer 2: appsettings.{environment}.json
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "environment-file-model",
                ["TenSecondTom:Llm:ApiKey"] = "environment-file-key"
            })
            // Layer 3: environment variables
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "environment-variable-model"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("environment-variable-model", "environment variables have highest precedence");
        settings.Llm.ApiKey.Should().Be("environment-file-key", "API key should come from environment-specific appsettings (not overridden by env)");
        settings.Ssh.KeyPath.Should().Be("/default/key", "SSH key should come from appsettings (lowest precedence)");
    }

    [Fact]
    public void EnvironmentVariable_SwitchingProviders_UpdatesModelCorrectly()
    {
        // Arrange - Switch from OpenAI to Anthropic via environment variables
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "sk-openai-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "Anthropic",
                ["TenSecondTom:Llm:ApiKey"] = "sk-ant-key",
                ["TenSecondTom:Llm:Model"] = "claude-3-5-haiku-20241022"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Provider.Should().Be(LlmProvider.Anthropic, "provider should be switched via environment");
        settings.Llm.Model.Should().Be("claude-3-5-haiku-20241022", "model should match new provider");
        settings.Llm.ApiKey.Should().Be("sk-ant-key", "API key should match new provider");
    }

}
