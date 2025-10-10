using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Configuration;

/// <summary>
/// Unit tests for EnvironmentHelper.
/// Tests centralized environment detection logic.
/// </summary>
public sealed class EnvironmentHelperTests
{
    [Fact]
    public void GetCurrentEnvironment_WithNoConfiguration_ReturnsProduction()
    {
        // Act
        var environment = EnvironmentHelper.GetCurrentEnvironment();

        // Assert
        environment.Should().Be(EnvironmentNames.Production);
    }

    [Fact]
    public void GetCurrentEnvironment_WithDotNetEnvironmentInConfig_ReturnsValue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, EnvironmentNames.Development }
            })
            .Build();

        // Act
        var environment = EnvironmentHelper.GetCurrentEnvironment(configuration);

        // Assert
        environment.Should().Be(EnvironmentNames.Development);
    }

    [Fact]
    public void GetCurrentEnvironment_WithEnvironmentVariable_ReturnsValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ConfigurationKeys.DotNetEnvironment, EnvironmentNames.Development);

        try
        {
            // Act
            var environment = EnvironmentHelper.GetCurrentEnvironment();

            // Assert
            environment.Should().Be(EnvironmentNames.Development);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.DotNetEnvironment, null);
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void GetCurrentEnvironment_WithValidEnvironment_ReturnsCaseSensitive(string envName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, envName }
            })
            .Build();

        // Act
        var environment = EnvironmentHelper.GetCurrentEnvironment(configuration);

        // Assert
        environment.Should().Be(envName);
    }

    [Fact]
    public void IsDevelopment_WithDevelopmentEnvironment_ReturnsTrue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, EnvironmentNames.Development }
            })
            .Build();

        // Act
        var isDevelopment = EnvironmentHelper.IsDevelopment(configuration);

        // Assert
        isDevelopment.Should().BeTrue();
    }

    [Fact]
    public void IsDevelopment_WithProductionEnvironment_ReturnsFalse()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, EnvironmentNames.Production }
            })
            .Build();

        // Act
        var isDevelopment = EnvironmentHelper.IsDevelopment(configuration);

        // Assert
        isDevelopment.Should().BeFalse();
    }

    [Fact]
    public void IsDevelopment_WithNoConfiguration_ReturnsFalse()
    {
        // Act
        var isDevelopment = EnvironmentHelper.IsDevelopment();

        // Assert (defaults to Production)
        isDevelopment.Should().BeFalse();
    }

    [Fact]
    public void IsProduction_WithProductionEnvironment_ReturnsTrue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, EnvironmentNames.Production }
            })
            .Build();

        // Act
        var isProduction = EnvironmentHelper.IsProduction(configuration);

        // Assert
        isProduction.Should().BeTrue();
    }

    [Fact]
    public void IsProduction_WithDevelopmentEnvironment_ReturnsFalse()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, EnvironmentNames.Development }
            })
            .Build();

        // Act
        var isProduction = EnvironmentHelper.IsProduction(configuration);

        // Assert
        isProduction.Should().BeFalse();
    }

    [Fact]
    public void IsProduction_WithNoConfiguration_ReturnsTrue()
    {
        // Act
        var isProduction = EnvironmentHelper.IsProduction();

        // Assert (defaults to Production)
        isProduction.Should().BeTrue();
    }

    [Fact]
    public void IsDevelopment_WithCaseInsensitiveMatch_ReturnsTrue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, "development" } // lowercase
            })
            .Build();

        // Act
        var isDevelopment = EnvironmentHelper.IsDevelopment(configuration);

        // Assert
        isDevelopment.Should().BeTrue();
    }

    [Fact]
    public void IsProduction_WithCaseInsensitiveMatch_ReturnsTrue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { ConfigurationKeys.DotNetEnvironment, "PRODUCTION" } // uppercase
            })
            .Build();

        // Act
        var isProduction = EnvironmentHelper.IsProduction(configuration);

        // Assert
        isProduction.Should().BeTrue();
    }
}
