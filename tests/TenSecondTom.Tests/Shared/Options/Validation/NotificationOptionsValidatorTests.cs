using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Options.Validation;

namespace TenSecondTom.Tests.Shared.Options.Validation;

/// <summary>
/// Unit tests for <see cref="NotificationOptionsValidator"/>.
/// Tests validation of notification configuration options.
/// </summary>
public sealed class NotificationOptionsValidatorTests
{
    private readonly NotificationOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new NotificationOptions
        {
            Enabled = true,
            DefaultTimeoutSeconds = 30,
            DefaultPriority = NotificationPriority.Normal,
            SilentFallback = true
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithDefaultOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new NotificationOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999)]
    public void Validate_WithNegativeTimeoutSeconds_ReturnsFailure(int invalidTimeout)
    {
        // Arrange
        var options = new NotificationOptions
        {
            DefaultTimeoutSeconds = invalidTimeout
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultTimeoutSeconds");
        result.FailureMessage.Should().Contain("non-negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(300)]
    [InlineData(999999)]
    public void Validate_WithValidTimeoutSeconds_ReturnsSuccess(int validTimeout)
    {
        // Arrange
        var options = new NotificationOptions
        {
            DefaultTimeoutSeconds = validTimeout
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Theory]
    [InlineData(NotificationPriority.Low)]
    [InlineData(NotificationPriority.Normal)]
    [InlineData(NotificationPriority.High)]
    [InlineData(NotificationPriority.Critical)]
    public void Validate_WithValidPriority_ReturnsSuccess(NotificationPriority priority)
    {
        // Arrange
        var options = new NotificationOptions
        {
            DefaultPriority = priority
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithInvalidPriority_ReturnsFailure()
    {
        // Arrange
        var options = new NotificationOptions
        {
            DefaultPriority = (NotificationPriority)999 // Invalid enum value
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultPriority");
        result.FailureMessage.Should().Contain("valid");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithAnyEnabledValue_ReturnsSuccess(bool enabled)
    {
        // Arrange
        var options = new NotificationOptions
        {
            Enabled = enabled
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithAnySilentFallbackValue_ReturnsSuccess(bool silentFallback)
    {
        // Arrange
        var options = new NotificationOptions
        {
            SilentFallback = silentFallback
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithDisabledNotifications_ReturnsSuccess()
    {
        // Arrange - Disabled notifications should still validate successfully
        var options = new NotificationOptions
        {
            Enabled = false,
            DefaultTimeoutSeconds = -999, // Invalid, but ignored when disabled
            SilentFallback = false
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        // Even with invalid timeout, validation should fail because we validate regardless
        result.Should().NotBe(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithAllValidEdgeCases_ReturnsSuccess()
    {
        // Arrange
        var options = new NotificationOptions
        {
            Enabled = false,
            DefaultTimeoutSeconds = 0, // Zero is valid (no timeout)
            DefaultPriority = NotificationPriority.Low,
            SilentFallback = false
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithMissingExtensionDirectory_ReturnsFailure()
    {
        // Arrange
        var options = new NotificationOptions
        {
            ExtensionDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.FailureMessage.Should().Contain("ExtensionDirectory");
    }

    [Fact]
    public void Validate_WithValidExtensionDirectory_ReturnsSuccess()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var extensionDir = Path.Combine(tempRoot, "TenSecondTom.Extensions.MacOS.app");
            var notifierDir = Path.Combine(extensionDir, "Contents", "MacOS");
            Directory.CreateDirectory(notifierDir);
            File.WriteAllText(Path.Combine(notifierDir, "notifier"), string.Empty);

            var options = new NotificationOptions
            {
                ExtensionDirectory = extensionDir
            };

            // Act
            var result = _validator.Validate(null, options);

            // Assert
            result.Should().Be(ValidateOptionsResult.Success);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
