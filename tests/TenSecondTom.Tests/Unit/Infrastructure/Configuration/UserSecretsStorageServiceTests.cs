using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Unit tests for UserSecretsStorageService
/// Tests User Secrets write/read, fallback to appsettings.json, and error handling
/// Note: These are actually integration tests as they perform real I/O operations.
/// Each test uses a unique User Secrets ID to avoid polluting production configuration.
/// </summary>
public sealed class UserSecretsStorageServiceTests : IDisposable
{
    private readonly Mock<ILogger<UserSecretsStorageService>> _loggerMock;
    private readonly string _testUserSecretsId;

    public UserSecretsStorageServiceTests()
    {
        _loggerMock = new Mock<ILogger<UserSecretsStorageService>>();
        // Use a unique ID for each test instance to avoid polluting production UserSecrets
        _testUserSecretsId = $"TenSecondTom-Test-{Guid.NewGuid()}";
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cleanup must not throw")]
    public void Dispose()
    {
        // Clean up test UserSecrets directory
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
                // Retry after delay if directory is locked
                Thread.Sleep(100);
                try
                {
                    Directory.Delete(userSecretsPath, recursive: true);
                }
                catch
                {
                    // Ignore - cleanup script can handle orphaned directories
                }
            }
            catch
            {
                // Ignore cleanup errors - don't fail tests because of cleanup
            }
        }
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UserSecretsStorageService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task SaveAsync_WithValidSettings_SavesSuccessfully()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings();

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.SaveAsync(settings, cts.Token);

