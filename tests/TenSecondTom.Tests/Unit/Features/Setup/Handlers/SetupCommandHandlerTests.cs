using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;
using Xunit;

namespace SetupCommandHandlers;

/// <summary>
/// Unit tests for <see cref="SetupCommandHandler"/>
/// Tests wizard flow orchestration, configuration persistence, validation, and error handling
/// </summary>
public sealed class SetupCommandHandlerTests
{
    private readonly Mock<IConfigurationStorageService> _mockStorageService;
    private readonly Mock<ISetupWizardUI> _mockWizardUI;
    private readonly Mock<ISshKeyDetectorFactory> _mockSshKeyDetectorFactory;
    private readonly Mock<ILogger<SetupCommandHandler>> _mockLogger;

    public SetupCommandHandlerTests()
    {
        _mockStorageService = new Mock<IConfigurationStorageService>();
        _mockWizardUI = new Mock<ISetupWizardUI>();
        _mockSshKeyDetectorFactory = new Mock<ISshKeyDetectorFactory>();
        _mockLogger = new Mock<ILogger<SetupCommandHandler>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullStorageService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SetupCommandHandler(
            null!,
            _mockWizardUI.Object,
            _mockSshKeyDetectorFactory.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("storageService");
    }

    [Fact]
    public void Constructor_WithNullWizardUI_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SetupCommandHandler(
            _mockStorageService.Object,
            null!,
            _mockSshKeyDetectorFactory.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("wizardUI");
    }

    [Fact]
    public void Constructor_WithNullSshKeyDetectorFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SetupCommandHandler(
            _mockStorageService.Object,
            _mockWizardUI.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sshKeyDetectorFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SetupCommandHandler(
            _mockStorageService.Object,
            _mockWizardUI.Object,
            _mockSshKeyDetectorFactory.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidInputs_CompletesSuccessfully()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand
        {
            Force = false,
            NonInteractive = false,
            ExistingConfiguration = null
        };

        SetupHappyPathMocks();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Llm.Provider.Should().Be(LlmProvider.OpenAI);
        result.Value.Llm.ApiKey.Should().Be("sk-test123456789012345678901234567890123456789012");
        result.Value.RootDirectory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_FirstTimeSetup_ShowsWelcomeMessage()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand
        {
            Force = false,
            NonInteractive = false,
            ExistingConfiguration = null
        };

        SetupHappyPathMocks();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockWizardUI.Verify(
            x => x.ShowStatus(It.Is<string>(s => s.Contains("Welcome"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WithExistingConfiguration_ShowsReconfigurationMessage()
    {
        // Arrange
        var handler = CreateHandler();
        var existingConfig = CreateValidConfiguration();
        var command = new SetupCommand
        {
            Force = true,
            NonInteractive = false,
            ExistingConfiguration = existingConfig
        };

        SetupHappyPathMocks();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockWizardUI.Verify(
            x => x.ShowStatus(It.Is<string>(s => s.Contains("Reconfiguring"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CompletesAllEightSteps_InCorrectOrder()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };
        var stepSequence = new List<int>();

        _mockWizardUI
            .Setup(x => x.ShowStepHeader(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, int, string>((step, total, title) => stepSequence.Add(step));

        SetupHappyPathMocks();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        stepSequence.Should().ContainInOrder(1, 2, 3, 4, 5, 6, 7, 8);
        stepSequence.Should().HaveCount(8);
    }

    [Fact]
    public async Task Handle_CallsSaveAsync_WithCorrectConfiguration()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };
        ConfigurationSettings? savedConfig = null;

        SetupHappyPathMocksExceptStorage();

        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path/to/config"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.Llm.Provider.Should().Be(LlmProvider.OpenAI);
        savedConfig.Llm.ApiKey.Should().NotBeNullOrWhiteSpace();
        savedConfig.RootDirectory.Should().NotBeNullOrWhiteSpace();
        savedConfig.Optional.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Information);
        savedConfig.Optional.RetentionDays.Should().Be(30);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WhenUserCancelsAtSshSelection_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupSshDetectionMock();
        _mockWizardUI
            .Setup(x => x.PromptForSshKeyAsync(It.IsAny<List<SshKeyInfo>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SshKeyInfo?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
        result.Error.Should().Contain("SSH key");
    }

    [Fact]
    public async Task Handle_WhenUserCancelsAtProviderSelection_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        _mockWizardUI
            .Setup(x => x.PromptForLlmProviderAsync(It.IsAny<LlmProvider?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmProvider?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
        result.Error.Should().Contain("LLM provider");
    }

    [Fact]
    public async Task Handle_WhenUserCancelsAtApiKey_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        SetupProviderSelectionMock();
        _mockWizardUI
            .Setup(x => x.PromptForApiKeyAsync(It.IsAny<LlmProvider>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
        result.Error.Should().Contain("API key");
    }

    [Fact]
    public async Task Handle_WhenUserCancelsAtSummary_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        SetupProviderSelectionMock();
        SetupApiKeyMock();
        SetupMemoryDirectoryMock();
        SetupLogLevelMock();
        SetupRetentionDaysMock();
        
        _mockWizardUI
            .Setup(x => x.ShowSummaryAndConfirmAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Setup cancelled");
        result.Error.Should().Contain("User chose not to save configuration");
    }

    [Fact]
    public async Task Handle_WithCancellationToken_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        SetupSshDetectionMock();

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Handle_WhenSshDetectionFails_PropagatesError()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        _mockSshKeyDetectorFactory
            .Setup(x => x.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SSH detection failed"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Setup failed");
    }

    [Fact]
    public async Task Handle_WhenStorageFails_ReturnsFailureWithClearMessage()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupHappyPathMocksExceptStorage();
        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Config.SaveFailed: Permission denied"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to save configuration");
        result.Error.Should().Contain("Permission denied");
    }

    [Fact]
    public async Task Handle_WhenStorageFails_DisplaysErrorToUser()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupHappyPathMocksExceptStorage();
        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Config.SaveFailed: Disk full"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockWizardUI.Verify(
            x => x.ShowError(It.Is<string>(s => s.Contains("Failed to save configuration"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnSuccess_DisplaysSuccessMessage()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupHappyPathMocks();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockWizardUI.Verify(
            x => x.ShowSuccess(It.Is<string>(s => s.Contains("Setup complete"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnSuccess_DisplaysStorageLocation()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };
        var storagePath = "/Users/test/.microsoft/usersecrets/secrets.json";

        SetupHappyPathMocksExceptStorage();
        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(storagePath));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockWizardUI.Verify(
            x => x.ShowStatus(It.Is<string>(s => s.Contains(storagePath))),
            Times.Once);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task Handle_WithReconfiguration_PreservesCreatedAtTimestamp()
    {
        // Arrange
        var handler = CreateHandler();
        var originalCreatedAt = DateTime.UtcNow.AddDays(-30);
        var existingConfig = new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "/Users/test/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test123456789012345678901234567890123456789012",
                Model = null
            },
            RootDirectory = "/Users/test/.memory/ten-second-tom",
            Storage = new StorageConfiguration
            {
                CreateIfMissing = true
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 30,
                EnableTelemetry = false
            },
            CreatedAt = originalCreatedAt,  // Set in object initializer
            LastModifiedAt = null,
            ConfigurationVersion = "1.0"
        };

        var command = new SetupCommand
        {
            Force = true,
            NonInteractive = false,
            ExistingConfiguration = existingConfig
        };

        ConfigurationSettings? savedConfig = null;

        SetupHappyPathMocksExceptStorage();

        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.CreatedAt.Should().Be(originalCreatedAt);
        savedConfig.LastModifiedAt.Should().NotBeNull();
        savedConfig.LastModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_FirstTimeSetup_SetsCreatedAtToNow()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand
        {
            Force = false,
            NonInteractive = false,
            ExistingConfiguration = null
        };

        ConfigurationSettings? savedConfig = null;

        SetupHappyPathMocksExceptStorage();

        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        savedConfig.LastModifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithDefaultMemoryDirectory_UsesHomeDirectoryPath()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupHappyPathMocksExceptMemoryDirectory();
        _mockWizardUI
            .Setup(x => x.PromptForMemoryDirectoryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty); // User accepts default

        ConfigurationSettings? savedConfig = null;
        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.RootDirectory.Should().Contain(DirectoryNames.ApplicationRoot);
        savedConfig.RootDirectory.Should().NotContain(".memory", "root directory should not have .memory subdirectory");
    }

    [Fact]
    public async Task Handle_SetsConfigurationVersion_To1Point0()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        ConfigurationSettings? savedConfig = null;

        SetupHappyPathMocksExceptStorage();

        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.ConfigurationVersion.Should().Be("1.0");
    }

    [Fact]
    public async Task Handle_SetsCreateIfMissing_ToTrue()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        ConfigurationSettings? savedConfig = null;

        SetupHappyPathMocksExceptStorage();

        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.Storage.CreateIfMissing.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SetsEnableTelemetry_ToFalse()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        ConfigurationSettings? savedConfig = null;

        SetupHappyPathMocksExceptStorage();

        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
            .ReturnsAsync(Result<string>.Success("/path"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedConfig.Should().NotBeNull();
        savedConfig!.Optional.EnableTelemetry.Should().BeFalse();
    }

    // NOTE: This test is commented out because Setup.Handler no longer uses IConfiguration directly.
    // Environment variable configuration is now handled at the Program.cs level and tested in integration tests.
    // See SetupWithTemplatesIntegrationTests for end-to-end environment variable testing.

    // [Fact]
    // public async Task Handle_WithEnvironmentMemoryDirectory_UsesEnvVarAsDefault()
    // {
    //     // Arrange
    //     var handler = CreateHandler();
    //     var command = new SetupCommand { Force = false, NonInteractive = false };
    //     var envMemoryDir = "/custom/env/memory";
    //     // Note: Environment configuration setup removed - handler doesn't use IConfiguration directly
    //
    //     SetupHappyPathMocksExceptMemoryDirectory();
    //
    //     // Mock will receive env var as default
    //     string? capturedDefault = null;
    //     _mockWizardUI
    //         .Setup(x => x.PromptForMemoryDirectoryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
    //         .Callback<string?, CancellationToken>((defaultDir, ct) => capturedDefault = defaultDir)
    //         .ReturnsAsync(envMemoryDir);
    //
    //     ConfigurationSettings? savedConfig = null;
    //     _mockStorageService
    //         .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
    //         .Callback<ConfigurationSettings, CancellationToken>((config, ct) => savedConfig = config)
    //         .ReturnsAsync(Result<string>.Success("/path"));
    //
    //     // Act
    //     await handler.Handle(command, CancellationToken.None);
    //
    //     // Assert
    //     capturedDefault.Should().Be(envMemoryDir, "environment variable should be passed as default");
    //     savedConfig.Should().NotBeNull();
    //     savedConfig!.RootDirectory.Should().Be(envMemoryDir);
    //
    //     // Verify debug logging
    //     _mockLogger.Verify(
    //         x => x.Log(
    //             LogLevel.Debug,
    //             It.IsAny<EventId>(),
    //             It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("environment") && v.ToString()!.Contains(envMemoryDir)),
    //             It.IsAny<Exception>(),
    //             It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    //         Times.Once);
    // }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task Handle_LogsSetupStart_WithForceAndNonInteractiveFlags()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand
        {
            Force = true,
            NonInteractive = false
        };

        SetupHappyPathMocks();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting setup wizard")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnSuccess_LogsCompletion()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        SetupHappyPathMocks();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnCancellation_LogsWarning()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockSshKeyDetectorFactory
            .Setup(x => x.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await handler.Handle(command, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnError_LogsError()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new SetupCommand { Force = false, NonInteractive = false };

        _mockSshKeyDetectorFactory
            .Setup(x => x.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Setup wizard failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private SetupCommandHandler CreateHandler()
    {
        return new SetupCommandHandler(
            _mockStorageService.Object,
            _mockWizardUI.Object,
            _mockSshKeyDetectorFactory.Object,
            _mockLogger.Object);
    }

    private void SetupHappyPathMocks()
    {
        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        SetupProviderSelectionMock();
        SetupApiKeyMock();
        SetupMemoryDirectoryMock();
        SetupLogLevelMock();
        SetupRetentionDaysMock();
        SetupConfirmationMock();
        SetupStorageMock();
    }

    private void SetupHappyPathMocksExceptStorage()
    {
        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        SetupProviderSelectionMock();
        SetupApiKeyMock();
        SetupMemoryDirectoryMock();
        SetupLogLevelMock();
        SetupRetentionDaysMock();
        SetupConfirmationMock();
    }

    private void SetupHappyPathMocksExceptMemoryDirectory()
    {
        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        SetupProviderSelectionMock();
        SetupApiKeyMock();
        SetupLogLevelMock();
        SetupRetentionDaysMock();
        SetupConfirmationMock();
        SetupStorageMock();
    }

    private void SetupHappyPathMocksExceptTemplateHandler()
    {
        SetupSshDetectionMock();
        SetupSshKeySelectionMock();
        SetupProviderSelectionMock();
        SetupApiKeyMock();
        SetupMemoryDirectoryMock();
        SetupLogLevelMock();
        SetupRetentionDaysMock();
        SetupConfirmationMock();
    }

    private void SetupSshDetectionMock()
    {
        var sshKeys = new List<SshKeyInfo>
        {
            new()
            {
                FilePath = "/Users/test/.ssh/id_ed25519",
                DisplayName = "Test Key",
                Source = SshKeySource.FileSystem,
                PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHello",
                IsEd25519 = true
            }
        };

        var detectionResult = new SshDetectionResult
        {
            DetectedKeys = sshKeys,
            DetectionDuration = TimeSpan.FromSeconds(1),
            SourcesChecked = new[] { SshKeySource.FileSystem }
        };

        _mockSshKeyDetectorFactory
            .Setup(x => x.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detectionResult);
    }

    private void SetupSshKeySelectionMock()
    {
        var selectedKey = new SshKeyInfo
        {
            FilePath = "/Users/test/.ssh/id_ed25519",
            DisplayName = "Test Key",
            Source = SshKeySource.FileSystem,
            PublicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHello",
            IsEd25519 = true
        };

        _mockWizardUI
            .Setup(x => x.PromptForSshKeyAsync(It.IsAny<List<SshKeyInfo>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedKey);
    }

    private void SetupProviderSelectionMock()
    {
        _mockWizardUI
            .Setup(x => x.PromptForLlmProviderAsync(It.IsAny<LlmProvider?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmProvider.OpenAI);
    }

    private void SetupApiKeyMock()
    {
        _mockWizardUI
            .Setup(x => x.PromptForApiKeyAsync(It.IsAny<LlmProvider>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sk-test123456789012345678901234567890123456789012");
    }

    private void SetupMemoryDirectoryMock()
    {
        _mockWizardUI
            .Setup(x => x.PromptForMemoryDirectoryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/Users/test/.memory/ten-second-tom");
    }

    private void SetupLogLevelMock()
    {
        _mockWizardUI
            .Setup(x => x.PromptForLogLevelAsync(It.IsAny<Microsoft.Extensions.Logging.LogLevel?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Microsoft.Extensions.Logging.LogLevel.Information);
    }

    private void SetupRetentionDaysMock()
    {
        _mockWizardUI
            .Setup(x => x.PromptForRetentionDaysAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);
    }

    private void SetupConfirmationMock()
    {
        _mockWizardUI
            .Setup(x => x.ShowSummaryAndConfirmAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupStorageMock()
    {
        _mockStorageService
            .Setup(x => x.SaveAsync(It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("/Users/test/.microsoft/usersecrets/secrets.json"));
    }

    private static ConfigurationSettings CreateValidConfiguration()
    {
        return new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "/Users/test/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test123456789012345678901234567890123456789012",
                Model = null
            },
            RootDirectory = "/Users/test/.memory/ten-second-tom",
            Storage = new StorageConfiguration
            {
                CreateIfMissing = true
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 30,
                EnableTelemetry = false
            },
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastModifiedAt = null,
            ConfigurationVersion = "1.0"
        };
    }

    #endregion
}
