using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Validation;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Setup.Validation;

/// <summary>
/// Unit tests for <see cref="OpenAIApiKeyValidator"/>
/// Tests API key format validation and network validation structure
/// </summary>
public sealed class OpenAIApiKeyValidatorTests
{
    private readonly Mock<ILogger<OpenAIApiKeyValidator>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public OpenAIApiKeyValidatorTests()
    {
        _mockLogger = new Mock<ILogger<OpenAIApiKeyValidator>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new OpenAIApiKeyValidator(null!, _mockHttpClientFactory.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new OpenAIApiKeyValidator(_mockLogger.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Provider_ReturnsOpenAI()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Assert
        validator.Provider.Should().Be(LlmProvider.OpenAI);
    }

    #endregion

    #region Format Validation Tests - Empty/Null Keys

    [Fact]
    public async Task ValidateFormatAsync_WithNullKey_ReturnsFormatFailure()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
        result.NetworkValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task ValidateFormatAsync_WithEmptyKey_ReturnsFormatFailure()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(string.Empty);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
        result.NetworkValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task ValidateFormatAsync_WithWhitespaceKey_ReturnsFormatFailure()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync("   ");

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
        result.NetworkValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    #endregion

    #region Format Validation Tests - Invalid Formats

    [Theory]
    [InlineData("sk-")]
    [InlineData("sk-abc")]
    [InlineData("sk-1234567890123456789012345678901234567890")]  // Too short (40 chars)
    [InlineData("sk-12345678901234567890123456789012345678901234567")]  // 47 chars (need 48+)
    [InlineData("invalid-key")]
    [InlineData("sk_proj_123456789012345678901234567890123456789012345678")]  // Wrong prefix
    [InlineData("not-an-api-key")]
    public async Task ValidateFormatAsync_WithInvalidFormat_ReturnsFormatFailure(string invalidKey)
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(invalidKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
        result.NetworkValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid OpenAI API key format");
        result.ErrorMessage.Should().Contain("sk-[48+ alphanumeric characters]");
    }

    [Theory]
    [InlineData("sk-with-special-chars!@#$%^&*()123456789012345678901234567")]
    [InlineData("sk-with spaces 123456789012345678901234567890123456789")]
    [InlineData("sk-with-dashes-123456789012345678901234567890123456789")]
    public async Task ValidateFormatAsync_WithSpecialCharacters_ReturnsFormatFailure(string invalidKey)
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(invalidKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
    }

    #endregion

    #region Format Validation Tests - Valid Formats

    [Theory]
    [InlineData("sk-123456789012345678901234567890123456789012345678")]  // Exactly 48 chars after sk-
    [InlineData("sk-1234567890123456789012345678901234567890123456789")]  // 49 chars
    [InlineData("sk-abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]  // Mixed case + numbers
    [InlineData("sk-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz012345678901234567890")]  // 80+ chars
    public async Task ValidateFormatAsync_WithValidFormat_ReturnsFormatSuccess(string validKey)
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(validKey);

        // Assert
        result.IsValid.Should().BeTrue();
        result.FormatValid.Should().BeTrue();
        result.NetworkValid.Should().BeFalse(); // Network not tested yet
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region Format Validation Logging Tests

    [Fact]
    public async Task ValidateFormatAsync_WithInvalidFormat_LogsWarning()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        await validator.ValidateFormatAsync("invalid-key");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("format validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task ValidateFormatAsync_WithValidFormat_LogsDebug()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        await validator.ValidateFormatAsync("sk-123456789012345678901234567890123456789012345678");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("format validation passed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    #endregion

    #region Network Validation Tests - Structure & Cancellation

    [Fact(Skip = "Requires mocking OpenAI ChatClient - complex SDK integration")]
    public void ValidateNetworkAsync_WithValidKey_ReturnsSuccess()
    {
        // This test requires mocking the OpenAI SDK's ChatClient which is complex
        // The actual OpenAI SDK uses internal clients that are difficult to mock
        // Better suited for integration tests with actual API keys or test API endpoints
    }

    [Fact(Skip = "Requires mocking OpenAI ChatClient - complex SDK integration")]
    public void ValidateNetworkAsync_WithInvalidKey_ReturnsNetworkFailure()
    {
        // This test requires mocking the OpenAI SDK's ChatClient
        // Better suited for integration tests
    }

    [Fact]
    public async Task ValidateNetworkAsync_WithCancellation_ReturnsFailureWithCancelledMessage()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var validKey = "sk-123456789012345678901234567890123456789012345678";
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act
        var result = await validator.ValidateNetworkAsync(validKey, maxRetries: 3, cts.Token);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeTrue(); // Format check passes before network
        result.NetworkValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact(Skip = "Requires complex OpenAI SDK mocking for retry simulation")]
    public void ValidateNetworkAsync_WithRetries_UsesExponentialBackoff()
    {
        // This test would verify:
        // - Attempt 1 fails, waits 1s (2^0)
        // - Attempt 2 fails, waits 2s (2^1)
        // - Attempt 3 fails, waits 4s (2^2)
        // - Attempt 4 fails (maxRetries=3), returns failure
        // Requires mocking OpenAI ChatClient to throw exceptions
        // Better suited for integration tests
    }

    #endregion

    #region Network Validation Logging Tests

    [Fact(Skip = "Requires OpenAI SDK mocking - integration test")]
    public void ValidateNetworkAsync_LogsAttempts()
    {
        // This test would verify debug logging for each attempt
        // Requires actual or mocked OpenAI API responses
    }

    [Fact(Skip = "Requires OpenAI SDK mocking - integration test")]
    public void ValidateNetworkAsync_OnSuccess_LogsInformation()
    {
        // This test would verify information log on successful validation
        // Requires actual or mocked OpenAI API responses
    }

    [Fact(Skip = "Requires OpenAI SDK mocking - integration test")]
    public void ValidateNetworkAsync_OnRetry_LogsWarning()
    {
        // This test would verify warning log on each failed attempt
        // Requires actual or mocked OpenAI API responses
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ValidateFormatAsync_WithVeryLongKey_HandlesGracefully()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var veryLongKey = "sk-" + new string('a', 500); // 500+ chars

        // Act
        var result = await validator.ValidateFormatAsync(veryLongKey);

        // Assert
        // Should still validate as long as it matches the pattern
        result.IsValid.Should().BeTrue();
        result.FormatValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateFormatAsync_IsCaseSensitive()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var upperCaseKey = "SK-123456789012345678901234567890123456789012345678";  // SK- instead of sk-

        // Act
        var result = await validator.ValidateFormatAsync(upperCaseKey);

        // Assert
        // OpenAI keys start with lowercase sk-
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ValidateFormatAsync_CompletesQuickly()
    {
        // Arrange
        var validator = new OpenAIApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var validKey = "sk-123456789012345678901234567890123456789012345678";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await validator.ValidateFormatAsync(validKey);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Format validation should be instant
    }

    #endregion
}
