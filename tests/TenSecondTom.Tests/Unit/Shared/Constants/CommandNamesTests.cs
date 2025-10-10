using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.SharedConstants;

public sealed class CommandNamesTests
{
    [Fact]
    public void CommandNames_AllConstants_AreNotNullOrEmpty()
    {
        CommandNames.Today.Should().NotBeNullOrWhiteSpace();
        CommandNames.ThisWeek.Should().NotBeNullOrWhiteSpace();
        CommandNames.Search.Should().NotBeNullOrWhiteSpace();
        CommandNames.Login.Should().NotBeNullOrWhiteSpace();
        CommandNames.Logout.Should().NotBeNullOrWhiteSpace();
        CommandNames.All.Should().Contain(new[] { CommandNames.Today, CommandNames.ThisWeek, CommandNames.Search, CommandNames.Login, CommandNames.Logout });
    }

    [Fact]
    public void CommandNames_Values_AreLowerCase()
    {
        CommandNames.Today.Should().Be("today");
        CommandNames.ThisWeek.Should().Be("thisweek");
        CommandNames.Search.Should().Be("search");
        CommandNames.Login.Should().Be("login");
        CommandNames.Logout.Should().Be("logout");
    }
}