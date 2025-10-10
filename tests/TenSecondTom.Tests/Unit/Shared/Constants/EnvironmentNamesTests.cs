using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.SharedConstants;

public sealed class EnvironmentNamesTests
{
    [Fact]
    public void EnvironmentNames_All_AreNotNullOrEmpty()
    {
        EnvironmentNames.Development.Should().NotBeNullOrWhiteSpace();
        EnvironmentNames.Production.Should().NotBeNullOrWhiteSpace();
        EnvironmentNames.Staging.Should().NotBeNullOrWhiteSpace();
        EnvironmentNames.All.Should().Contain(new[] { EnvironmentNames.Development, EnvironmentNames.Production, EnvironmentNames.Staging });
    }
}