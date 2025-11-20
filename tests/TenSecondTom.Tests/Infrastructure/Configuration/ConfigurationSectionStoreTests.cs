using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Shared.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;
using Xunit;


namespace TenSecondTom.Tests.Infrastructure.Configuration;

/// <summary>
/// Tests for ConfigurationSectionStore - generic, feature-agnostic configuration infrastructure.
/// Validates thread-safe, atomic read/write of arbitrary configuration sections.
/// </summary>
public sealed class ConfigurationSectionStoreTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly ConfigurationSectionStore _sut;
    private readonly Mock<ILogger<ConfigurationSectionStore>> _loggerMock;

    public ConfigurationSectionStoreTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");
        _loggerMock = new Mock<ILogger<ConfigurationSectionStore>>();

        var configuration = new ConfigurationBuilder().Build();
        _sut = new ConfigurationSectionStore(_loggerMock.Object, configuration, _testConfigPath);
    }

    [Fact]
    public async Task ReadSectionAsync_WhenFileDoesNotExist_ReturnsDefaultInstance()
    {
        // Arrange - no config file exists

        // Act
        var result = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().BeNull();
        result.Value.Count.Should().Be(0);
    }

    [Fact]
    public async Task ReadSectionAsync_WhenSectionDoesNotExist_ReturnsDefaultInstance()
    {
        // Arrange - create file with different section
        await File.WriteAllTextAsync(_testConfigPath, """
            {
              "OtherSection": {
                "Value": "test"
              }
            }
            """);

        // Act
        var result = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().BeNull();
    }

    [Fact]
    public async Task WriteSectionAsync_WithEmptyFile_CreatesSection()
    {
        // Arrange
        var config = new TestConfig { Name = "Test", Count = 42 };

        // Act
        var result = await _sut.WriteSectionAsync("TenSecondTom:Test", config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_testConfigPath);

        // Verify file contents
        var json = await File.ReadAllTextAsync(_testConfigPath);
        using var document = JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom).Should().BeTrue();
        tenSecondTom.TryGetProperty("Test", out var testSection).Should().BeTrue();
        testSection.GetProperty("Name").GetString().Should().Be("Test");
        testSection.GetProperty("Count").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task WriteSectionAsync_PreservesOtherSections()
    {
        // Arrange - existing file with other sections
        await File.WriteAllTextAsync(_testConfigPath, """
            {
              "Serilog": {
                "MinimumLevel": "Information"
              },
              "TenSecondTom": {
                "RootDirectory": "~/ten-second-tom"
              }
            }
            """);

        var config = new TestConfig { Name = "Test", Count = 42 };

        // Act
        var result = await _sut.WriteSectionAsync("TenSecondTom:Audio", config);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify Serilog section preserved
        var json = await File.ReadAllTextAsync(_testConfigPath);
        using var document = System.Text.Json.JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("Serilog", out var serilog).Should().BeTrue();
        serilog.GetProperty("MinimumLevel").GetString().Should().Be("Information");

        // Verify TenSecondTom:RootDirectory preserved
        document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom).Should().BeTrue();
        tenSecondTom.GetProperty("RootDirectory").GetString().Should().Be("~/ten-second-tom");

        // Verify new section added
        tenSecondTom.TryGetProperty("Audio", out var audio).Should().BeTrue();
        audio.GetProperty("Name").GetString().Should().Be("Test");
    }

    [Fact]
    public async Task ReadSectionAsync_AfterWrite_ReturnsWrittenData()
    {
        // Arrange
        var originalConfig = new TestConfig { Name = "Original", Count = 99 };
        await _sut.WriteSectionAsync("TenSecondTom:Test", originalConfig);

        // Act
        var result = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Original");
        result.Value.Count.Should().Be(99);
    }

    [Fact]
    public async Task WriteMultipleSectionsAsync_WritesAllSectionsAtomically()
    {
        // Arrange
        var sections = new Dictionary<string, object>
        {
            ["TenSecondTom:Audio"] = new TestConfig { Name = "Audio", Count = 1 },
            ["TenSecondTom:Ssh"] = new TestConfig { Name = "Ssh", Count = 2 },
            ["TenSecondTom:Llm"] = new TestConfig { Name = "Llm", Count = 3 }
        };

        // Act
        var result = await _sut.WriteMultipleSectionsAsync(sections);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all sections written
        var audioResult = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Audio");
        audioResult.Value.Name.Should().Be("Audio");
        audioResult.Value.Count.Should().Be(1);

        var sshResult = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Ssh");
        sshResult.Value.Name.Should().Be("Ssh");
        sshResult.Value.Count.Should().Be(2);

        var llmResult = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Llm");
        llmResult.Value.Name.Should().Be("Llm");
        llmResult.Value.Count.Should().Be(3);
    }

    [Fact]
    public async Task ReadFullConfigAsync_ReturnsCompleteConfiguration()
    {
        // Arrange
        await File.WriteAllTextAsync(_testConfigPath, """
            {
              "Serilog": {
                "MinimumLevel": "Debug"
              },
              "TenSecondTom": {
                "RootDirectory": "~/test"
              }
            }
            """);

        // Act
        var result = await _sut.ReadFullConfigAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        using var document = result.Value;
        document.RootElement.TryGetProperty("Serilog", out var serilog).Should().BeTrue();
        document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom).Should().BeTrue();
        tenSecondTom.GetProperty("RootDirectory").GetString().Should().Be("~/test");
    }

    [Fact]
    public void GetConfigPath_ReturnsCorrectPath()
    {
        // Act
        var path = _sut.GetConfigPath();

        // Assert
        path.Should().Be(_testConfigPath);
    }

    [Fact]
    public async Task WriteSectionAsync_WithNestedPath_CreatesNestedStructure()
    {
        // Arrange
        var config = new TestConfig { Name = "Nested", Count = 5 };

        // Act
        var result = await _sut.WriteSectionAsync("TenSecondTom:Audio:Recorder", config);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify nested structure
        var json = await File.ReadAllTextAsync(_testConfigPath);
        using var document = System.Text.Json.JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom).Should().BeTrue();
        tenSecondTom.TryGetProperty("Audio", out var audio).Should().BeTrue();
        audio.TryGetProperty("Recorder", out var recorder).Should().BeTrue();
        recorder.GetProperty("Name").GetString().Should().Be("Nested");
        recorder.GetProperty("Count").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task ReadSectionAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange - write invalid JSON
        await File.WriteAllTextAsync(_testConfigPath, "{ invalid json }");

        // Act
        var result = await _sut.ReadSectionAsync<TestConfig>("TenSecondTom:Test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid JSON");
    }

    public void Dispose()
    {
        _sut.Dispose();

        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    /// <summary>
    /// Test configuration model - intentionally generic to verify infrastructure
    /// has no knowledge of domain models.
    /// </summary>
    private sealed class TestConfig
    {
        public string? Name { get; init; }
        public int Count { get; init; }
    }
}

