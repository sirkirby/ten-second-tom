using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Unit tests for ConfigurationStorageService
/// Tests unified configuration storage to appsettings.json with atomic operations
/// </summary>
public sealed class ConfigurationStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testAppSettingsPath;
    private readonly Mock<ILogger<ConfigurationStorageService>> _mockLogger;

    public ConfigurationStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"tom-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _testAppSettingsPath = Path.Combine(_testDirectory, "appsettings.json");
        _mockLogger = new Mock<ILogger<ConfigurationStorageService>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ConfigurationStorageService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task SaveAsync_WithValidSettings_SavesSuccessfully()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
        var settings = CreateValidConfigurationSettings();

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_testAppSettingsPath);
        File.Exists(_testAppSettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

        // Act
        var act = async () => await service.SaveAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
        var settings = CreateValidConfigurationSettings();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await service.SaveAsync(settings, cts.Token);

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
    public async Task LoadAsync_AfterSave_ReturnsConfigurationSettings()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
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
    public async Task LoadAsync_WithNoConfiguration_ReturnsDefaults()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

        // Act
        var result = await service.LoadAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Storage.MemoryDirectory.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
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
    public async Task SaveAsync_WithComplexConfiguration_PreservesAllFields()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
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
                ApiKey = "sk-ant-api03-test1234567890abcdefghijklmnopqrstuvwxyz",
                Model = "claude-3-5-sonnet-20241022"
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
        loaded.Llm.Model.Should().Be(settings.Llm.Model);

        loaded.Storage.MemoryDirectory.Should().Be(settings.Storage.MemoryDirectory);
        loaded.Optional.LogLevel.Should().Be(settings.Optional.LogLevel);
        loaded.Optional.RetentionDays.Should().Be(settings.Optional.RetentionDays);
    }

    [Fact]
    public async Task SaveAsync_ShouldPreserveOtherSections()
    {
        // Arrange
        var initialJson = """
        {
          "Serilog": {
            "MinimumLevel": "Debug"
          }
        }
        """;
        await File.WriteAllTextAsync(_testAppSettingsPath, initialJson);

        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
        var settings = CreateValidConfigurationSettings();

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var content = await File.ReadAllTextAsync(_testAppSettingsPath);
        content.Should().Contain("Serilog");
        content.Should().Contain("MinimumLevel");
        content.Should().Contain("Debug");
    }

    [Fact]
    public async Task SaveAsync_WithConcurrentWrites_ShouldNotCorruptFile()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
        var settings1 = CreateValidConfigurationSettings() with
        {
            Llm = new LlmConfiguration { Provider = LlmProvider.OpenAI, ApiKey = "sk-key1" }
        };
        var settings2 = CreateValidConfigurationSettings() with
        {
            Llm = new LlmConfiguration { Provider = LlmProvider.Anthropic, ApiKey = "sk-ant-key2" }
        };

        // Act
        var task1 = service.SaveAsync(settings1, CancellationToken.None);
        var task2 = service.SaveAsync(settings2, CancellationToken.None);
        var results = await Task.WhenAll(task1, task2);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());

        // File should be valid JSON (not corrupted)
        var loadResult = await service.LoadAsync(CancellationToken.None);
        loadResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithNullOptionalFields_HandlesGracefully()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
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
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

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
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

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
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
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
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
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
    public async Task LoadAsync_WithCorruptedData_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_testAppSettingsPath, "{ this is not valid json }", CancellationToken.None);
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

        // Act
        var result = await service.LoadAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LoadFailed");
    }

    [Fact]
    public async Task SaveAsync_MultipleTimesSequentially_UpdatesConfiguration()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

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

    [Fact]
    public void GetStorageLocation_ReturnsAppSettingsPath()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

        // Act
        var location = service.GetStorageLocation();

        // Assert
        location.Should().Be(_testAppSettingsPath);
    }

    [Fact]
    public async Task SaveAsync_LogsStorageLocation()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);
        var settings = CreateValidConfigurationSettings();

        // Act
        var result = await service.SaveAsync(settings, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadAsync_WithPartialConfiguration_ReturnsDefaults()
    {
        // Arrange
        var service = new ConfigurationStorageService(_mockLogger.Object, _testAppSettingsPath);

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
