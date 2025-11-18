using FluentAssertions;
using TenSecondTom.Features.Generate;
using Xunit.Abstractions;

namespace TenSecondTom.IntegrationTests.Features.Generate;

/// <summary>
/// Integration tests for the generate command with --template argument support.
/// Tests Phase 4: User Story 2 - One-Shot Command Execution (non-interactive automation).
/// These tests will fail until T041-T045 are implemented.
/// </summary>
public sealed class GenerateCommandIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public GenerateCommandIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Smoke test to verify GenerateCommand.ExecuteAsync has the updated signature with templateName parameter.
    /// After T041 implementation, this test verifies the new signature.
    /// </summary>
    [Fact]
    public async Task GenerateCommand_ExecuteAsync_HasTemplateNameParameter()
    {
        // Arrange & Act
        // Verify the method exists with the NEW signature (after T041)
        var methodInfo = typeof(GenerateCommand).GetMethod(
            "ExecuteAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        // Assert
        methodInfo.Should().NotBeNull("GenerateCommand.ExecuteAsync should exist");

        var parameters = methodInfo!.GetParameters();
        parameters.Should().HaveCount(3, "new signature has 3 parameters: serviceProvider, jsonOutput, templateName");
        parameters[0].Name.Should().Be("serviceProvider");
        parameters[1].Name.Should().Be("jsonOutput");
        parameters[2].Name.Should().Be("templateName", "T041 adds templateName parameter");
        parameters[2].IsOptional.Should().BeTrue("templateName should be optional");
        parameters[2].DefaultValue.Should().BeNull("templateName default should be null");

        _output.WriteLine("✓ Updated signature verified: ExecuteAsync(IServiceProvider, bool, string? = null)");
        _output.WriteLine("✓ T041 implementation confirmed");

        await Task.CompletedTask;
    }
}
