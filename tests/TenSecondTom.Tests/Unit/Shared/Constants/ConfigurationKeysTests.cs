using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.SharedConstants;

public sealed class ConfigurationKeysTests
{
    [Fact]
    public void ConfigurationKeys_All_AreNotNullOrEmpty()
    {
        ConfigurationKeys.LlmApiKeyKey.Should().NotBeNullOrWhiteSpace();
        ConfigurationKeys.LlmProviderKey.Should().NotBeNullOrWhiteSpace();
        ConfigurationKeys.LlmModelKey.Should().NotBeNullOrWhiteSpace();
        ConfigurationKeys.DotNetEnvironment.Should().NotBeNullOrWhiteSpace();
    }
}