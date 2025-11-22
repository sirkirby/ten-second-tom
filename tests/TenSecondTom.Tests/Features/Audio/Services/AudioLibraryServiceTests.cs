using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Audio.Services;

/// <summary>
/// Tests for <see cref="AudioLibraryService"/> that validate discovery of note/recording audio files.
/// </summary>
public sealed class AudioLibraryServiceTests
{
    private readonly Mock<ILogger<AudioLibraryService>> _logger = new();
    private readonly Mock<IOptions<StorageOptions>> _storageOptions = new();
    private static readonly string[] RecordingFileNames = ["10-24-2025_1", "10-23-2025_2"];

    public AudioLibraryServiceTests()
    {
        _storageOptions.Setup(o => o.Value).Returns(new StorageOptions
        {
            RootDirectory = "/memory"
        });
    }

    [Fact]
    public async Task ListAudioFilesAsync_WithRecordingFiles_ReturnsItems()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/memory/recording");
        fileSystem.AddFile("/memory/recording/10-24-2025_1.wav", new MockFileData(new byte[] { 0x00 }));
        fileSystem.AddFile("/memory/recording/10-23-2025_2.wav", new MockFileData(new byte[] { 0x00 }));

        var service = CreateService(fileSystem);

        // Act
        var result = await service.ListAudioFilesAsync(AudioLibraryScope.Recording, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(i => i.BaseName).Should().Contain(RecordingFileNames);
    }

    [Fact]
    public async Task ListAudioFilesAsync_WithMissingDirectory_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(new MockFileSystem());

        // Act
        var result = await service.ListAudioFilesAsync(AudioLibraryScope.Note, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("note");
    }

    [Fact]
    public void GetAudioFile_WithExistingBaseName_ReturnsItem()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/memory/note");
        fileSystem.AddFile("/memory/note/10-21-2025_1.wav", new MockFileData(new byte[] { 0x00 }));

        var service = CreateService(fileSystem);

        // Act
        var result = service.GetAudioFile(AudioLibraryScope.Note, "10-21-2025_1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AudioFilePath.Should().Be("/memory/note/10-21-2025_1.wav");
    }

    [Fact]
    public void GetAudioFile_WithUnknownBaseName_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(new MockFileSystem());

        // Act
        var result = service.GetAudioFile(AudioLibraryScope.Recording, "missing");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("missing");
    }

    private AudioLibraryService CreateService(MockFileSystem fileSystem)
    {
        return new AudioLibraryService(
            fileSystem,
            _storageOptions.Object,
            _logger.Object);
    }
}
