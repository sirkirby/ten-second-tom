using FluentAssertions;
using Moq;
using TenSecondTom.Features.Audio;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Audio;

public sealed class GetAudioConfigurationTests
{
    [Fact]
    public async Task Handle_WhenSectionExists_ReturnsStoredOptions()
    {
        // Arrange
        var expected = new AudioOptions
        {
            Timeouts = new RecordingTimeoutsOptions
            {
                TodaySeconds = 600,
                RecordSeconds = 2400
            }
        };

        var sectionStore = new Mock<IConfigurationSectionStore>();
        sectionStore
            .Setup(s => s.ReadSectionAsync<AudioOptions>(AudioOptions.SectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioOptions>.Success(expected));

        var handler = new GetAudioConfiguration.Handler(sectionStore.Object);

        // Act
        var result = await handler.Handle(new GetAudioConfiguration.Query(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_WhenSectionReadFails_ReturnsFailure()
    {
        var sectionStore = new Mock<IConfigurationSectionStore>();
        sectionStore
            .Setup(s => s.ReadSectionAsync<AudioOptions>(AudioOptions.SectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioOptions>.Failure("boom"));

        var handler = new GetAudioConfiguration.Handler(sectionStore.Object);

        var result = await handler.Handle(new GetAudioConfiguration.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("boom");
    }
}

