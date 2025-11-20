using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Audio.Handlers;

public sealed class ConfigureAudioHandlerTests
{
    [Fact]
    public async Task Handle_WithCommandLineOverride_UpdatesRecorderTimeoutWithoutPrompts()
    {
        // Arrange
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<UpdateAudioConfiguration.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("ok"));

        var currentAudio = new AudioOptions
        {
            Recorder = new RecorderOptions
            {
                FfmpegPath = "ffmpeg",
                InputVolume = 1.0,
                EnableNoiseReduction = true,
                EnableFrequencyFilters = true
            },
            Preprocessing = new PreprocessingOptions
            {
                RemoveSilence = false,
                SilenceThresholdDb = -50,
                MinimumSilenceDurationMs = 500
            },
            Timeouts = new RecordingTimeoutsOptions
            {
                TodaySeconds = 300,
                RecordSeconds = 1800
            }
        };

        var options = Options.Create(currentAudio);

        var wizard = new Mock<ISetupWizardUI>();
        wizard.Setup(w => w.ShowSuccess(It.IsAny<string>()));
        wizard.Setup(w => w.ShowStatus(It.IsAny<string>()));

        var logger = Mock.Of<ILogger<ConfigureAudio.Handler>>();

        var handler = new ConfigureAudio.Handler(
            mediator.Object,
            options,
            wizard.Object,
            logger);

        var command = new ConfigureAudio.Command
        {
            RecordTimeoutSeconds = 3600
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Timeouts.RecordSeconds.Should().Be(3600);

        mediator.Verify(m => m.Send(
                It.Is<UpdateAudioConfiguration.Command>(cmd => cmd.Config.Timeouts.RecordSeconds == 3600),
                It.IsAny<CancellationToken>()),
            Times.Once);

        wizard.Verify(w => w.PromptForInputVolumeAsync(It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Never);
        wizard.Verify(w => w.PromptForBooleanAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
        wizard.Verify(w => w.PromptForIntAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        wizard.Verify(w => w.PromptForSttProviderAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        wizard.Verify(w => w.PromptForSttApiKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

