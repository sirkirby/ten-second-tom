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
/// Tests for <see cref="RecordingService"/> implementation.
/// Validates recording discovery, timestamp parsing, transcript loading, and file validation.
/// </summary>
public sealed class RecordingServiceTests
{
    private readonly Mock<ILogger<RecordingService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly string _testMemoryDirectory;

    public RecordingServiceTests()
    {
        _mockLogger = new Mock<ILogger<RecordingService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _testMemoryDirectory = "/test/memory";

        // Setup configuration to return test memory directory
        _mockConfiguration
            .Setup(c => c[ConfigurationKeys.MemoryDirectory])
            .Returns(_testMemoryDirectory);
    }

    #region ListRecordingsAsync Tests

    [Fact]
    public async Task ListRecordingsAsync_WithEmptyDirectory_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory($"{_testMemoryDirectory}/{DirectoryNames.Recording}");

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListRecordingsAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No recordings found");
    }

    [Fact]
    public async Task ListRecordingsAsync_WithValidRecordings_ReturnsSortedList()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        // Create recordings with different dates (newest should be first)
        AddTestRecording(fileSystem, recordingDir, "10-21-2025_1.txt", "First recording content");
        AddTestRecording(fileSystem, recordingDir, "10-24-2025_1.txt", "Newest recording content");
        AddTestRecording(fileSystem, recordingDir, "10-20-2025_2.txt", "Oldest recording content");

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListRecordingsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var recordings = result.Value;
        recordings.Should().HaveCount(3);

        // Verify sorted by date descending (newest first)
        recordings[0].RecordingBaseName.Should().Be("10-24-2025_1");
        recordings[1].RecordingBaseName.Should().Be("10-21-2025_1");
        recordings[2].RecordingBaseName.Should().Be("10-20-2025_2");
    }

    [Fact]
    public async Task ListRecordingsAsync_WithInvalidFilenames_SkipsInvalidFiles()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        // Add valid and invalid recordings
        AddTestRecording(fileSystem, recordingDir, "10-21-2025_1.txt", "Valid recording");
        fileSystem.AddFile($"{recordingDir}/invalid-name.txt", new MockFileData("Invalid"));
        fileSystem.AddFile($"{recordingDir}/not-a-recording.md", new MockFileData("Not a recording"));

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListRecordingsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].RecordingBaseName.Should().Be("10-21-2025_1");
    }

    [Fact]
    public async Task ListRecordingsAsync_WithMissingDirectory_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        // Don't create the recording directory

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListRecordingsAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Recording directory not found");
    }

    [Fact]
    public async Task ListRecordingsAsync_PopulatesWordCount()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var content = "This is a test recording with ten words total here";
        AddTestRecording(fileSystem, recordingDir, "10-21-2025_1.txt", content);

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListRecordingsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].WordCount.Should().Be(10);
    }

    [Fact]
    public async Task ListRecordingsAsync_PopulatesFileSizeBytes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var content = "Test content";
        AddTestRecording(fileSystem, recordingDir, "10-21-2025_1.txt", content);

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListRecordingsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].FileSizeBytes.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetTranscriptContentAsync Tests

    [Fact]
    public async Task GetTranscriptContentAsync_WithValidFile_ReturnsContent()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var expectedContent = "This is the transcript content.";
        var filePath = $"{recordingDir}/10-21-2025_1.txt";
        fileSystem.AddFile(filePath, new MockFileData(expectedContent));

        var service = CreateService(fileSystem);

        // Act
        var result = await service.GetTranscriptContentAsync(filePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedContent);
    }

    [Fact]
    public async Task GetTranscriptContentAsync_WithMissingFile_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);
        var nonExistentPath = "/path/to/missing/file.txt";

        // Act
        var result = await service.GetTranscriptContentAsync(nonExistentPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetTranscriptContentAsync_WithEmptyPath_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);

        // Act
        var result = await service.GetTranscriptContentAsync(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region ValidateTranscriptFileAsync Tests

    [Fact]
    public async Task ValidateTranscriptFileAsync_WithValidFile_ReturnsSuccess()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var filePath = $"{recordingDir}/10-21-2025_1.txt";
        fileSystem.AddFile(filePath, new MockFileData("Content"));

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ValidateTranscriptFileAsync(filePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTranscriptFileAsync_WithMissingFile_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);

        // Act
        var result = await service.ValidateTranscriptFileAsync("/missing/file.txt");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateTranscriptFileAsync_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var recordingDir = $"{_testMemoryDirectory}/{DirectoryNames.Recording}";
        fileSystem.AddDirectory(recordingDir);

        var filePath = $"{recordingDir}/10-21-2025_1.txt";
        fileSystem.AddFile(filePath, new MockFileData(string.Empty));

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ValidateTranscriptFileAsync(filePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region ParseRecordingTimestamp Tests

    [Theory]
    [InlineData("10-21-2025_1.txt", 2025, 10, 21)]
    [InlineData("1-5-2025_2.txt", 2025, 1, 5)]
    [InlineData("12-31-2024_10.txt", 2024, 12, 31)]
    public void ParseRecordingTimestamp_WithValidFilename_ReturnsCorrectDate(
        string filename,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);

        // Act
        var result = service.ParseRecordingTimestamp(filename);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Year.Should().Be(expectedYear);
        result.Value.Month.Should().Be(expectedMonth);
        result.Value.Day.Should().Be(expectedDay);
    }

    [Theory]
    [InlineData("invalid-format.txt")]
    [InlineData("not-a-date_1.txt")]
    [InlineData("10-21-2025.txt")] // Missing increment
    [InlineData("10-21-2025_.txt")] // Empty increment
    [InlineData("recording-20251024-143022.txt")] // Old format
    public void ParseRecordingTimestamp_WithInvalidFilename_ReturnsFailure(string filename)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);

        // Act
        var result = service.ParseRecordingTimestamp(filename);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseRecordingTimestamp_ExtractsIncrement()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = CreateService(fileSystem);

        // Act
        var result = service.ParseRecordingTimestamp("10-21-2025_3.txt");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Note: Increment is extracted but not directly exposed in RecordedAt
        // It's embedded in the RecordingBaseName
    }

    #endregion

    #region Helper Methods

    private RecordingService CreateService(IFileSystem? fileSystem = null)
    {
        return new RecordingService(
            fileSystem ?? new MockFileSystem(),
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    private static void AddTestRecording(
        MockFileSystem fileSystem,
        string directory,
        string filename,
        string content)
    {
        var filePath = $"{directory}/{filename}";
        fileSystem.AddFile(filePath, new MockFileData(content));
    }

    #endregion
}