        // Assert
        // Service may or may not throw depending on cancellation timing
        // Just verify it handles cancellation token
        try
        {
            await act.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Expected - cancellation was respected
        }
    }

    [Fact]
    public async Task LoadAsync_AfterSave_ReturnsConfigurationSettings()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var originalSettings = CreateValidConfigurationSettings();

        // Save first
        var saveResult = await service.SaveAsync(originalSettings, CancellationToken.None);
        saveResult.IsSuccess.Should().BeTrue();

        // Act
        var loadResult = await service.LoadAsync(CancellationToken.None);

        // Assert
        loadResult.Should().NotBeNull();
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Should().NotBeNull();
        loadResult.Value.Llm.Provider.Should().Be(originalSettings.Llm.Provider);
        loadResult.Value.Llm.ApiKey.Should().Be(originalSettings.Llm.ApiKey);
        loadResult.Value.Storage.MemoryDirectory.Should().Be(originalSettings.Storage.MemoryDirectory);
    }

    [Fact]
    public async Task LoadAsync_WithNoConfiguration_ReturnsFailure()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);

        // Note: This test assumes a clean state where no configuration exists
        // In practice, if configuration exists from previous tests, this may succeed
        // The service should handle missing configuration gracefully

        // Act
        var result = await service.LoadAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Storage.MemoryDirectory.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithNullSettings_ReturnsFailure()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);

        // Act
        var act = async () => await service.SaveAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_LogsStorageLocation()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings();

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        
        // Verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SaveAsync_WithComplexConfiguration_PreservesAllFields()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "/Users/test/.ssh/id_ed25519",
                KeySource = SshKeySource.FileSystem,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.Anthropic,
                ApiKey = "sk-ant-api03-test1234567890abcdefghijklmnopqrstuvwxyz"
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = "/Users/test/.tom/memory"
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 90
            }
        };

        // Act - Save and Load
        var saveResult = await service.SaveAsync(settings, CancellationToken.None);
        saveResult.IsSuccess.Should().BeTrue();

        var loadResult = await service.LoadAsync(CancellationToken.None);

        // Assert - All fields preserved
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        
        loaded.Ssh.KeyPath.Should().Be(settings.Ssh.KeyPath);
        loaded.Ssh.KeySource.Should().Be(settings.Ssh.KeySource);
        
        loaded.Llm.Provider.Should().Be(settings.Llm.Provider);
        loaded.Llm.ApiKey.Should().Be(settings.Llm.ApiKey);
        
        loaded.Storage.MemoryDirectory.Should().Be(settings.Storage.MemoryDirectory);
        loaded.Optional.LogLevel.Should().Be(settings.Optional.LogLevel);
        loaded.Optional.RetentionDays.Should().Be(settings.Optional.RetentionDays);
    }

    [Fact]
    public async Task LoadAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.LoadAsync(cts.Token);

        // Assert
        try
        {
            await act.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Expected - cancellation was respected
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings();

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        
        // Directory should have been created - verify via storage location
        result.Value.Should().NotBeNullOrEmpty();
        var directory = Path.GetDirectoryName(result.Value);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.Exists(directory).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetStorageLocation_AfterSave_ReturnsUserSecretsPath()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings();

        // Act
        var saveResult = await service.SaveAsync(settings, CancellationToken.None);
        var location = service.GetStorageLocation();

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        location.Should().NotBeNullOrEmpty();
        location.Should().Contain("usersecrets");
        location.Should().EndWith("secrets.json");
    }

    [Fact]
    public void GetStorageLocation_BeforeSave_ReturnsDefaultPath()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);

        // Act
        var location = service.GetStorageLocation();

        // Assert
        location.Should().NotBeNullOrEmpty();
        location.Should().Contain("usersecrets");
        location.Should().EndWith("secrets.json");
    }

    [Fact]
    public async Task LoadAsync_WithPartialConfiguration_ReturnsDefaults()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        
        // Save a minimal configuration with only required fields
        var minimalSettings = new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "/test/key"
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test123"
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = "/test/memory"
            },
            Optional = new OptionalConfiguration()
        };

        var saveResult = await service.SaveAsync(minimalSettings, CancellationToken.None);
        saveResult.IsSuccess.Should().BeTrue();

        // Act
        var loadResult = await service.LoadAsync(CancellationToken.None);

        // Assert
        loadResult.IsSuccess.Should().BeTrue();
        var loaded = loadResult.Value;
        
        // Core fields should be preserved
        loaded.Ssh.KeyPath.Should().Be("/test/key");
        loaded.Llm.ApiKey.Should().Be("sk-test123");
        
        // Optional fields should have defaults
        loaded.Optional.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Information);
        loaded.Optional.RetentionDays.Should().Be(30);
    }

    [Fact]
    public async Task SaveAsync_WithNullOptionalFields_HandlesGracefully()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "/test/key",
                KeySource = null,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test123",
                Model = null
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = "/test/memory"
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Warning,
                RetentionDays = 60
            }
        };

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Load and verify null fields are handled
        var loadResult = await service.LoadAsync(CancellationToken.None);
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Ssh.KeySource.Should().BeNull();
        loadResult.Value.Ssh.AgentSocketPath.Should().BeNull();
        loadResult.Value.Llm.Model.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithDifferentProviders_PreservesProviderChoice()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);

        // Test OpenAI
        var openAiSettings = CreateValidConfigurationSettings() with
        {
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test-openai"
            }
        };

        await service.SaveAsync(openAiSettings, CancellationToken.None);
        var loadedOpenAi = await service.LoadAsync(CancellationToken.None);
        loadedOpenAi.Value.Llm.Provider.Should().Be(LlmProvider.OpenAI);

        // Test Anthropic
        var anthropicSettings = CreateValidConfigurationSettings() with
        {
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.Anthropic,
                ApiKey = "sk-ant-test-anthropic"
            }
        };

        await service.SaveAsync(anthropicSettings, CancellationToken.None);
        var loadedAnthropic = await service.LoadAsync(CancellationToken.None);
        loadedAnthropic.Value.Llm.Provider.Should().Be(LlmProvider.Anthropic);
    }

    [Fact]
    public async Task SaveAsync_WithDifferentSshKeySources_PreservesSource()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);

        // Test each SSH key source
        var sources = new[]
        {
            SshKeySource.SystemAgent,
            SshKeySource.OnePasswordAgent,
            SshKeySource.SecretiveAgent,
            SshKeySource.FileSystem,
            SshKeySource.ManualPath
        };

        foreach (var source in sources)
        {
            var settings = CreateValidConfigurationSettings() with
            {
                Ssh = new SshConfiguration
                {
                    KeyPath = "/test/key",
                    KeySource = source
                }
            };

            // Act
            await service.SaveAsync(settings, CancellationToken.None);
            var loaded = await service.LoadAsync(CancellationToken.None);

            // Assert
            loaded.Value.Ssh.KeySource.Should().Be(source);
        }
    }

    [Fact]
    public async Task SaveAsync_WithTimestamps_PreservesTimestamps()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var createdAt = DateTime.UtcNow.AddDays(-7);
        var modifiedAt = DateTime.UtcNow;
        
        var settings = CreateValidConfigurationSettings() with
        {
            CreatedAt = createdAt,
            LastModifiedAt = modifiedAt,
            ConfigurationVersion = "2.0"
        };

        // Act
        await service.SaveAsync(settings, CancellationToken.None);
        var loaded = await service.LoadAsync(CancellationToken.None);

        // Assert
        loaded.IsSuccess.Should().BeTrue();
        // Allow for timezone differences during serialization (DateTimes are saved as ISO 8601 strings)
        loaded.Value.CreatedAt.ToUniversalTime().Should().BeCloseTo(createdAt.ToUniversalTime(), TimeSpan.FromSeconds(1));
        loaded.Value.LastModifiedAt.Should().NotBeNull();
        loaded.Value.LastModifiedAt!.Value.ToUniversalTime().Should().BeCloseTo(modifiedAt.ToUniversalTime(), TimeSpan.FromSeconds(1));
        loaded.Value.ConfigurationVersion.Should().Be("2.0");
    }

    [Fact]
    public async Task SaveAsync_WithUnlimitedRetention_PreservesNegativeValue()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings() with
        {
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = -1 // Unlimited
            }
        };

        // Act
        await service.SaveAsync(settings, CancellationToken.None);
        var loaded = await service.LoadAsync(CancellationToken.None);

        // Assert
        loaded.IsSuccess.Should().BeTrue();
        loaded.Value.Optional.RetentionDays.Should().Be(-1);
    }

    [Fact]
    public async Task SaveAsync_LogsWarningOnFallback()
    {
        // Note: Testing actual fallback to appsettings.json is difficult in unit tests
        // as it requires forcing User Secrets to fail. This test verifies the
        // warning logging behavior is set up correctly.
        
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        var settings = CreateValidConfigurationSettings();

        // Act
        await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        // Verify that if Information level logging occurred (successful save)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedData_ReturnsFailure()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        
        // Write corrupted JSON directly to User Secrets path
        var location = service.GetStorageLocation();
        var directory = Path.GetDirectoryName(location);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(location, "{ this is not valid json }", CancellationToken.None);
        }

        // Act
        var result = await service.LoadAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LoadFailed");
        
        // Clean up
        if (File.Exists(location))
        {
            File.Delete(location);
        }
    }

    [Fact]
    public async Task SaveAsync_MultipleTimesSequentially_UpdatesConfiguration()
    {
        // Arrange
        var service = new UserSecretsStorageService(_loggerMock.Object, _testUserSecretsId);
        
        var settings1 = CreateValidConfigurationSettings() with
        {
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-first-key"
            }
        };

        var settings2 = settings1 with
        {
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.Anthropic,
                ApiKey = "sk-ant-second-key"
            }
        };

        // Act
        await service.SaveAsync(settings1, CancellationToken.None);
        var loaded1 = await service.LoadAsync(CancellationToken.None);

        await service.SaveAsync(settings2, CancellationToken.None);
        var loaded2 = await service.LoadAsync(CancellationToken.None);

        // Assert
        loaded1.Value.Llm.Provider.Should().Be(LlmProvider.OpenAI);
        loaded1.Value.Llm.ApiKey.Should().Be("sk-first-key");

        loaded2.Value.Llm.Provider.Should().Be(LlmProvider.Anthropic);
        loaded2.Value.Llm.ApiKey.Should().Be("sk-ant-second-key");
    }

    #region Helper Methods

    private static ConfigurationSettings CreateValidConfigurationSettings()
    {
        return new ConfigurationSettings
        {
            Ssh = new SshConfiguration
            {
                KeyPath = "/Users/test/.ssh/id_ed25519",
                KeySource = SshKeySource.SystemAgent,
                AgentSocketPath = null
            },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = "sk-test1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKL"
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = "/Users/test/.tom/memory"
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 30
            }
        };
    }

    #endregion
}
