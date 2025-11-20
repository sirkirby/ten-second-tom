using TenSecondTom.Features.Audio;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Options;
using Xunit;


namespace TenSecondTom.Tests.Features.Audio.Services;

/// <summary>
/// Tests for <see cref="FfmpegAudioPreprocessor"/>.
/// Note: Tests that require actual FFmpeg execution have been moved to integration tests.
/// </summary>
public sealed class FfmpegAudioPreprocessorTests
{
    private readonly Mock<ILogger<FfmpegAudioPreprocessor>> _mockLogger;
    private readonly AudioOptions _config;

    public FfmpegAudioPreprocessorTests()
    {
        _mockLogger = new Mock<ILogger<FfmpegAudioPreprocessor>>();
        _config = new AudioOptions
        {
            Recorder = new RecorderOptions { FfmpegPath = "ffmpeg" },
            Preprocessing = new PreprocessingOptions
            {
                RemoveSilence = true,
                SilenceThresholdDb = -40,
                MinimumSilenceDurationMs = 500
            }
        };
    }

    private FfmpegAudioPreprocessor CreatePreprocessor(AudioOptions? config = null)
    {
        var options = Options.Create(config ?? _config);
        return new FfmpegAudioPreprocessor(options, _mockLogger.Object);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenFfmpegDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var config = new AudioOptions
        {
            Recorder = new RecorderOptions { FfmpegPath = "nonexistent-ffmpeg-binary" },
            Preprocessing = new PreprocessingOptions()
        };
        var preprocessor = CreatePreprocessor(config);

        // Act
        var result = await preprocessor.IsAvailableAsync();

        // Assert
        result.Should().BeFalse("FFmpeg should not be available with invalid path");
    }

    [Fact]
    public async Task PreprocessAsync_WithNullFilePath_ReturnsFailure()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();

        // Act
        var result = await preprocessor.PreprocessAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("file path");
    }

    [Fact]
    public async Task PreprocessAsync_WithEmptyFilePath_ReturnsFailure()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();

        // Act
        var result = await preprocessor.PreprocessAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("file path");
    }

    [Fact]
    public async Task PreprocessAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.wav");

        // Act
        var result = await preprocessor.PreprocessAsync(nonExistentPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task PreprocessAsync_WhenRemoveSilenceDisabled_ReturnsSuccessWithoutProcessing()
    {
        // Arrange
        var config = new AudioOptions
        {
            Recorder = new RecorderOptions { FfmpegPath = "ffmpeg" },
            Preprocessing = new PreprocessingOptions { RemoveSilence = false }
        };
        var preprocessor = CreatePreprocessor(config);

        // Create a temp audio file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.wav");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x52, 0x49, 0x46, 0x46 }); // Minimal WAV header

        try
        {
            // Act
            var result = await preprocessor.PreprocessAsync(tempFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ProcessedFilePath.Should().Be(tempFile);
            result.Value.OriginalSizeBytes.Should().Be(result.Value.ProcessedSizeBytes);
            result.Value.ProcessingTime.Should().Be(TimeSpan.Zero, "no actual processing should occur");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

