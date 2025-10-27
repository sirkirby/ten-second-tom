using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Tests.Features.Generate.Services;

/// <summary>
/// Tests for <see cref="OutputStorageService"/> implementation.
/// Validates file path building, existence checking, and output saving.
/// </summary>
public sealed class OutputStorageServiceTests
{
    private readonly Mock<ILogger<OutputStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly string _testMemoryDirectory;

    public OutputStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<OutputStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _testMemoryDirectory = "/test/memory";

        _mockConfiguration
            .Setup(c => c[ConfigurationKeys.MemoryDirectoryKey])
            .Returns(_testMemoryDirectory);
    }

    #region BuildOutputFilePath Tests

    [Fact]
    public void BuildOutputFilePath_WithValidInputs_ReturnsCorrectPath()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);
        var recordingBaseName = "10-21-2025_1";
        var templateId = "daily-summary";

        // Act
        var result = service.BuildOutputFilePath(recordingBaseName, templateId);

        // Assert
        var expectedFileName = "10-21-2025_1_daily-summary.md";
        var expectedPath = $"{_testMemoryDirectory}/{DirectoryNames.Recording}/{expectedFileName}";
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void BuildOutputFilePath_WithDifferentTemplate_ChangesFilename()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);
        var recordingBaseName = "10-21-2025_2";
        var templateId = "business-meeting";

        // Act
        var result = service.BuildOutputFilePath(recordingBaseName, templateId);

        // Assert
        result.Should().Contain("10-21-2025_2_business-meeting.md");
    }

    [Fact]
    public void BuildOutputFilePath_UsesMarkdownExtension()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);

        // Act
        var result = service.BuildOutputFilePath("10-21-2025_1", "template");

        // Assert
        result.Should().EndWith(".md");
    }

    #endregion

    #region OutputExistsAsync Tests

    [Fact]
    public async Task OutputExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var recordingBaseName = "10-21-2025_1";
        var templateId = "daily-summary";
        var filePath = $"{recordingDir}/{recordingBaseName}_{templateId}.md";
        fileSystem.AddFile(filePath, new MockFileData("existing content"));

        var service = CreateService(fileSystem);

        // Act
        var result = await service.OutputExistsAsync(recordingBaseName, templateId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task OutputExistsAsync_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var service = CreateService(fileSystem);

        // Act
        var result = await service.OutputExistsAsync("10-21-2025_1", "daily-summary");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SaveOutputAsync Tests

    [Fact]
    public async Task SaveOutputAsync_WithValidOutput_CreatesFileWithCorrectContent()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var service = CreateService(fileSystem);
        var output = CreateTestOutput();

        // Act
        var result = await service.SaveOutputAsync(output);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var outputPath = result.Value;
        fileSystem.File.Exists(outputPath).Should().BeTrue();

        var savedContent = fileSystem.File.ReadAllText(outputPath);
        savedContent.Should().Contain(output.Content);
        savedContent.Should().Contain("---");
        savedContent.Should().Contain("entry-id:");
        savedContent.Should().Contain("command: generate");
    }

    [Fact]
    public async Task SaveOutputAsync_IncludesMetadataInOutput()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var service = CreateService(fileSystem);
        var output = CreateTestOutput();

        // Act
        var result = await service.SaveOutputAsync(output);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var savedContent = fileSystem.File.ReadAllText(result.Value);

        savedContent.Should().Contain(output.RecordingBaseName);
        savedContent.Should().Contain(output.TemplateId);
        savedContent.Should().Contain(output.ProviderName);
        savedContent.Should().Contain(output.ModelName);
        savedContent.Should().Contain(output.InputTokens.ToString());
        savedContent.Should().Contain(output.OutputTokens.ToString());
    }

    [Fact]
    public async Task SaveOutputAsync_WhenFileExists_OverwritesIt()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var service = CreateService(fileSystem);
        var output = CreateTestOutput();

        // Pre-create the file with old content
        var outputPath = service.BuildOutputFilePath(output.RecordingBaseName, output.TemplateId);
        fileSystem.AddFile(outputPath, new MockFileData("old content"));

        // Act
        var result = await service.SaveOutputAsync(output);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var savedContent = fileSystem.File.ReadAllText(result.Value);
        savedContent.Should().NotContain("old content");
        savedContent.Should().Contain(output.Content);
    }

    [Fact]
    public async Task SaveOutputAsync_WhenDirectoryMissing_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        // Don't create the recording directory

        var service = CreateService(fileSystem);
        var output = CreateTestOutput();

        // Act
        var result = await service.SaveOutputAsync(output);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task SaveOutputAsync_ReturnsSavedFilePath()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var service = CreateService(fileSystem);
        var output = CreateTestOutput();

        // Act
        var result = await service.SaveOutputAsync(output);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().EndWith(".md");
        result.Value.Should().Contain(output.RecordingBaseName);
        result.Value.Should().Contain(output.TemplateId);
    }

    [Fact]
    public async Task SaveOutputAsync_WithTruncatedTranscript_IncludesTruncationInfo()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var service = CreateService(fileSystem);
        var output = CreateTestOutput() with
        {
            WasTruncated = true,
            OriginalWordCount = 500
        };

        // Act
        var result = await service.SaveOutputAsync(output);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var savedContent = fileSystem.File.ReadAllText(result.Value);
        savedContent.Should().Contain("truncated: true");
        savedContent.Should().Contain("original-word-count: 500");
    }

    #endregion

    #region Helper Methods

    private OutputStorageService CreateService(IFileSystem fileSystem)
    {
        return new OutputStorageService(
            fileSystem,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    private static GeneratedOutput CreateTestOutput()
    {
        return new GeneratedOutput
        {
            Content = "This is the generated content from the LLM.",
            RecordingBaseName = "10-21-2025_1",
            TemplateId = "daily-summary",
            TemplateTitle = "Daily Summary",
            GeneratedAt = DateTimeOffset.UtcNow,
            ProviderName = "TestProvider",
            ModelName = "test-model",
            InputTokens = 100,
            OutputTokens = 50,
            WasTruncated = false,
            OriginalWordCount = 150
        };
    }

    #endregion
}
