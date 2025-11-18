using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using Xunit;
using TenSecondTom.Features.Setup;

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
    [InlineData("sk-12345678901234567")]  // Too short (19 chars, need 20+)
    [InlineData("invalid-key")]
    [InlineData("not-an-api-key")]
    [InlineData("sk-with-special-chars!@#$%^&*()1234567890")]  // Invalid special chars
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
        result.ErrorMessage.Should().Contain("sk-[alphanumeric]");
    }

    [Theory]
    [InlineData("sk-with-special-chars!@#$%^&*()1234567890")]  // Special chars not allowed
    [InlineData("sk-with spaces 123456789012345678901234567890")]  // Spaces not allowed
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
    [InlineData("sk-12345678901234567890")]  // Exactly 20 chars after sk- (legacy format)
    [InlineData("sk-123456789012345678901234567890123456789012345678")]  // 48 chars (legacy)
    [InlineData("sk-1234567890123456789012345678901234567890123456789")]  // 49 chars (legacy)
    [InlineData("sk-abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]  // Mixed case + numbers
    [InlineData("sk-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz012345678901234567890")]  // 80+ chars
    [InlineData("sk-proj-1234567890123456789012345678901234567890")]  // Project key format
    [InlineData("sk-proj-abc_def-ghi_jkl-1234567890")]  // Project key with underscores and hyphens
    [InlineData("sk-proj-abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ-extra123")]  // Long project key (realistic 161 chars)
    [InlineData("sk-svcacct-1234567890123456789012345678901234567890")]  // Service account key
    [InlineData("sk-svcacct-abc_def-ghi_jkl-1234567890")]  // Service account with underscores and hyphens
    [InlineData("sk-svcacct-zb4_XyZ123AbCdEf-GhIjKlMnOp_QrStUvWxYz0123456789_ABCDEFGHIJKLMNOP_qrstuvwxyz-ABCD_efgh_ijkl_mnop_qrst_uvwxyz_0123456789_ABCDEF-xyz")]  // Realistic service account key format (151 chars) - SYNTHETIC
    [InlineData("sk-with-dashes-and_underscores_123456")]  // Legacy format with hyphens/underscores
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
