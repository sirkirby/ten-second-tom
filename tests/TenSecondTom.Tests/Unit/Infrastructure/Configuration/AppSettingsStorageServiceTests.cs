using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Configuration;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Unit tests for AppSettingsStorageService
/// Tests appsettings.json updates and atomic operations
/// </summary>
public sealed class AppSettingsStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testAppSettingsPath;
    private readonly Mock<ILogger<AppSettingsStorageService>> _mockLogger;

    public AppSettingsStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"tom-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _testAppSettingsPath = Path.Combine(_testDirectory, "appsettings.json");
        _mockLogger = new Mock<ILogger<AppSettingsStorageService>>();
    }

    [Fact]
    public async Task SaveAudioConfigurationAsync_WithNewFile_ShouldCreateFile()
    {
        // Arrange
        var service = new AppSettingsStorageService(_mockLogger.Object, _testAppSettingsPath);
        var audioConfig = new AudioConfiguration
        {
            PreferredStt = "local",
            KeepFiles = false,
            Recorder = new RecorderConfiguration { InputVolume = 0.8 }
        };

        // Act
        var result = await service.SaveAudioConfigurationAsync(audioConfig, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(_testAppSettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAudioConfigurationAsync_ShouldPreserveOtherSections()
    {
        // Arrange
        var initialJson = """
        {
          "TenSecondTom": {
            "MemoryDirectory": "/test/path"
          }
        }
        """;
        await File.WriteAllTextAsync(_testAppSettingsPath, initialJson);

        var service = new AppSettingsStorageService(_mockLogger.Object, _testAppSettingsPath);
        var audioConfig = new AudioConfiguration
        {
            PreferredStt = "auto",
            Recorder = new RecorderConfiguration { InputVolume = 1.0 }
        };

        // Act
        var result = await service.SaveAudioConfigurationAsync(audioConfig, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var content = await File.ReadAllTextAsync(_testAppSettingsPath);
        content.Should().Contain("MemoryDirectory");
        content.Should().Contain("/test/path");
    }

    [Fact]
    public async Task LoadAudioConfigurationAsync_WithExistingConfig_ShouldReturnConfiguration()
    {
        // Arrange
        var json = """
        {
          "TenSecondTom": {
            "Audio": {
              "PreferredStt": "openai",
              "KeepFiles": true,
              "Recorder": {
                "InputVolume": 1.2,
                "EnableNoiseReduction": false
              }
            }
          }
        }
        """;
        await File.WriteAllTextAsync(_testAppSettingsPath, json);

        var service = new AppSettingsStorageService(_mockLogger.Object, _testAppSettingsPath);

        // Act
        var result = await service.LoadAudioConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PreferredStt.Should().Be("openai");
        result.Value.KeepFiles.Should().BeTrue();
        result.Value.Recorder.InputVolume.Should().Be(1.2);
    }

    [Fact]
    public async Task LoadAudioConfigurationAsync_WithMissingFile_ShouldReturnDefaults()
    {
        // Arrange
        var service = new AppSettingsStorageService(_mockLogger.Object, _testAppSettingsPath);

        // Act
        var result = await service.LoadAudioConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PreferredStt.Should().Be("auto"); // Default value
    }

    [Fact]
    public async Task SaveAudioConfigurationAsync_WithConcurrentWrites_ShouldNotCorruptFile()
    {
        // Arrange
        var service = new AppSettingsStorageService(_mockLogger.Object, _testAppSettingsPath);
        var config1 = new AudioConfiguration { PreferredStt = "local" };
        var config2 = new AudioConfiguration { PreferredStt = "openai" };

        // Act
        var task1 = service.SaveAudioConfigurationAsync(config1, CancellationToken.None);
        var task2 = service.SaveAudioConfigurationAsync(config2, CancellationToken.None);
        var results = await Task.WhenAll(task1, task2);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());

        // File should be valid JSON (not corrupted)
        var loadResult = await service.LoadAudioConfigurationAsync(CancellationToken.None);
        loadResult.IsSuccess.Should().BeTrue();
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
}
