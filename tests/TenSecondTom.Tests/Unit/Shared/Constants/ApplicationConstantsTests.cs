using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.SharedConstants;

/// <summary>
/// Unit tests for the <see cref="ApplicationConstants"/> class.
/// Verifies that application branding constants are defined correctly.
/// </summary>
public sealed class ApplicationConstantsTests
{
    [Fact]
    public void ApplicationConstants_AllConstants_AreNotNullOrEmpty()
    {
        // Assert
        ApplicationConstants.ApplicationName.Should().NotBeNullOrWhiteSpace();
        ApplicationConstants.ApplicationNameWithVersionPrefix.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ApplicationConstants_Values_AreCorrect()
    {
        // Assert
        ApplicationConstants.ApplicationName.Should().Be("Ten Second Tom");
        ApplicationConstants.ApplicationNameWithVersionPrefix.Should().Be("Ten Second Tom v");
    }

    [Fact]
    public void ApplicationConstants_VersionPrefix_StartsWithApplicationName()
    {
        // Assert
        ApplicationConstants.ApplicationNameWithVersionPrefix.Should().StartWith(ApplicationConstants.ApplicationName);
    }

    [Fact]
    public void ApplicationConstants_VersionPrefix_EndsWithSpaceAndV()
    {
        // Assert
        ApplicationConstants.ApplicationNameWithVersionPrefix.Should().EndWith(" v");
    }
}
