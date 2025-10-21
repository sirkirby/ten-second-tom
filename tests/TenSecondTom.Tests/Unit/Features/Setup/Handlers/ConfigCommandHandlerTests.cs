using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Validation;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.Setup.Handlers;

/// <summary>
/// Comprehensive unit tests for ConfigCommandHandler
/// Tests all actions: Show, Set, Reset, Validate
/// Covers error handling, validation, and configuration updates
/// </summary>
public sealed class ConfigCommandHandlerTests
{
    private readonly Mock<IConfigurationStorageService> _mockStorageService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ISetupWizardUI> _mockSetupWizard;
    private readonly Mock<IApiKeyValidator> _mockOpenAIValidator;
    private readonly Mock<IApiKeyValidator> _mockAnthropicValidator;
    private readonly Mock<ILogger<ConfigCommandHandler>> _mockLogger;
    private readonly ConfigCommandHandler _handler;

    public ConfigCommandHandlerTests()
    {
        _mockStorageService = new Mock<IConfigurationStorageService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockSetupWizard = new Mock<ISetupWizardUI>();
        _mockOpenAIValidator = new Mock<IApiKeyValidator>();
        _mockAnthropicValidator = new Mock<IApiKeyValidator>();
        _mockLogger = new Mock<ILogger<ConfigCommandHandler>>();

        _mockOpenAIValidator.Setup(v => v.Provider).Returns(LlmProvider.OpenAI);
        _mockAnthropicValidator.Setup(v => v.Provider).Returns(LlmProvider.Anthropic);

        // Setup default configuration values
        _mockConfiguration.Setup(c => c[ConfigurationKeys.LlmProvider]).Returns((string?)null);
        _mockConfiguration.Setup(c => c[ConfigurationKeys.LlmApiKey]).Returns((string?)null);
        _mockConfiguration.Setup(c => c[ConfigurationKeys.LlmModel]).Returns((string?)null);

        var validators = new[] { _mockOpenAIValidator.Object, _mockAnthropicValidator.Object };

        _handler = new ConfigCommandHandler(
            _mockStorageService.Object,
            _mockConfiguration.Object,
            _mockSetupWizard.Object,
            validators, 
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullStorageService_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ConfigCommandHandler(
            null!,
            _mockConfiguration.Object,
            _mockSetupWizard.Object,
            new[] { _mockOpenAIValidator.Object },
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("storageService");
    }

    [Fact]
    public void Constructor_WithNullSetupWizard_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ConfigCommandHandler(
            _mockStorageService.Object,
            _mockConfiguration.Object,
            null!,
            new[] { _mockOpenAIValidator.Object },
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("setupWizard");
    }

    [Fact]
    public void Constructor_WithNullValidators_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ConfigCommandHandler(
            _mockStorageService.Object,
            _mockConfiguration.Object,
            _mockSetupWizard.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("apiKeyValidators");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ConfigCommandHandler(
            _mockStorageService.Object,
            _mockConfiguration.Object,
            _mockSetupWizard.Object,
            new[] { _mockOpenAIValidator.Object },
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Show Action Tests

    [Fact]
    public async Task HandleShow_WithExistingConfiguration_ShouldReturnConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand { Action = ConfigAction.Show };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeSameAs(config);
        
        _mockStorageService.Verify(s => s.LoadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleShow_WithNoConfiguration_ShouldReturnFailure()
    {
        // Arrange
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Failure("No config found"));

        var command = new ConfigCommand { Action = ConfigAction.Show };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No configuration found");
        result.Error.Should().Contain("tom setup");
    }

    [Fact]
    public async Task HandleShow_WithShowSecretsFlag_ShouldLogShowSecretsStatus()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand { Action = ConfigAction.Show, ShowSecrets = true };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ShowSecrets: True")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Set Action Tests - Validation

    [Fact]
    public async Task HandleSet_WithNullSettingName_ShouldReturnFailure()
    {
        // Arrange
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = null,
            SettingValue = "test"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Setting name is required");
        result.Error.Should().Contain("Example:");
    }

    [Fact]
    public async Task HandleSet_WithEmptySettingName_ShouldReturnFailure()
    {
        // Arrange
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "   ",
            SettingValue = "test"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Setting name is required");
    }

    [Fact]
    public async Task HandleSet_WithNullSettingValue_ShouldReturnFailure()
    {
        // Arrange
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = null
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Setting value is required");
        result.Error.Should().Contain("Example:");
    }

    [Fact]
    public async Task HandleSet_WithNoConfiguration_ShouldReturnFailure()
    {
        // Arrange
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Failure("No config"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "OpenAI"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No configuration found");
        result.Error.Should().Contain("tom setup");
    }

    [Fact]
    public async Task HandleSet_WithUnknownSettingName_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "unknown-setting",
            SettingValue = "value"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unknown setting");
    }

    #endregion

    #region Set Action Tests - LLM Provider

    [Fact]
    public async Task HandleSet_UpdateLlmProvider_WithValidProvider_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "Anthropic"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Llm.Provider.Should().Be(LlmProvider.Anthropic);
        result.Value.LastModifiedAt.Should().NotBeNull();
        
        _mockStorageService.Verify(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSet_UpdateLlmProvider_WithInvalidProvider_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm-provider",
            SettingValue = "InvalidProvider"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid LLM provider");
        result.Error.Should().Contain("Valid values: OpenAI, Anthropic");
    }

    [Fact]
    public async Task HandleSet_UpdateLlmProvider_CaseInsensitive_ShouldWork()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "LLM-PROVIDER",
            SettingValue = "anthropic"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Llm.Provider.Should().Be(LlmProvider.Anthropic);
    }

    #endregion

    #region Set Action Tests - API Key

    [Fact]
    public async Task HandleSet_UpdateApiKey_WithValidKey_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));
        _mockOpenAIValidator.Setup(v => v.ValidateFormatAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApiValidationResult { IsValid = true });

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "api-key",
            SettingValue = "sk-new-key-123"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Llm.ApiKey.Should().Be("sk-new-key-123");
        
        _mockOpenAIValidator.Verify(v => v.ValidateFormatAsync("sk-new-key-123"), Times.Once);
    }

