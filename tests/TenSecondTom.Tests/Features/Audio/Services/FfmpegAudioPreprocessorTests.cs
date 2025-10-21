using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Tests.Features.Audio.Services;

/// <summary>
/// Tests for <see cref="FfmpegAudioPreprocessor"/>.
/// </summary>
public sealed class FfmpegAudioPreprocessorTests
{
    private readonly Mock<ILogger<FfmpegAudioPreprocessor>> _mockLogger;
    private readonly AudioConfiguration _config;

    public FfmpegAudioPreprocessorTests()
    {
        _mockLogger = new Mock<ILogger<FfmpegAudioPreprocessor>>();
        _config = new AudioConfiguration
        {
            Recorder = new RecorderConfiguration { FfmpegPath = "ffmpeg" },
            Preprocessing = new PreprocessingConfiguration
            {
                RemoveSilence = true,
                SilenceThresholdDb = -40,
                MinimumSilenceDurationMs = 500
            }
        };
    }

    private FfmpegAudioPreprocessor CreatePreprocessor(AudioConfiguration? config = null)
    {
        var options = Options.Create(config ?? _config);
        return new FfmpegAudioPreprocessor(options, _mockLogger.Object);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenFfmpegExists_ReturnsTrue()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();

        // Act
        var result = await preprocessor.IsAvailableAsync();

        // Assert
        result.Should().BeTrue("FFmpeg should be available on the system");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenFfmpegDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            Recorder = new RecorderConfiguration { FfmpegPath = "nonexistent-ffmpeg-binary" },
            Preprocessing = new PreprocessingConfiguration()
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
        var config = new AudioConfiguration
        {
            Recorder = new RecorderConfiguration { FfmpegPath = "ffmpeg" },
            Preprocessing = new PreprocessingConfiguration { RemoveSilence = false }
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

    [Fact]
    public async Task PreprocessAsync_WhenReplaceOriginalTrue_ReplacesOriginalFile()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();

        // Create a minimal valid WAV file (silent audio)
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.wav");
        CreateMinimalWavFile(tempFile);

        try
        {
            var originalSize = new FileInfo(tempFile).Length;

            // Act
            var result = await preprocessor.PreprocessAsync(tempFile, replaceOriginal: true);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ProcessedFilePath.Should().Be(tempFile, "should return the same path when replacing");
            File.Exists(tempFile).Should().BeTrue("original file should still exist (but replaced)");
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

    [Fact]
    public async Task PreprocessAsync_WhenReplaceOriginalFalse_CreatesNewFile()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();

        // Create a minimal valid WAV file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.wav");
        CreateMinimalWavFile(tempFile);

        try
        {
            // Act
            var result = await preprocessor.PreprocessAsync(tempFile, replaceOriginal: false);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ProcessedFilePath.Should().NotBe(tempFile, "should create a new file");
            result.Value.ProcessedFilePath.Should().Contain("_processed", "processed file should have identifier");
            File.Exists(tempFile).Should().BeTrue("original file should still exist");
            File.Exists(result.Value.ProcessedFilePath).Should().BeTrue("processed file should exist");

            // Cleanup processed file
            if (File.Exists(result.Value.ProcessedFilePath))
            {
                File.Delete(result.Value.ProcessedFilePath);
            }
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

    [Fact]
    public async Task PreprocessAsync_ReturnsStatisticsWithOriginalAndProcessedInfo()
    {
        // Arrange
        var preprocessor = CreatePreprocessor();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.wav");
        CreateMinimalWavFile(tempFile);

        try
        {
            var originalSize = new FileInfo(tempFile).Length;

            // Act
            var result = await preprocessor.PreprocessAsync(tempFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.OriginalSizeBytes.Should().BeGreaterThan(0);
            result.Value.ProcessedSizeBytes.Should().BeGreaterThan(0);
            result.Value.OriginalDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            result.Value.ProcessedDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            result.Value.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero, "actual processing should take some time");
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

    private static readonly char[] RiffChunkId = ['R', 'I', 'F', 'F'];
    private static readonly char[] WaveFormat = ['W', 'A', 'V', 'E'];
    private static readonly char[] FmtSubchunkId = ['f', 'm', 't', ' '];
    private static readonly char[] DataSubchunkId = ['d', 'a', 't', 'a'];

    /// <summary>
    /// Creates a minimal valid WAV file with 1 second of silence at 16kHz mono.
    /// This matches the format expected by the audio pipeline.
    /// </summary>
    private static void CreateMinimalWavFile(string filePath)
    {
        const int sampleRate = 16000;
        const int channels = 1;
        const int bitsPerSample = 16;
        const int durationSeconds = 1;
        const int dataSize = sampleRate * channels * (bitsPerSample / 8) * durationSeconds;

        using var stream = new FileStream(filePath, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        // WAV header
        writer.Write(RiffChunkId); // ChunkID
        writer.Write(36 + dataSize); // ChunkSize
        writer.Write(WaveFormat); // Format

        // fmt subchunk
        writer.Write(FmtSubchunkId); // Subchunk1ID
        writer.Write(16); // Subchunk1Size (PCM)
        writer.Write((short)1); // AudioFormat (PCM)
        writer.Write((short)channels); // NumChannels
        writer.Write(sampleRate); // SampleRate
        writer.Write(sampleRate * channels * bitsPerSample / 8); // ByteRate
        writer.Write((short)(channels * bitsPerSample / 8)); // BlockAlign
        writer.Write((short)bitsPerSample); // BitsPerSample

        // data subchunk
        writer.Write(DataSubchunkId); // Subchunk2ID
        writer.Write(dataSize); // Subchunk2Size

        // Silent audio data (zeros)
        writer.Write(new byte[dataSize]);
    }
}

