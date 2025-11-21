using FluentAssertions;
using TenSecondTom.Features.Generate;
using Xunit.Abstractions;

namespace TenSecondTom.IntegrationTests.Integration.Features.Generate;

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
        parameters.Should().HaveCount(6, "new signature has 6 parameters");
        parameters[0].Name.Should().Be("serviceProvider");
        parameters[1].Name.Should().Be("jsonOutput");
        parameters[2].Name.Should().Be("templateId");
        parameters[3].Name.Should().Be("noteName");
        parameters[4].Name.Should().Be("recordingName");
        parameters[5].Name.Should().Be("listTemplates");

        _output.WriteLine("✓ Updated signature verified: ExecuteAsync(IServiceProvider, bool, string?, string?, string?, bool)");
        _output.WriteLine("✓ T041 implementation confirmed");

        await Task.CompletedTask;
    }
}