    [Fact]
    public async Task HandleSet_UpdateApiKey_WithInvalidFormat_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockOpenAIValidator.Setup(v => v.ValidateFormatAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApiValidationResult { IsValid = false, ErrorMessage = "Invalid format" });

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "api-key",
            SettingValue = "invalid-key"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid OpenAI API key format");
        result.Error.Should().Contain("https://platform.openai.com/api-keys");
    }

    #endregion

    #region Set Action Tests - Memory Directory

    [Fact]
    public async Task HandleSet_UpdateMemoryDirectory_WithValidPath_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "memory-directory",
            SettingValue = "/tmp/test-memory"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Storage.MemoryDirectory.Should().NotBeNullOrEmpty();
        Path.IsPathRooted(result.Value.Storage.MemoryDirectory).Should().BeTrue("path should be absolute");
    }

    [Fact]
    public async Task HandleSet_UpdateMemoryDirectory_WithInvalidPath_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        // Use a path with null character which is invalid on all systems
        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "memory-directory",
            SettingValue = "/path/with\0null" // Path with null character is always invalid
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid directory path", "path with null character should fail validation");
    }

    #endregion

    #region Set Action Tests - SSH Key Path

    [Fact]
    public async Task HandleSet_UpdateSshKeyPath_WithExistingFile_ShouldUpdateConfiguration()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateValidConfiguration();
            _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
            _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Success("Configuration saved"));

            var command = new ConfigCommand
            {
                Action = ConfigAction.Set,
                SettingName = "ssh-key-path",
                SettingValue = tempFile
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.Ssh.KeyPath.Should().Be(tempFile);
            result.Value.Ssh.KeySource.Should().Be(SshKeySource.ManualPath);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task HandleSet_UpdateSshKeyPath_WithNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "ssh-key-path",
            SettingValue = "/nonexistent/path/to/key"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SSH key file not found");
        result.Error.Should().Contain("Example:");
    }

    [Fact]
    public async Task HandleSet_UpdateSshKeyPath_WithTildePath_ShouldExpandPath()
    {
        // Arrange
        // Create a temp file in a subdirectory to simulate ~/.ssh/key
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var testDir = Path.Combine(homeDir, ".test-ssh-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "test_key");
        await File.WriteAllTextAsync(testFile, "test key");

        try
        {
            var config = CreateValidConfiguration();
            _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
            _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Success("Configuration saved"));

            var relativePath = testFile.Replace(homeDir, "~", StringComparison.Ordinal);
            var command = new ConfigCommand
            {
                Action = ConfigAction.Set,
                SettingName = "ssh-key-path",
                SettingValue = relativePath
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.Ssh.KeyPath.Should().Be(testFile, "tilde should be expanded to full path");
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    #endregion

    #region Set Action Tests - Log Level

    [Fact]
    public async Task HandleSet_UpdateLogLevel_WithValidLevel_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "log-level",
            SettingValue = "Debug"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Optional.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
    }

    [Fact]
    public async Task HandleSet_UpdateLogLevel_WithInvalidLevel_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "log-level",
            SettingValue = "InvalidLevel"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid log level");
        result.Error.Should().Contain("Valid values");
    }

    [Theory]
    [InlineData("debug", Microsoft.Extensions.Logging.LogLevel.Debug)]
    [InlineData("INFORMATION", Microsoft.Extensions.Logging.LogLevel.Information)]
    [InlineData("Warning", Microsoft.Extensions.Logging.LogLevel.Warning)]
    [InlineData("ERROR", Microsoft.Extensions.Logging.LogLevel.Error)]
    public async Task HandleSet_UpdateLogLevel_CaseInsensitive_ShouldWork(string input, Microsoft.Extensions.Logging.LogLevel expected)
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "log-level",
            SettingValue = input
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Optional.LogLevel.Should().Be(expected);
    }

    #endregion

    #region Set Action Tests - Retention Days

    [Fact]
    public async Task HandleSet_UpdateRetentionDays_WithValidValue_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "retention-days",
            SettingValue = "60"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Optional.RetentionDays.Should().Be(60);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-10")]
    public async Task HandleSet_UpdateRetentionDays_WithNonPositiveValue_ShouldReturnFailure(string value)
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "retention-days",
            SettingValue = value
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid retention days");
        result.Error.Should().Contain("positive integer");
    }

    [Fact]
    public async Task HandleSet_UpdateRetentionDays_WithNonNumericValue_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "retention-days",
            SettingValue = "not-a-number"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("positive integer");
    }

    #endregion

    #region Set Action Tests - Save Failures

    [Fact]
    public async Task HandleSet_WhenSaveFails_ShouldReturnFailure()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Save failed"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "retention-days",
            SettingValue = "60"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to save configuration");
    }

    #endregion

    #region Validate Action Tests

    [Fact]
    public async Task HandleValidate_WithValidConfiguration_ShouldReturnSuccess()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand { Action = ConfigAction.Validate };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(config);
    }

    [Fact]
    public async Task HandleValidate_WithNoConfiguration_ShouldReturnFailure()
    {
        // Arrange
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Failure("No config"));

        var command = new ConfigCommand { Action = ConfigAction.Validate };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No configuration found");
        result.Error.Should().Contain("tom setup");
    }

    [Fact]
    public async Task HandleValidate_WithInvalidConfiguration_ShouldReturnFailure()
    {
        // Arrange
        var invalidConfig = new ConfigurationSettings
        {
            Ssh = new SshConfiguration(),
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = null },
            Storage = new StorageConfiguration { MemoryDirectory = "/tmp/test" }
        };

        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(invalidConfig));

        var command = new ConfigCommand { Action = ConfigAction.Validate };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Configuration validation failed");
        result.Error.Should().Contain("Required fields");
    }

    #endregion

    #region Reset Action Tests

    [Fact]
    public async Task HandleReset_ShouldReturnNotImplemented()
    {
        // Arrange
        var command = new ConfigCommand { Action = ConfigAction.Reset };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Reset configuration is not yet implemented");
        result.Error.Should().Contain("tom setup");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WithCancelledToken_ShouldReturnFailureResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var command = new ConfigCommand { Action = ConfigAction.Show };

        // Act
        var result = await _handler.Handle(command, cts.Token);

        // Assert
        // Handler catches exceptions and returns failure result
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Configuration operation failed");
        result.Error.Should().Contain("canceled");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Handle_WhenExceptionThrown_ShouldReturnFailureResult()
    {
        // Arrange
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var command = new ConfigCommand { Action = ConfigAction.Show };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Configuration operation failed");
        result.Error.Should().Contain("Test exception");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task Handle_ShouldLogConfigCommandProcessing()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        var command = new ConfigCommand { Action = ConfigAction.Show };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing config command")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleSet_OnSuccess_ShouldLogSettingUpdate()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));
        _mockStorageService.Setup(s => s.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("Configuration saved"));

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "retention-days",
            SettingValue = "60"
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("updated successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Set Action Tests - LLM Interactive Configuration

    [Fact]
    public async Task HandleSet_WithLlmSettingName_ShouldTriggerInteractiveConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        // Setup wizard to return null (cancel)
        _mockSetupWizard.Setup(w => w.PromptForLlmProviderAsync(
                It.IsAny<LlmProvider?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmProvider?)null);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm",
            SettingValue = null // Interactive mode - no value provided
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LLM configuration cancelled");
        
        // Verify wizard was called
        _mockSetupWizard.Verify(w => w.PromptForLlmProviderAsync(
            It.IsAny<LlmProvider?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleSet_WithLlmSettingNameAndValue_ShouldStillUseInteractiveMode()
    {
        // Arrange
        var config = CreateValidConfiguration();
        _mockStorageService.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ConfigurationSettings>.Success(config));

        // Setup wizard to return null (cancel)
        _mockSetupWizard.Setup(w => w.PromptForLlmProviderAsync(
                It.IsAny<LlmProvider?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmProvider?)null);

        var command = new ConfigCommand
        {
            Action = ConfigAction.Set,
            SettingName = "llm",
            SettingValue = "some-value" // Value is ignored for llm setting
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // LLM configuration uses interactive mode regardless of value
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LLM configuration cancelled");
        
        // Verify wizard was called even though value was provided
        _mockSetupWizard.Verify(w => w.PromptForLlmProviderAsync(
            It.IsAny<LlmProvider?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
