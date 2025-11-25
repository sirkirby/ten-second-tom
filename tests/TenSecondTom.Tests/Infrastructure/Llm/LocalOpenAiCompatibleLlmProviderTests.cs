using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TenSecondTom.Infrastructure.Llm;

namespace TenSecondTom.Tests.Infrastructure.Llm;

public sealed class LocalOpenAiCompatibleLlmProviderTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<LocalOpenAiCompatibleLlmProvider>> _mockLogger;
    private readonly HttpClient _httpClient;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public LocalOpenAiCompatibleLlmProviderTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger<LocalOpenAiCompatibleLlmProvider>>();

        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithValidResponse_ReturnsContent()
    {
        // Arrange
        var expectedResponse = "{\"choices\": [{\"message\": {\"content\": \"Test response\"}}]}";
        
        var responseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(expectedResponse)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        try
        {
            var provider = new LocalOpenAiCompatibleLlmProvider(
                _mockHttpClientFactory.Object,
                _mockLogger.Object,
                "test-model",
                "http://localhost:8080/v1");

            // Act
            var result = await provider.GenerateCompletionAsync("Test prompt", CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Content.Should().Be("Test response");
        }
        finally
        {
            responseMessage?.Dispose();
        }
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithErrorResponse_ReturnsFailure()
    {
        // Arrange
        var responseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        try
        {
            var provider = new LocalOpenAiCompatibleLlmProvider(
                _mockHttpClientFactory.Object,
                _mockLogger.Object,
                "test-model",
                "http://localhost:8080/v1");

            // Act
            var result = await provider.GenerateCompletionAsync("Test prompt", CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("InternalServerError");
        }
        finally
        {
            responseMessage?.Dispose();
        }
    }
}
