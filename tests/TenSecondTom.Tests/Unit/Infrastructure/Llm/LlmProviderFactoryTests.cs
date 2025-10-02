using Anthropic.SDK;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Llm;

/// <summary>
/// Tests for LLM provider factory.
/// </summary>
public sealed class LlmProviderFactoryTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AnthropicClient _anthropicClient;

    public LlmProviderFactoryTests()
    {
        var services = new ServiceCollection();
        
        // Register mock OpenAI provider
        var mockOpenAILogger = new Mock<ILogger<OpenAILlmProvider>>();
        var mockChatClient = new Mock<OpenAI.Chat.ChatClient>();
        services.AddTransient<OpenAILlmProvider>(_ => 
            new OpenAILlmProvider(
                mockChatClient.Object,
                mockOpenAILogger.Object,
                "gpt-4"));
        
        // Register mock Anthropic provider
        // Note: AnthropicClient is sealed, so we create a real instance with a dummy API key for testing
        var mockAnthropicLogger = new Mock<ILogger<AnthropicLlmProvider>>();
        _anthropicClient = new AnthropicClient("test-api-key-for-factory-tests");
        services.AddTransient<AnthropicLlmProvider>(_ => 
            new AnthropicLlmProvider(
                _anthropicClient,
                mockAnthropicLogger.Object,
                "claude-3-sonnet-20240229"));
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _anthropicClient?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public void Create_WithOpenAI_ReturnsOpenAIProvider()
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create("OpenAI");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<OpenAILlmProvider>();
        result.Value.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public void Create_WithAnthropic_ReturnsAnthropicProvider()
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create("Anthropic");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<AnthropicLlmProvider>();
        result.Value.ProviderName.Should().Be("Anthropic");
    }

    [Fact]
    public void Create_WithInvalidProvider_ReturnsFailure()
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create("InvalidProvider");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unsupported LLM provider");
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("OPENAI")]
    [InlineData("OpEnAi")]
    public void Create_WithCaseInsensitiveOpenAI_ReturnsOpenAIProvider(string providerName)
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create(providerName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<OpenAILlmProvider>();
    }

    [Theory]
    [InlineData("anthropic")]
    [InlineData("ANTHROPIC")]
    [InlineData("AnThRoPiC")]
    public void Create_WithCaseInsensitiveAnthropic_ReturnsAnthropicProvider(string providerName)
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create(providerName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<AnthropicLlmProvider>();
    }

    [Fact]
    public void Create_WithNullProvider_ReturnsFailure()
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Provider name cannot be empty");
    }

    [Fact]
    public void Create_WithEmptyProvider_ReturnsFailure()
    {
        // Arrange
        var factory = new LlmProviderFactory(_serviceProvider);

        // Act
        Result<ILlmProvider> result = factory.Create(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Provider name cannot be empty");
    }
}
