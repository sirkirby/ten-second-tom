using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Comprehensive unit tests for ConfigurationChecker
/// Tests configuration validation and completeness checks
/// </summary>
public sealed class ConfigurationCheckerTests
{
    private readonly Mock<ILogger<ConfigurationChecker>> _mockLogger;
    private readonly EmbeddedPromptTemplateLoader _embeddedTemplateLoader;

    public ConfigurationCheckerTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationChecker>>();
        var yamlParser = new YamlFrontMatterParser(Mock.Of<ILogger<YamlFrontMatterParser>>());
        _embeddedTemplateLoader = new EmbeddedPromptTemplateLoader(
            baseDirectory: null,
            yamlParser: yamlParser);
    }

    #region Complete Configuration Tests

    [Fact]
    public void IsConfigured_WithAllRequiredSettingsForOpenAI_ReturnsTrue()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("all required settings are present");
    }

    [Fact]
    public void IsConfigured_WithAllRequiredSettingsForAnthropic_ReturnsTrue()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.Anthropic,
                ApiKey = "sk-ant-test1234567890",
                Model = LlmConstants.AnthropicModels.ClaudeHaiku,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("all required settings are present");
    }

    [Fact]
    public void IsConfigured_WithCaseInsensitiveProviderOpenAI_ReturnsTrue()
    {
        // Arrange - Provider enum is case-sensitive, but parsing should handle it
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("provider should be properly configured");
    }

    [Fact]
    public void IsConfigured_WithApiKeyFromConfiguration_ReturnsTrue()
    {
        // Arrange - API key is now stored directly in LlmOptions
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("API key from configuration should be accepted");
    }

    #endregion

    #region Missing Configuration Tests

    [Fact]
    public void IsConfigured_WithMissingSshConfiguration_ReturnsFalse()
    {
        // Arrange - Neither KeyPath nor KeySource is set
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: null, // SSH configuration missing entirely
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("SSH configuration is required");
        VerifyLogContains(LogLevel.Debug, "Missing: SSH configuration");
    }

    [Fact]
    public void IsConfigured_WithSshKeySourceOnly_ReturnsTrue()
    {
        // Arrange - KeyPath is null but KeySource is set (agent scenario)
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = null,
                KeySource = SshKeySource.OnePasswordAgent,
                AgentSocketPath = "/tmp/1password.sock"
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("SSH agent configuration without KeyPath should be valid");
    }

    [Fact]
    public void IsConfigured_WithMissingLlmProvider_ReturnsFalse()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: null, // LLM configuration missing
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("LLM provider is required");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM provider");
    }

    [Fact]
    public void IsConfigured_WithMissingRootDirectory_ReturnsFalse()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: null); // Root directory missing

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("Root directory is required");
        VerifyLogContains(LogLevel.Debug, "Missing: Root directory");
    }

    [Fact]
    public void IsConfigured_WithMissingApiKey_ReturnsFalse()
    {
        // Arrange - Create LlmOptions without API key using Mock to bypass required property
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "", // Empty API key
            Model = LlmConstants.OpenAIModels.GPTMini,
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("API key is required");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM API key");
    }

    [Fact]
    public void IsConfigured_WithWrongProviderApiKey_ReturnsFalse()
    {
        // Arrange - API key is stored in Llm:ApiKey for all providers
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "", // Missing API key
            Model = LlmConstants.OpenAIModels.GPTMini,
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("API key is required regardless of provider");
    }

    [Fact]
    public void IsConfigured_WithEmptyStrings_ReturnsFalse()
    {
        // Arrange - Create options with empty strings using Mock
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "",
            Model = "",
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var mockAuthOptions = new Mock<IOptions<AuthOptions>>();
        var authOptionsValue = new AuthOptions
        {
            KeyPath = "",
            KeySource = SshKeySource.FileSystem
        };
        mockAuthOptions.Setup(x => x.Value).Returns(authOptionsValue);

        var mockStorageOptions = new Mock<IOptions<StorageOptions>>();
        var storageOptionsValue = new StorageOptions
        {
            RootDirectory = ""
        };
        mockStorageOptions.Setup(x => x.Value).Returns(storageOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            mockAuthOptions.Object,
            mockStorageOptions.Object,
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("empty strings should be treated as missing");
    }

    [Fact]
    public void IsConfigured_WithWhitespaceStrings_ReturnsFalse()
    {
        // Arrange - Create options with whitespace strings using Mock
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "   ",
            Model = "  ",
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var mockAuthOptions = new Mock<IOptions<AuthOptions>>();
        var authOptionsValue = new AuthOptions
        {
            KeyPath = "   ",
            KeySource = SshKeySource.FileSystem
        };
        mockAuthOptions.Setup(x => x.Value).Returns(authOptionsValue);

        var mockStorageOptions = new Mock<IOptions<StorageOptions>>();
        var storageOptionsValue = new StorageOptions
        {
            RootDirectory = "   "
        };
        mockStorageOptions.Setup(x => x.Value).Returns(storageOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            mockAuthOptions.Object,
            mockStorageOptions.Object,
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("whitespace-only strings should be treated as missing");
    }

    #endregion

    #region Unknown Provider Tests

    [Fact]
    public void IsConfigured_WithUnknownProvider_ReturnsTrue()
    {
        // Arrange - Any valid LlmProvider enum value will work
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("configuration checker only validates presence, not provider validity");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public void IsConfigured_WhenNotConfigured_LogsInformationMessage()
    {
        // Arrange
        var checker = CreateConfigurationChecker();

        // Act
        checker.IsConfigured();

        // Assert
        VerifyLogContains(LogLevel.Information, "Application is not configured");
        VerifyLogContains(LogLevel.Information, "Setup wizard will be launched");
    }

    [Fact]
    public void IsConfigured_WhenNotConfigured_LogsAllMissingSettings()
    {
        // Arrange
        var checker = CreateConfigurationChecker();

        // Act
        checker.IsConfigured();

        // Assert
        VerifyLogContains(LogLevel.Debug, "Missing: SSH configuration");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM provider");
        VerifyLogContains(LogLevel.Debug, "Missing: Root directory");
        VerifyLogContains(LogLevel.Debug, "Missing: LLM API key");
    }

    [Fact]
    public void IsConfigured_WhenConfigured_DoesNotLogMissingSettings()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        checker.IsConfigured();

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
        // Arrange - Missing API key and memory directory
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "", // Missing API key
            Model = LlmConstants.OpenAIModels.GPTMini,
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            null, // Missing storage options
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        checker.IsConfigured();

        // Assert
        VerifyLogContains(LogLevel.Debug, "Missing: Root directory");
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
    public void IsConfigured_WithNullConfiguration_DoesNotThrow()
    {
        // Arrange - Create checker with null options
        var checker = CreateConfigurationChecker();

        // Act & Assert - Should not throw, just return false
        var result = checker.IsConfigured();
        result.Should().BeFalse("null configuration should not be considered configured");
    }

    [Fact]
    public void IsConfigured_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ConfigurationChecker(
            Options.Create(new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            }),
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            null!);

        act.Should().Throw<ArgumentNullException>("null logger should throw");
    }

    [Fact]
    public void IsConfigured_WithEmptyConfiguration_ReturnsFalse()
    {
        // Arrange
        var checker = CreateConfigurationChecker();

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("empty configuration should not be considered configured");
    }

    #endregion

    #region Configuration Precedence Tests

    [Fact]
    public void IsConfigured_WithApiKeyInConfiguration_ReturnsTrue()
    {
        // Arrange - API key is stored in LlmOptions
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-config-key",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeTrue("all required configuration is present");
    }

    [Fact]
    public void IsConfigured_WithOnlyEnvironmentApiKey_RequiresConfiguration()
    {
        // Arrange - ConfigurationChecker only checks LlmOptions
        // Environment variables must be loaded into IConfiguration before this check
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "", // No API key in options
            Model = LlmConstants.OpenAIModels.GPTMini,
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.IsConfigured();

        // Assert
        result.Should().BeFalse("API key is required in configuration");
    }

    #endregion

    #region Model Validation Tests

    [Fact]
    public void ValidateModel_WithValidOpenAIModel_ReturnsTrue()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeTrue($"{LlmConstants.OpenAIModels.GPTMini} is a valid OpenAI model");
    }

    [Fact]
    public void ValidateModel_WithValidAnthropicModel_ReturnsTrue()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.Anthropic,
                ApiKey = "sk-ant-test1234567890",
                Model = LlmConstants.AnthropicModels.ClaudeHaiku,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeTrue($"{LlmConstants.AnthropicModels.ClaudeHaiku} is a valid Anthropic model");
    }

    [Fact]
    public void ValidateModel_WithInvalidModelForProvider_ReturnsFalse()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.AnthropicModels.ClaudeHaiku, // Anthropic model with OpenAI provider
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeFalse("claude model should not be valid for OpenAI provider");
        VerifyLogContains(LogLevel.Error, $"Invalid model '{LlmConstants.AnthropicModels.ClaudeHaiku}' configured for provider OpenAI");
    }

    [Fact]
    public void ValidateModel_WithNoModelConfigured_ReturnsTrue()
    {
        // Arrange
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "sk-test1234567890",
            Model = "", // No model configured
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeTrue("validation should pass when no model is configured");
    }

    [Fact]
    public void ValidateModel_WithNoProviderConfigured_ReturnsTrue()
    {
        // Arrange - Create checker without LLM options
        var checker = CreateConfigurationChecker(
            llmOptions: null,
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeTrue("validation should pass when no provider is configured");
    }

    [Fact]
    public void ValidateModel_WithNonExistentModel_ReturnsFalse()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = "gpt-999-nonexistent",
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeFalse("non-existent model should fail validation");
    }

    [Fact]
    public void ValidateModel_WithEmptyModel_ReturnsTrue()
    {
        // Arrange
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "sk-test1234567890",
            Model = "",
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeTrue("empty model should be treated as not configured");
    }

    [Fact]
    public void ValidateModel_WithWhitespaceModel_ReturnsTrue()
    {
        // Arrange
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "sk-test1234567890",
            Model = "   ",
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.ValidateModel();

        // Assert
        result.Should().BeTrue("whitespace model should be treated as not configured");
    }

    #endregion

    #region GetModelValidationError Tests

    [Fact]
    public void GetModelValidationError_WithValidModel_ReturnsNull()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.OpenAIModels.GPTMini,
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.GetModelValidationError();

        // Assert
        result.Should().BeNull("valid model should not return an error message");
    }

    [Fact]
    public void GetModelValidationError_WithInvalidModel_ReturnsErrorMessage()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = LlmConstants.AnthropicModels.ClaudeHaiku, // Anthropic model
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.GetModelValidationError();

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
        var mockLlmOptions = new Mock<IOptions<LlmOptions>>();
        var llmOptionsValue = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "sk-test1234567890",
            Model = "", // No model configured
            MaxInputTokens = 100000
        };
        mockLlmOptions.Setup(x => x.Value).Returns(llmOptionsValue);

        var checker = new ConfigurationChecker(
            mockLlmOptions.Object,
            Options.Create(new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            }),
            Options.Create(new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            }),
            _embeddedTemplateLoader,
            _mockLogger.Object);

        // Act
        var result = checker.GetModelValidationError();

        // Assert
        result.Should().BeNull("no model configured should not return an error");
    }

    [Fact]
    public void GetModelValidationError_WithNoProvider_ReturnsNull()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: null, // No provider configured
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.GetModelValidationError();

        // Assert
        result.Should().BeNull("no provider configured should not return an error");
    }

    [Fact]
    public void GetModelValidationError_MessageContainsAllValidModels()
    {
        // Arrange
        var checker = CreateConfigurationChecker(
            llmOptions: new LlmOptions
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890",
                Model = "invalid-model",
                MaxInputTokens = 100000
            },
            authOptions: new AuthOptions
            {
                KeyPath = "~/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem
            },
            storageOptions: new StorageOptions
            {
                RootDirectory = "~/.ten-second-tom/memory"
            });

        // Act
        var result = checker.GetModelValidationError();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(LlmConstants.OpenAIModels.GPTMini);
        result.Should().Contain(LlmConstants.OpenAIModels.GPTStandard);
        result.Should().Contain(LlmConstants.OpenAIModels.GPTNano);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a ConfigurationChecker with the specified options.
    /// If any option is null, creates an empty/invalid option that will fail validation.
    /// </summary>
    private ConfigurationChecker CreateConfigurationChecker(
        LlmOptions? llmOptions = null,
        AuthOptions? authOptions = null,
        StorageOptions? storageOptions = null)
    {
        var llmOptionsWrapper = llmOptions != null
            ? Options.Create(llmOptions)
            : null;

        var authOptionsWrapper = authOptions != null
            ? Options.Create(authOptions)
            : null;

        var storageOptionsWrapper = storageOptions != null
            ? Options.Create(storageOptions)
            : null;

        return new ConfigurationChecker(
            llmOptionsWrapper,
            authOptionsWrapper,
            storageOptionsWrapper,
            _embeddedTemplateLoader,
            _mockLogger.Object);
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
