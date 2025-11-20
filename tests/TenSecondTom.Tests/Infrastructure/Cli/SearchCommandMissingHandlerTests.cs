using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Cli;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Cli;

/// <summary>
/// Tests that the search command fails gracefully (without throwing) when the SearchMemories.Handler
/// has not been registered in the service provider. This guards against unhandled exceptions when a
/// minimal or custom service provider is used (e.g., in focused tests or tooling scenarios).
/// </summary>
public sealed class SearchCommandMissingHandlerTests
{
    [Fact]
    public async Task Invoke_SearchCommand_WithoutRegisteredHandler_ShouldNotThrow()
    {
        // Arrange: create a minimal service provider intentionally omitting AddTenSecondTomServices()
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var root = CommandRegistry.BuildRootCommand(provider);

        // Act
        Func<Task> act = async () =>
        {
            // Provide a simple query so action would attempt to resolve handler
            var parseResult = root.Parse("search test");
            await parseResult.InvokeAsync();
        };

        // Assert: Should not throw (graceful no-op / error message instead)
        await act.Should().NotThrowAsync();
    }
}
