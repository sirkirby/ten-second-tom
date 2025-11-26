using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Audio;
using TenSecondTom.Infrastructure.Configuration;
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

        var sectionStore = new Mock<IConfigurationSectionStore>();

        // Mock ReadSectionAsync to return current audio config
        sectionStore
            .Setup(s => s.ReadSectionAsync<AudioOptions>(AudioOptions.SectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioOptions>.Success(currentAudio));

        // Mock WriteSectionAsync
        sectionStore
            .Setup(s => s.WriteSectionAsync(AudioOptions.SectionPath, It.IsAny<AudioOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("config.json"));

        var wizard = new Mock<ISetupWizardUI>();
        wizard.Setup(w => w.ShowSuccess(It.IsAny<string>()));
        wizard.Setup(w => w.ShowStatus(It.IsAny<string>()));

        var logger = Mock.Of<ILogger<ConfigureAudio.Handler>>();

        var handler = new ConfigureAudio.Handler(
            sectionStore.Object,
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
        result.Value!.Audio.Timeouts.RecordSeconds.Should().Be(3600);

        sectionStore.Verify(s => s.WriteSectionAsync(
                AudioOptions.SectionPath,
                It.Is<AudioOptions>(opts => opts.Timeouts.RecordSeconds == 3600),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no interactive prompts were shown (since we provided CLI override)
        wizard.Verify(w => w.PromptForInputVolumeAsync(It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Never);
        wizard.Verify(w => w.PromptForBooleanAsync(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
        wizard.Verify(w => w.PromptForIntAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
