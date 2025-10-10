using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.SharedConstants;

public sealed class ConfigurationKeysTests
{
    [Fact]
    public void ConfigurationKeys_All_AreNotNullOrEmpty()
    {
        ConfigurationKeys.OpenAIApiKey.Should().NotBeNullOrWhiteSpace();
        ConfigurationKeys.AnthropicApiKey.Should().NotBeNullOrWhiteSpace();
        ConfigurationKeys.DotNetEnvironment.Should().NotBeNullOrWhiteSpace();
        ConfigurationKeys.LlmProvider.Should().NotBeNullOrWhiteSpace();
    }
}