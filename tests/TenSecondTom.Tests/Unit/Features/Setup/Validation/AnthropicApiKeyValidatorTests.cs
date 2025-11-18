using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using Xunit;
using TenSecondTom.Features.Setup;

namespace TenSecondTom.Tests.Unit.Features.Setup.Validation;

/// <summary>
/// Unit tests for <see cref="AnthropicApiKeyValidator"/>
/// Tests API key format validation and network validation structure
/// </summary>
public sealed class AnthropicApiKeyValidatorTests
{
    private readonly Mock<ILogger<AnthropicApiKeyValidator>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public AnthropicApiKeyValidatorTests()
    {
        _mockLogger = new Mock<ILogger<AnthropicApiKeyValidator>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AnthropicApiKeyValidator(null!, _mockHttpClientFactory.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AnthropicApiKeyValidator(_mockLogger.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Provider_ReturnsAnthropic()
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Assert
        validator.Provider.Should().Be(LlmProvider.Anthropic);
    }

    #endregion

    #region Format Validation Tests - Empty/Null Keys

    [Fact]
    public async Task ValidateFormatAsync_WithNullKey_ReturnsFormatFailure()
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

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
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

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
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

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
    [InlineData("sk-ant-")]
    [InlineData("sk-ant-abc")]
    [InlineData("sk-ant-12345678901234567890123456789")]  // Too short (31 chars)
    [InlineData("sk-123456789012345678901234567890123456789012345678")]  // OpenAI format
    [InlineData("invalid-key")]
    [InlineData("ant-12345678901234567890123456789012")]  // Missing sk- prefix
    [InlineData("sk-api-12345678901234567890123456789012")]  // Wrong prefix (sk-api instead of sk-ant)
    [InlineData("not-an-api-key")]
    public async Task ValidateFormatAsync_WithInvalidFormat_ReturnsFormatFailure(string invalidKey)
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(invalidKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
        result.NetworkValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid Anthropic API key format");
        result.ErrorMessage.Should().Contain("sk-ant-");
    }

    [Theory]
    [InlineData("sk-ant-with!special@chars#12345678901234567890")]
    [InlineData("sk-ant-with spaces 12345678901234567890")]
    public async Task ValidateFormatAsync_WithInvalidCharacters_ReturnsFormatFailure(string invalidKey)
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(invalidKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
    }

    #endregion

    #region Format Validation Tests - Valid Formats

    [Theory]
    [InlineData("sk-ant-12345678901234567890123456789012")]  // Exactly 32 chars
    [InlineData("sk-ant-123456789012345678901234567890123")]  // 33 chars
    [InlineData("sk-ant-api03-abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]  // Mixed case + numbers
    [InlineData("sk-ant-abcd-1234-EFGH-5678-ijkl-9012-mnop")]  // With hyphens (38 chars after prefix)
    [InlineData("sk-ant-ABCDEFGHIJKLMNOPQRSTUVWXYZ12345678901234567890abcdefghijklmnopqrstuvwxyz")]  // Long key
    [InlineData("sk-ant-with_underscores_123456789012345")]  // With underscores - allowed
    [InlineData("sk-ant-api03-XyZ_AbC123DeF_GhI456JkL_MnO789PqR_StU012VwX_YzA345BcD_EfG678HiJ_KlM901NoPqRsTuVwXyZaBcDeFgHiJkLmNoPqRsTuVwXyZ")]  // Realistic format with underscores (120 chars) - SYNTHETIC
    public async Task ValidateFormatAsync_WithValidFormat_ReturnsFormatSuccess(string validKey)
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

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
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

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
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        await validator.ValidateFormatAsync("sk-ant-12345678901234567890123456789012");

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
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var validKey = "sk-ant-12345678901234567890123456789012";
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
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var veryLongKey = "sk-ant-" + new string('a', 500); // 500+ chars after prefix

        // Act
        var result = await validator.ValidateFormatAsync(veryLongKey);

        // Assert
        // Should still validate as long as it matches the pattern (32+ chars)
        result.IsValid.Should().BeTrue();
        result.FormatValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateFormatAsync_IsCaseSensitive()
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var upperCasePrefix = "SK-ANT-12345678901234567890123456789012";  // SK-ANT instead of sk-ant

        // Act
        var result = await validator.ValidateFormatAsync(upperCasePrefix);

        // Assert
        // Anthropic keys start with lowercase sk-ant-
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateFormatAsync_AllowsHyphensInKey()
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var keyWithHyphens = "sk-ant-1234-5678-9012-3456-7890-1234-5678";  // 39 chars after prefix

        // Act
        var result = await validator.ValidateFormatAsync(keyWithHyphens);

        // Assert
        result.IsValid.Should().BeTrue();
        result.FormatValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("sk-ant")]  // Missing hyphen before key
    [InlineData("skant-12345678901234567890123456789012")]  // Missing hyphen in prefix
    public async Task ValidateFormatAsync_RequiresCorrectHyphenation(string invalidKey)
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await validator.ValidateFormatAsync(invalidKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ValidateFormatAsync_CompletesQuickly()
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var validKey = "sk-ant-12345678901234567890123456789012";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await validator.ValidateFormatAsync(validKey);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Format validation should be instant
    }

    #endregion

    #region Comparison with OpenAI Format

    [Fact]
    public async Task ValidateFormatAsync_RejectsOpenAIKeys()
    {
        // Arrange
        var validator = new AnthropicApiKeyValidator(_mockLogger.Object, _mockHttpClientFactory.Object);
        var openAiKey = "sk-123456789012345678901234567890123456789012345678";  // Valid OpenAI format

        // Act
        var result = await validator.ValidateFormatAsync(openAiKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.FormatValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Anthropic");
    }

    #endregion
}
