using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Abstractions.Notifications;

namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class FfmpegAudioRecorderTests
{
    private readonly Mock<ILogger<FfmpegAudioRecorder>> _mockLogger = new();
    private readonly Mock<INotificationService> _mockNotificationService = new();
    private readonly AudioOptions _config = new()
    {
        Recorder = new RecorderOptions
        {
            FfmpegPath = "ffmpeg"
        },
        Timeouts = new RecordingTimeoutsOptions
        {
            TodaySeconds = 180,
            RecordSeconds = 900
        }
    };

    [Fact]
    public async Task IsAvailableAsync_WhenFfmpegDoesNotExist_ReturnsFalse()
    {
        var invalidConfig = new AudioOptions
        {
            Recorder = new RecorderOptions
            {
                FfmpegPath = "nonexistent-ffmpeg-binary"
            },
            Timeouts = new RecordingTimeoutsOptions
            {
                TodaySeconds = 180,
                RecordSeconds = 900
            }
        };

        var recorder = CreateRecorder(invalidConfig);

        var result = await recorder.IsAvailableAsync();

        result.Should().BeFalse("FFmpeg binary does not exist");
    }

    private FfmpegAudioRecorder CreateRecorder(AudioOptions? config = null)
    {
        config ??= _config;
        return new FfmpegAudioRecorder(Options.Create(config), _mockLogger.Object, _mockNotificationService.Object);
    }
}
