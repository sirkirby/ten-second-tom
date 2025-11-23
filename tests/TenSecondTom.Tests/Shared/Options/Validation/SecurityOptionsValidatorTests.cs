using FluentAssertions;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Options.Validation;

namespace TenSecondTom.Tests.Shared.Options.Validation;

/// <summary>
/// Unit tests for <see cref="SecurityOptionsValidator"/>.
/// Tests validation of security configuration options.
/// </summary>
public sealed class SecurityOptionsValidatorTests
{
    private readonly SecurityOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = "valid-secret-key-with-minimum-16-chars",
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithEmptyNotificationSecret_ReturnsFailure()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = string.Empty,
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("NotificationSecret");
        result.FailureMessage.Should().Contain("required");
    }

    [Fact]
    public void Validate_WithWhitespaceNotificationSecret_ReturnsFailure()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = "   ",
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("NotificationSecret");
        result.FailureMessage.Should().Contain("required");
    }

    [Fact]
    public void Validate_WithShortNotificationSecret_ReturnsFailure()
    {
        // Arrange - Less than 16 characters
        var options = new SecurityOptions
        {
            NotificationSecret = "short-secret",
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("NotificationSecret");
        result.FailureMessage.Should().Contain("at least 16 characters");
    }

    [Fact]
    public void Validate_WithMinimumLengthSecret_ReturnsSuccess()
    {
        // Arrange - Exactly 16 characters
        var options = new SecurityOptions
        {
            NotificationSecret = "1234567890123456", // Exactly 16 chars
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithLongSecret_ReturnsSuccess()
    {
        // Arrange - Very long secret
        var options = new SecurityOptions
        {
            NotificationSecret = new string('a', 128), // 128 characters
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999)]
    public void Validate_WithNonPositiveMaxTokenAge_ReturnsFailure(int invalidAge)
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = "valid-secret-key-with-minimum-16-chars",
            MaxTokenAgeSeconds = invalidAge
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTokenAgeSeconds");
        result.FailureMessage.Should().Contain("positive");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(3600)]
    [InlineData(999999)]
    public void Validate_WithPositiveMaxTokenAge_ReturnsSuccess(int validAge)
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = "valid-secret-key-with-minimum-16-chars",
            MaxTokenAgeSeconds = validAge
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithDefaultMaxTokenAge_ReturnsSuccess()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = "valid-secret-key-with-minimum-16-chars"
            // MaxTokenAgeSeconds defaults to 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithMultipleViolations_ReturnsFailure()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = "short", // Too short
            MaxTokenAgeSeconds = -1 // Negative
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().NotBe(ValidateOptionsResult.Success);
        result.Failed.Should().BeTrue();
        // Should fail on first violation (secret length)
        result.FailureMessage.Should().Contain("NotificationSecret");
    }

    [Fact]
    public void Validate_ErrorMessage_IncludesConfigurationPath()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = string.Empty,
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.FailureMessage.Should().Contain("TenSecondTom:Security:NotificationSecret");
    }

    [Fact]
    public void Validate_ErrorMessage_IncludesSecurityWarning()
    {
        // Arrange
        var options = new SecurityOptions
        {
            NotificationSecret = string.Empty,
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.FailureMessage.Should().Contain("NEVER commit this secret to source control");
    }

    [Fact]
    public void Validate_WithSpecialCharactersInSecret_ReturnsSuccess()
    {
        // Arrange - Secret with special characters
        var options = new SecurityOptions
        {
            NotificationSecret = "!@#$%^&*()_+-=[]{}|;:,.<>?~`1234567890",
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Fact]
    public void Validate_WithUnicodeSecret_ReturnsSuccess()
    {
        // Arrange - Secret with unicode characters
        var options = new SecurityOptions
        {
            NotificationSecret = "密码密钥密钥密钥密钥密钥密钥",
            MaxTokenAgeSeconds = 300
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Should().Be(ValidateOptionsResult.Success);
    }
}
