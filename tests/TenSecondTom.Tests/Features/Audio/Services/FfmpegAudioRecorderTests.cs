using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class FfmpegAudioRecorderTests
{
    private readonly Mock<ILogger<FfmpegAudioRecorder>> _mockLogger = new();
    private readonly AudioConfiguration _config = new()
    {
        Recorder = new RecorderConfiguration
        {
            FfmpegPath = "ffmpeg"
        },
        Timeouts = new RecordingTimeoutsConfiguration
        {
            TodaySeconds = 180,
            RecordSeconds = 900
        }
    };

    [Fact]
    public async Task IsAvailableAsync_WhenFfmpegDoesNotExist_ReturnsFalse()
    {
        var invalidConfig = new AudioConfiguration
        {
            Recorder = new RecorderConfiguration
            {
                FfmpegPath = "nonexistent-ffmpeg-binary"
            },
            Timeouts = new RecordingTimeoutsConfiguration
            {
                TodaySeconds = 180,
                RecordSeconds = 900
            }
        };

        var recorder = CreateRecorder(invalidConfig);

        var result = await recorder.IsAvailableAsync();

        result.Should().BeFalse("FFmpeg binary does not exist");
    }

    private FfmpegAudioRecorder CreateRecorder(AudioConfiguration? config = null)
    {
        config ??= _config;
        return new FfmpegAudioRecorder(Options.Create(config), _mockLogger.Object);
    }
}

