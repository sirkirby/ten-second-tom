using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Tests for configuration loading hierarchy and priority.
/// </summary>
public sealed class ConfigurationTests
{
    [Fact]
    public void Configuration_LoadsDefaultsFromAppSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Act
        string? memoryDirectory = configuration["TenSecondTom:MemoryDirectory"];
        string? llmProvider = configuration["TenSecondTom:Llm:Provider"];

        // Assert
        memoryDirectory.Should().NotBeNullOrEmpty("default memory directory should be configured");
        llmProvider.Should().NotBeNullOrEmpty("default LLM provider should be configured");
    }

    [Fact]
    public void Configuration_UserSecretsOverrideAppSettings()
    {
        // Arrange - Simulate configuration hierarchy
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:OpenAI:ApiKey"] = "secret-from-user-secrets"
            })
            .Build();

        // Act
        string? apiKey = configuration["TenSecondTom:OpenAI:ApiKey"];

        // Assert
        apiKey.Should().Be("secret-from-user-secrets", "user secrets should override appsettings");
    }

    [Fact]
    public void Configuration_EnvironmentVariablesOverrideUserSecrets()
    {
        // Arrange - Simulate configuration hierarchy with environment variables
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:OpenAI:ApiKey"] = "secret-from-user-secrets"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:OpenAI:ApiKey"] = "secret-from-environment"
            })
            .Build();

        // Act
        string? apiKey = configuration["TenSecondTom:OpenAI:ApiKey"];

        // Assert
        apiKey.Should().Be("secret-from-environment", "environment variables should override user secrets");
    }

    [Fact]
    public void Configuration_CommandLineArgsOverrideEverything()
    {
        // Arrange - Simulate full configuration hierarchy
        string[] args = ["--TenSecondTom:Llm:Provider=OpenAI"];
        
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI" // User secrets
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "Anthropic" // Environment variables
            })
            .AddCommandLine(args) // Command line
            .Build();

        // Act
        string? provider = configuration["TenSecondTom:Llm:Provider"];

        // Assert
        provider.Should().Be("OpenAI", "command-line arguments should override everything");
    }

    [Fact]
    public void Configuration_ValidatesRequiredSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Intentionally missing required settings
            })
            .Build();

        // Act
        string? memoryDirectory = configuration["TenSecondTom:MemoryDirectory"];
        string? llmProvider = configuration["TenSecondTom:Llm:Provider"];

        // Assert
        memoryDirectory.Should().BeNull("missing required settings should be null");
        llmProvider.Should().BeNull("missing required settings should be null");
    }

    [Fact]
    public void Configuration_LoadsOpenAISettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Act
        string? maxInputTokens = configuration["TenSecondTom:Llm:MaxInputTokens"];
        string? provider = configuration["TenSecondTom:Llm:Provider"];

        // Assert
        provider.Should().NotBeNullOrEmpty("LLM provider should be configured");
        maxInputTokens.Should().NotBeNullOrEmpty("LLM max input tokens should be configured");
        
        // Validate max input tokens is a valid integer
        bool isValidInt = int.TryParse(maxInputTokens, out int tokens);
        isValidInt.Should().BeTrue("max input tokens should be a valid integer");
        tokens.Should().BeGreaterThan(0, "max input tokens should be positive");
    }

    [Fact]
    public void Configuration_SupportsOptionalModelSetting()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Act
        string? model = configuration["TenSecondTom:Llm:Model"];

        // Assert - Model is optional and can be null (defaults will be used)
        // If model is set, verify it's a non-empty string
        if (!string.IsNullOrEmpty(model))
        {
            model.Should().NotBeNullOrWhiteSpace("if model is specified, it should not be whitespace");
        }
    }

    [Fact]
    public void Configuration_LoadsDataRetentionSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Act
        string? defaultPolicy = configuration["TenSecondTom:DataRetention:DefaultPolicy"];
        string? autoPurge = configuration["TenSecondTom:DataRetention:AutoPurgeEnabled"];

        // Assert
        defaultPolicy.Should().NotBeNullOrEmpty("default retention policy should be configured");
        autoPurge.Should().NotBeNullOrEmpty("auto purge setting should be configured");
        
        // Validate auto purge is a valid boolean
        bool isValidBool = bool.TryParse(autoPurge, out _);
        isValidBool.Should().BeTrue("auto purge should be a valid boolean");
    }

    [Fact]
    public void Configuration_InvalidMaxTokens_CanBeDetected()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:OpenAI:MaxTokens"] = "not-a-number"
            })
            .Build();

        // Act
        string? maxTokens = configuration["TenSecondTom:OpenAI:MaxTokens"];
        bool isValidInt = int.TryParse(maxTokens, out _);

        // Assert
        isValidInt.Should().BeFalse("invalid integer values should be detectable");
    }

    [Fact]
    public void Configuration_InvalidBooleanValue_CanBeDetected()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:DataRetention:AutoPurgeEnabled"] = "not-a-boolean"
            })
            .Build();

        // Act
        string? autoPurge = configuration["TenSecondTom:DataRetention:AutoPurgeEnabled"];
        bool isValidBool = bool.TryParse(autoPurge, out _);

        // Assert
        isValidBool.Should().BeFalse("invalid boolean values should be detectable");
    }
}
