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
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OpenAI",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["OPENAI_API_KEY"] = "sk-test1234567890"
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
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "Anthropic",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["ANTHROPIC_API_KEY"] = "sk-ant-test1234567890"
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
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OPENAI",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["OPENAI_API_KEY"] = "sk-test1234567890"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeTrue("provider name should be case-insensitive");
    }

    [Fact]
    public void IsConfigured_WithApiKeyFromEnvironmentVariable_ReturnsTrue()
    {
        // Arrange
        var originalEnvVar = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-env-test-key");
            
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
                ["TenSecondTom:LlmProvider"] = "OpenAI",
                ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory"
                // API key from environment variable
            });

            // Act
            var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

            // Assert
            result.Should().BeTrue("API key from environment variable should be accepted");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalEnvVar);
        }
    }

    #endregion

    #region Missing Configuration Tests

    [Fact]
    public void IsConfigured_WithMissingSshKeyPath_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            // SSH key path missing
            ["TenSecondTom:LlmProvider"] = "OpenAI",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["OPENAI_API_KEY"] = "sk-test1234567890"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("SSH key path is required");
        VerifyLogContains(LogLevel.Debug, "Missing: SSH key path");
    }

    [Fact]
    public void IsConfigured_WithMissingLlmProvider_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            // LLM provider missing
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["OPENAI_API_KEY"] = "sk-test1234567890"
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
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OpenAI",
            // Memory directory missing
            ["OPENAI_API_KEY"] = "sk-test1234567890"
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
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OpenAI",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory"
            // API key missing
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("API key is required");
        VerifyLogContains(LogLevel.Debug, "Missing: API key");
    }

    [Fact]
    public void IsConfigured_WithWrongProviderApiKey_ReturnsFalse()
    {
        // Arrange - OpenAI provider but Anthropic key
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OpenAI",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["ANTHROPIC_API_KEY"] = "sk-ant-test1234567890" // Wrong key for OpenAI provider
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("API key must match the selected provider");
    }

    [Fact]
    public void IsConfigured_WithEmptyStrings_ReturnsFalse()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKeyPath"] = "",
            ["TenSecondTom:LlmProvider"] = "",
            ["TenSecondTom:MemoryDirectory"] = "",
            ["OPENAI_API_KEY"] = ""
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
            ["TenSecondTom:Auth:PublicKeyPath"] = "   ",
            ["TenSecondTom:LlmProvider"] = "  ",
            ["TenSecondTom:MemoryDirectory"] = "   ",
            ["OPENAI_API_KEY"] = "  "
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
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "UnknownProvider",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["OPENAI_API_KEY"] = "sk-test1234567890"
        });

        // Act
        var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        result.Should().BeFalse("unknown provider should result in no API key match");
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
        VerifyLogContains(LogLevel.Debug, "Missing: SSH key path");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM provider");
        VerifyLogContains(LogLevel.Debug, "Missing: Memory directory");
        VerifyLogContains(LogLevel.Debug, "Missing: API key");
    }

    [Fact]
    public void IsConfigured_WhenConfigured_DoesNotLogMissingSettings()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OpenAI",
            ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
            ["OPENAI_API_KEY"] = "sk-test1234567890"
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
            ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
            ["TenSecondTom:LlmProvider"] = "OpenAI"
            // Missing: Memory directory and API key
        });

        // Act
        ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

        // Assert
        VerifyLogContains(LogLevel.Debug, "Missing: Memory directory");
        VerifyLogContains(LogLevel.Debug, "Missing: API key");
        
        // Should NOT log for present settings
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Missing: SSH key path")),
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
        // Arrange
        var originalEnvVar = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-env-key");
            
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
                ["TenSecondTom:LlmProvider"] = "OpenAI",
                ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory",
                ["OPENAI_API_KEY"] = "sk-config-key"
            });

            // Act
            var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

            // Assert
            result.Should().BeTrue("configuration API key should take precedence over environment variable");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalEnvVar);
        }
    }

    [Fact]
    public void IsConfigured_WithOnlyEnvironmentApiKey_UsesEnvironmentVariable()
    {
        // Arrange
        var originalEnvVar = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-env-only-key");
            
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["TenSecondTom:Auth:PublicKeyPath"] = "~/.ssh/id_ed25519",
                ["TenSecondTom:LlmProvider"] = "OpenAI",
                ["TenSecondTom:MemoryDirectory"] = "~/.ten-second-tom/memory"
                // No API key in configuration
            });

            // Act
            var result = ConfigurationChecker.IsConfigured(configuration, _mockLogger.Object);

            // Assert
            result.Should().BeTrue("environment variable should be used when config key is missing");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalEnvVar);
        }
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
