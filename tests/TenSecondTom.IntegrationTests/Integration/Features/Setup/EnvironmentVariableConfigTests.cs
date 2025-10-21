using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.Secrets;

namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration tests for environment variable configuration precedence.
/// Tests User Story 3: Model Configuration via Environment Variables
/// Verifies that environment variables override user secrets in the configuration hierarchy.
/// </summary>
public sealed class EnvironmentVariableConfigTests : IDisposable
{
    private readonly TemporaryTestDirectory _testDirectory;
    private readonly string _testUserSecretsId;

    public EnvironmentVariableConfigTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _testUserSecretsId = $"TenSecondTom-Test-EnvVar-{Guid.NewGuid()}";
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cleanup must not throw")]
    public void Dispose()
    {
        _testDirectory.Dispose();
        
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
                Thread.Sleep(100);
                try
                {
                    Directory.Delete(userSecretsPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task EnvironmentVariable_OverridesUserSecretsModel()
    {
        // Arrange - Save configuration with one model to user secrets
        using var serviceProvider = BuildTestServiceProvider();
        var storageService = serviceProvider.GetRequiredService<IConfigurationStorageService>();
        
        var configurationWithModel = new ConfigurationSettings
        {
            Ssh = new SshConfiguration { KeyPath = "/home/user/.ssh/id_ed25519" },
            Llm = new LlmConfiguration 
            { 
                Provider = LlmProvider.OpenAI, 
                ApiKey = "sk-test-key",
                Model = "gpt-4o-mini" // User secrets model
            },
            Storage = new StorageConfiguration { MemoryDirectory = _testDirectory.BasePath }
        };

        var saveResult = await storageService.SaveAsync(configurationWithModel, CancellationToken.None);
        saveResult.IsSuccess.Should().BeTrue();

        // Act - Build configuration with environment variable override
        // Simulate loading from user secrets file, then override with environment variable
        var userSecretsPath = SecretsHelper.GetUserSecretsPath(_testUserSecretsId);
        
        var configWithEnvOverride = new ConfigurationBuilder()
            .AddJsonFile(userSecretsPath, optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "gpt-4o" // Environment variable override
            })
            .Build();

        var settings = configWithEnvOverride.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert - Environment variable should override user secrets
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("gpt-4o", "environment variable should override user secrets model");
        settings.Llm.Provider.Should().Be(LlmProvider.OpenAI);
    }

    [Fact]
    public void EnvironmentVariable_OverridesUserSecretsProvider()
    {
        // Arrange - Simulate user secrets with OpenAI provider
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI", // User secrets
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
    public void EnvironmentVariable_PartialOverride_PreservesOtherUserSecretsValues()
    {
        // Arrange - Only override model via environment, keep other settings from user secrets
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // User secrets layer
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "sk-user-secrets-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini",
                ["TenSecondTom:Ssh:KeyPath"] = "/user/secrets/key",
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
        settings.Llm.Provider.Should().Be(LlmProvider.OpenAI, "provider should come from user secrets");
        settings.Llm.ApiKey.Should().Be("sk-user-secrets-key", "API key should come from user secrets");
        settings.Ssh.KeyPath.Should().Be("/user/secrets/key", "SSH key path should come from user secrets");
    }

    [Fact]
    public void EnvironmentVariable_WithEmptyModel_OverridesUserSecretsToNull()
    {
        // Arrange - Environment variable with empty string should clear user secrets value
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "sk-test-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini", // User secrets
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
        settings!.Llm.Model.Should().BeNullOrEmpty("empty environment variable should clear user secrets value");
    }

    [Fact]
    public void ConfigurationHierarchy_ShowsCorrectPrecedence()
    {
        // Arrange - Full configuration hierarchy: appsettings < user secrets < environment
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
            // Layer 2: user secrets
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "user-secrets-model",
                ["TenSecondTom:Llm:ApiKey"] = "user-secrets-key"
            })
            // Layer 3: environment variables
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "environment-model"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("environment-model", "environment variables have highest precedence");
        settings.Llm.ApiKey.Should().Be("user-secrets-key", "API key should come from user secrets (not overridden by env)");
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

    private ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Configure with test user secrets ID
        var userSecretsPath = SecretsHelper.GetUserSecretsPath(_testUserSecretsId);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(userSecretsPath, optional: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        // Register storage service with test configuration
        services.AddSingleton<IConfigurationStorageService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<UserSecretsStorageService>>();
            return new UserSecretsStorageService(logger, _testUserSecretsId);
        });

        return services.BuildServiceProvider();
    }
}
