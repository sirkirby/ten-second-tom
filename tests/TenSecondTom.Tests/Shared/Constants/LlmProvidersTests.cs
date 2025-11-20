using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Shared.Constants;

public sealed class LlmProvidersTests
{
    [Fact]
    public void LlmProviders_All_AreNotNullOrEmpty()
    {
        LlmProviders.OpenAI.Should().NotBeNullOrWhiteSpace();
        LlmProviders.Anthropic.Should().NotBeNullOrWhiteSpace();
        LlmProviders.All.Should().Contain(new[] { LlmProviders.OpenAI, LlmProviders.Anthropic });
    }
}