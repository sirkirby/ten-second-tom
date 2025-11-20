namespace TenSecondTom.Tests.Infrastructure.Cli;

using Xunit;
using FluentAssertions;
using TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Unit tests for the <see cref="Logo"/> class.
/// </summary>
public sealed class LogoTests
{
    [Fact]
    public void Display_WithSuppressOutput_DoesNotThrowException()
    {
        // Arrange & Act
        Action act = () => Logo.Display(suppressOutput: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Display_WithoutSuppressOutput_DoesNotThrowException()
    {
        // Arrange & Act
        Action act = () => Logo.Display(suppressOutput: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetVersionInfo_ReturnsVersionString()
    {
        // Act
        string versionInfo = Logo.GetVersionInfo();

        // Assert
        versionInfo.Should().NotBeNullOrEmpty();
        versionInfo.Should().StartWith("Ten Second Tom v");
        versionInfo.Should().MatchRegex(@"Ten Second Tom v\d+\.\d+\.\d+");
    }

    [Fact]
    public void DisplayWithVersion_WithSuppressOutput_DoesNotThrowException()
    {
        // Arrange & Act
        Action act = () => Logo.DisplayWithVersion(suppressOutput: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DisplayWithVersion_WithoutSuppressOutput_DoesNotThrowException()
    {
        // Arrange & Act
        Action act = () => Logo.DisplayWithVersion(suppressOutput: false);

        // Assert
        act.Should().NotThrow();
    }
}
