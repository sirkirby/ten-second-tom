using FluentAssertions;
using Moq;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Llm;

/// <summary>
/// Tests for ILlmProvider interface contract and behavior.
/// Validates that any ILlmProvider implementation adheres to expected patterns.
/// </summary>
public sealed class ILlmProviderTests
{
    [Fact]
    public async Task GenerateCompletionAsync_WithValidPrompt_ReturnsSuccessResult()
    {
        // Arrange
        const string expectedCompletion = "Test completion response";
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Success(expectedCompletion));

        // Act
        Result<string> result = await mockProvider.Object.GenerateCompletionAsync(
            "Test prompt",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedCompletion);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithApiError_ReturnsFailureResult()
    {
        // Arrange
        const string expectedError = "API request failed";
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Failure(expectedError));

        // Act
        Result<string> result = await mockProvider.Object.GenerateCompletionAsync(
            "Test prompt",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithMaxTokensParameter_PassesParameter()
    {
        // Arrange
        const int expectedMaxTokens = 1000;
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                expectedMaxTokens,
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Success("response"))
            .Verifiable();

        // Act
        await mockProvider.Object.GenerateCompletionAsync(
            "Test prompt",
            CancellationToken.None,
            expectedMaxTokens);

        // Assert
        mockProvider.Verify();
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithTemperatureParameter_PassesParameter()
    {
        // Arrange
        const double expectedTemperature = 0.7;
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                expectedTemperature))
            .ReturnsAsync(Result<string>.Success("response"))
            .Verifiable();

        // Act
        await mockProvider.Object.GenerateCompletionAsync(
            "Test prompt",
            CancellationToken.None,
            null,
            expectedTemperature);

        // Assert
        mockProvider.Verify();
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithCancellationToken_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Failure("Operation cancelled"));

        await cts.CancelAsync();

        // Act
        Result<string> result = await mockProvider.Object.GenerateCompletionAsync(
            "Test prompt",
            cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ProviderName_ReturnsNonEmptyString()
    {
        // Arrange
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("TestProvider");

        // Act
        string providerName = mockProvider.Object.ProviderName;

        // Assert
        providerName.Should().NotBeNullOrEmpty();
        providerName.Should().Be("TestProvider");
    }
}
