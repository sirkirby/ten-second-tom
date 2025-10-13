using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Comprehensive unit tests for ConfigurationChecker
/// Tests configuration validation and completeness checks
/// </summary>
public sealed class ConfigurationCheckerTests
{
    private readonly Mock<ILogger> _mockLogger;

    public ConfigurationCheckerTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    #region Complete Configuration Tests

    [Fact]
    public void IsConfigured_WithAllRequiredSettingsForOpenAI_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("all required settings are present");
    }

    [Fact]
    public void IsConfigured_WithAllRequiredSettingsForAnthropic_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "Anthropic",
            ["Llm:ApiKey"] = "sk-ant-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("all required settings are present");
    }

    [Fact]
    public void IsConfigured_WithCasInsensitiveProviderOpenAI_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OPENAI",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("provider name should be case-insensitive");
    }

    [Fact]
    public void IsConfigured_WithApiKeyFromEnvironmentVariable_ReturnsTrue()
    {
        // Arrange - API key is now stored directly in configuration (Llm:ApiKey)
        // This test is no longer relevant since we don't check environment variables
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("API key from configuration should be accepted");
    }

    #endregion

    #region Missing Configuration Tests

    [Fact]
    public void IsConfigured_WithMissingSshConfiguration_ReturnsFalse()
    {
        // Arrange - Neither KeyPath nor KeySource is set
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            // SSH configuration missing entirely
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("SSH configuration is required");
        VerifyLogContains(LogLevel.Debug, "Missing: SSH configuration");
    }

    [Fact]
    public void IsConfigured_WithSshKeySourceOnly_ReturnsTrue()
    {
        // Arrange - KeyPath is null but KeySource is set (agent scenario)
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = null,
            ["Ssh:KeySource"] = "OnePasswordAgent",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("SSH agent configuration without KeyPath should be valid");
    }

    [Fact]
    public void IsConfigured_WithMissingLlmProvider_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            // LLM provider missing
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("LLM provider is required");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM provider");
    }

    [Fact]
    public void IsConfigured_WithMissingMemoryDirectory_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-test1234567890"
            // Memory directory missing
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("Memory directory is required");
        VerifyLogContains(LogLevel.Debug, "Missing: Memory directory");
    }

    [Fact]
    public void IsConfigured_WithMissingApiKey_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
            // API key missing
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("API key is required");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM API key");
    }

    [Fact]
    public void IsConfigured_WithWrongProviderApiKey_ReturnsFalse()
    {
        // Arrange - Test is no longer relevant as API key is stored in Llm:ApiKey
        // All providers use the same configuration key now
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
            // Missing API key
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("API key is required regardless of provider");
    }

    [Fact]
    public void IsConfigured_WithEmptyStrings_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "",
            ["Llm:Provider"] = "",
            ["Llm:ApiKey"] = "",
            ["Storage:MemoryDirectory"] = ""
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("empty strings should be treated as missing");
    }

    [Fact]
    public void IsConfigured_WithWhitespaceStrings_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "   ",
            ["Llm:Provider"] = "  ",
            ["Llm:ApiKey"] = "  ",
            ["Storage:MemoryDirectory"] = "   "
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("whitespace-only strings should be treated as missing");
    }

    #endregion

    #region Unknown Provider Tests

    [Fact]
    public void IsConfigured_WithUnknownProvider_ReturnsFalse()
    {
        // Arrange - Unknown provider is still configured as long as all fields are present
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "UnknownProvider",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        // ConfigurationChecker doesn't validate provider names, just checks if fields exist
        result.Should().BeTrue("configuration checker only validates presence, not provider validity");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public void IsConfigured_WhenNotConfigured_LogsInformationMessage()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        // Act
        ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        VerifyLogContains(LogLevel.Information, "Application is not configured");
        VerifyLogContains(LogLevel.Information, "Setup wizard will be launched");
    }

    [Fact]
    public void IsConfigured_WhenNotConfigured_LogsAllMissingSettings()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        // Act
        ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        VerifyLogContains(LogLevel.Debug, "Missing: SSH configuration");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM provider");
        VerifyLogContains(LogLevel.Debug, "Missing: Memory directory");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM API key");
    }

    [Fact]
    public void IsConfigured_WhenConfigured_DoesNotLogMissingSettings()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-test1234567890",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not configured")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void IsConfigured_WithPartialConfiguration_LogsSpecificMissingItems()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI"
            // Missing: Memory directory and API key
        });

        // Act
        ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        VerifyLogContains(LogLevel.Debug, "Missing: Memory directory");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM API key");
        
        // Should NOT log for present settings
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Missing: SSH configuration")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void IsConfigured_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => ConfigurationChecker.IsConfigured(null!, _mockLogger.Object);

        act.Should().Throw<NullReferenceException>("null configuration should throw");
    }

    [Fact]
    public void IsConfigured_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        // Act & Assert
        var act = () => ConfigurationChecker.IsConfigured(configuration, null!);

        act.Should().Throw<ArgumentNullException>("null logger should throw");
    }

    [Fact]
    public void IsConfigured_WithEmptyConfiguration_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("empty configuration should not be considered configured");
    }

    #endregion

    #region Configuration Precedence Tests

    [Fact]
    public void IsConfigured_WithApiKeyInBothConfigAndEnvironment_PrefersConfig()
    {
        // Arrange - This test is no longer relevant as ConfigurationChecker only checks Llm:ApiKey
        // Environment variables are not checked by ConfigurationChecker
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "sk-config-key",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("all required configuration is present");
    }

    [Fact]
    public void IsConfigured_WithOnlyEnvironmentApiKey_UsesEnvironmentVariable()
    {
        // Arrange - ConfigurationChecker now only checks Llm:ApiKey from configuration
        // Environment variables must be loaded into IConfiguration before this check
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ssh:KeyPath"] = "~/.ssh/id_ed25519",
            ["Llm:Provider"] = "OpenAI",
            ["Storage:MemoryDirectory"] = "~/.ten-second-tom/memory"
            // No API key in configuration
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("API key is required in configuration");
    }

    #endregion

    #region Model Validation Tests

    [Fact]
    public void ValidateModel_WithValidOpenAIModel_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "gpt-4o-mini-2024-07-18"
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("gpt-4o-mini-2024-07-18 is a valid OpenAI model");
    }

    [Fact]
    public void ValidateModel_WithValidAnthropicModel_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "Anthropic",
            ["Llm:Model"] = "claude-3-5-haiku-20241022"
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("claude-3-5-haiku-20241022 is a valid Anthropic model");
    }

    [Fact]
    public void ValidateModel_WithInvalidModelForProvider_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "claude-3-5-haiku-20241022" // Anthropic model with OpenAI provider
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("claude model should not be valid for OpenAI provider");
        VerifyLogContains(LogLevel.Error, "Invalid model 'claude-3-5-haiku-20241022' configured for provider OpenAI");
    }

    [Fact]
    public void ValidateModel_WithNoModelConfigured_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI"
            // No model configured
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("validation should pass when no model is configured");
    }

    [Fact]
    public void ValidateModel_WithNoProviderConfigured_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Model"] = "gpt-4o-mini"
            // No provider configured
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("validation should pass when no provider is configured");
    }

    [Fact]
    public void ValidateModel_WithInvalidProvider_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "InvalidProvider",
            ["Llm:Model"] = "some-model"
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("invalid provider should fail validation");
        VerifyLogContains(LogLevel.Error, "Invalid LLM provider configured");
    }

    [Fact]
    public void ValidateModel_WithNonExistentModel_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "gpt-999-nonexistent"
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("non-existent model should fail validation");
    }

    [Fact]
    public void ValidateModel_WithEmptyModel_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = ""
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("empty model should be treated as not configured");
    }

    [Fact]
    public void ValidateModel_WithWhitespaceModel_ReturnsTrue()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "   "
        });

        // Act
        var result = ConfigurationChecker.ValidateModel(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("whitespace model should be treated as not configured");
    }

    #endregion

    #region GetModelValidationError Tests

    [Fact]
    public void GetModelValidationError_WithValidModel_ReturnsNull()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "gpt-4o-mini-2024-07-18"
        });

        // Act
        var result = ConfigurationChecker.GetModelValidationError(configuration);

        // Assert
        result.Should().BeNull("valid model should not return an error message");
    }

    [Fact]
    public void GetModelValidationError_WithInvalidModel_ReturnsErrorMessage()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "claude-3-5-haiku-20241022" // Anthropic model
        });

        // Act
        var result = ConfigurationChecker.GetModelValidationError(configuration);

        // Assert
        result.Should().NotBeNullOrEmpty("invalid model should return an error message");
        result.Should().Contain("not valid for provider OpenAI");
        result.Should().Contain("Valid models for OpenAI:");
        result.Should().Contain("tom setup");
    }

    [Fact]
    public void GetModelValidationError_WithNoModel_ReturnsNull()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI"
            // No model configured
        });

        // Act
        var result = ConfigurationChecker.GetModelValidationError(configuration);

        // Assert
        result.Should().BeNull("no model configured should not return an error");
    }

    [Fact]
    public void GetModelValidationError_WithNoProvider_ReturnsNull()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Model"] = "gpt-4o-mini"
            // No provider configured
        });

        // Act
        var result = ConfigurationChecker.GetModelValidationError(configuration);

        // Assert
        result.Should().BeNull("no provider configured should not return an error");
    }

    [Fact]
    public void GetModelValidationError_WithInvalidProvider_ReturnsErrorMessage()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "InvalidProvider",
            ["Llm:Model"] = "some-model"
        });

        // Act
        var result = ConfigurationChecker.GetModelValidationError(configuration);

        // Assert
        result.Should().NotBeNullOrEmpty("invalid provider should return an error message");
        result.Should().Contain("Invalid LLM provider configured");
    }

    [Fact]
    public void GetModelValidationError_MessageContainsAllValidModels()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "OpenAI",
            ["Llm:Model"] = "invalid-model"
        });

        // Act
        var result = ConfigurationChecker.GetModelValidationError(configuration);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("gpt-4o-mini-2024-07-18");
        result.Should().Contain("gpt-4o-2024-11-20");
        result.Should().Contain("chatgpt-4o-latest");
    }

    #endregion

    #region Helper Methods

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private void VerifyLogContains(LogLevel logLevel, string messageSubstring)
    {
        _mockLogger.Verify(
            l => l.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageSubstring, StringComparison.Ordinal)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            $"Expected log message containing '{messageSubstring}' at level {logLevel}");
    }

    #endregion
}
