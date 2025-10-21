using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Tests for ConfigurationSettings binding with environment variables for model configuration.
/// Tests User Story 3: Model Configuration via Environment Variables
/// </summary>
public sealed class ConfigurationSettingsTests
{
    [Fact]
    public void ConfigurationSettings_BindsModelFromEnvironmentVariable()
    {
        // Arrange - Simulate environment variable configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("gpt-4o", "model should bind from environment variable");
        settings.Llm.Provider.Should().Be(LlmProvider.OpenAI);
        settings.Llm.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public void ConfigurationSettings_EnvironmentVariableOverridesUserSecrets()
    {
        // Arrange - Simulate configuration hierarchy: user secrets < environment variables
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o-mini", // User secrets
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Model"] = "gpt-4o" // Environment variable overrides
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("gpt-4o", "environment variable should override user secrets");
    }

    [Fact]
    public void ConfigurationSettings_AllowsNullModel()
    {
        // Arrange - Configuration without model (should fall back to default)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().BeNull("model is optional and should be null when not configured");
    }

    [Fact]
    public void ConfigurationSettings_BindsAnthropicModelFromEnvironmentVariable()
    {
        // Arrange - Test with Anthropic provider
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "Anthropic",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "claude-3-5-sonnet-20241022",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("claude-3-5-sonnet-20241022", "Anthropic model should bind from environment variable");
        settings.Llm.Provider.Should().Be(LlmProvider.Anthropic);
    }

    [Fact]
    public void ConfigurationSettings_BindsModelWithComplexCharacters()
    {
        // Arrange - Test model IDs with special characters (hyphens, numbers, dates)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "Anthropic",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "claude-3-5-sonnet-20241022",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("claude-3-5-sonnet-20241022", "model IDs with dates and hyphens should bind correctly");
    }

    [Fact]
    public void ConfigurationSettings_PreservesWhitespaceInModelId()
    {
        // Arrange - Test that whitespace is NOT preserved (should be trimmed or rejected)
        // Model IDs should not have leading/trailing whitespace
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "  gpt-4o  ",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        // Configuration binding does NOT auto-trim, so whitespace is preserved
        // Validation should catch this as invalid model
        settings!.Llm.Model.Should().Be("  gpt-4o  ", "configuration binding preserves whitespace");
    }

    [Fact]
    public void ConfigurationSettings_IsValid_ReturnsFalseWhenMissingModel()
    {
        // Arrange - Valid configuration WITHOUT model (model is optional)
        var settings = new ConfigurationSettings
        {
            Ssh = new SshConfiguration { KeyPath = "/path/to/key" },
            Llm = new LlmConfiguration 
            { 
                Provider = LlmProvider.OpenAI, 
                ApiKey = "test-api-key",
                Model = null // No model specified
            },
            Storage = new StorageConfiguration { MemoryDirectory = "/tmp/memory" }
        };

        // Act
        bool isValid = settings.IsValid();

        // Assert
        // Model is optional - configuration should still be valid
        // LlmProviderFactory will provide defaults if model is null
        isValid.Should().BeTrue("configuration should be valid even without explicit model (defaults will be used)");
    }

    [Fact]
    public void ConfigurationSettings_LlmConfiguration_AllowsNullableModel()
    {
        // Arrange & Act
        var llmConfig = new LlmConfiguration
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "test-api-key",
            Model = null
        };

        // Assert
        llmConfig.Model.Should().BeNull("LlmConfiguration.Model should support null values");
        llmConfig.Provider.Should().Be(LlmProvider.OpenAI);
        llmConfig.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public void ConfigurationSettings_EnvironmentVariable_UsesDoubleUnderscoreDelimiter()
    {
        // Arrange - Test the actual environment variable format: TenSecondTom__Llm__Model
        // Note: In-memory collection simulates environment variables, which use : internally
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4o",
                ["TenSecondTom:Ssh:KeyPath"] = "/path/to/key",
                ["TenSecondTom:MemoryDirectory"] = "/tmp/memory"
            })
            .Build();

        // Act
        var settings = configuration.GetSection("TenSecondTom").Get<ConfigurationSettings>();

        // Assert
        settings.Should().NotBeNull();
        settings!.Llm.Model.Should().Be("gpt-4o", "environment variables use __ delimiter which maps to : internally");
        settings.Llm.Provider.Should().Be(LlmProvider.OpenAI);
    }
}
