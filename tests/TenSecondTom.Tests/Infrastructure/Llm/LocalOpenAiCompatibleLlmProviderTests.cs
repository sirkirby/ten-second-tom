using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TenSecondTom.Infrastructure.Llm;

namespace TenSecondTom.Tests.Infrastructure.Llm;

public class LocalOpenAiCompatibleLlmProviderTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<LocalOpenAiCompatibleLlmProvider>> _mockLogger;
    private readonly HttpClient _httpClient;

    public LocalOpenAiCompatibleLlmProviderTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger<LocalOpenAiCompatibleLlmProvider>>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithValidResponse_ReturnsContent()
    {
        // Arrange
        var expectedResponse = "{\"choices\": [{\"message\": {\"content\": \"Test response\"}}]}";
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedResponse)
            });

        var provider = new LocalOpenAiCompatibleLlmProvider(
            _httpClient,
            _mockLogger.Object,
            "test-model",
            "http://localhost:8080/v1");

        // Act
        var result = await provider.GenerateCompletionAsync("Test prompt", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Test response");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithErrorResponse_ReturnsFailure()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var provider = new LocalOpenAiCompatibleLlmProvider(
            _httpClient,
            _mockLogger.Object,
            "test-model",
            "http://localhost:8080/v1");

        // Act
        var result = await provider.GenerateCompletionAsync("Test prompt", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("InternalServerError");
    }
}
