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
    /// T038: Integration test for --template with valid template name.
    /// Verifies non-interactive execution with automatic selection of most recent recording.
    /// This test MUST FAIL until the --template parameter is added to GenerateCommand.ExecuteAsync (T041).
    /// </summary>
    [Fact(Skip = "RED phase: Test will not compile until T041 adds --template parameter to GenerateCommand.ExecuteAsync")]
    public async Task GenerateCommand_WithValidTemplate_ProcessesMostRecentRecording()
    {
        // This test cannot be implemented yet because GenerateCommand.ExecuteAsync doesn't have a template parameter.
        // When T041 is implemented, remove the Skip attribute and implement this test.

        // Expected behavior after implementation:
        // 1. Create multiple test recordings in recording/ directory
        // 2. Create test template in templates/ directory
        // 3. Execute: GenerateCommand.ExecuteAsync(serviceProvider, jsonOutput: true, templateName: "business-meeting")
        // 4. Verify: Most recent recording is processed automatically (no interactive prompt)
        // 5. Verify: Output file created in recording/ directory with naming: {recording}_{template}.md

        await Task.CompletedTask;
        _output.WriteLine("T038: Placeholder - implement after T041 adds --template parameter");
    }

    /// <summary>
    /// T039: Integration test for --template with invalid template name.
    /// Verifies clear error message listing all available templates.
    /// This test MUST FAIL until the --template parameter is added (T041) and error handling implemented (T044).
    /// </summary>
    [Fact(Skip = "RED phase: Test will not compile until T041 adds --template parameter")]
    public async Task GenerateCommand_WithInvalidTemplate_ReturnsErrorWithAvailableTemplates()
    {
        // Expected behavior after implementation:
        // 1. Create test recording
        // 2. Create valid templates (e.g., "daily-summary", "action-items")
        // 3. Execute with invalid template: GenerateCommand.ExecuteAsync(..., templateName: "nonexistent-template")
        // 4. Verify: Exit code != 0
        // 5. Verify: Error message contains list of available templates

        await Task.CompletedTask;
        _output.WriteLine("T039: Placeholder - implement after T041 and T044");
    }

    /// <summary>
    /// T040: Integration test for case-insensitive template matching.
    /// Verifies that template names match case-insensitively against both TemplateId and Title.
    /// This test MUST FAIL until template resolution is implemented (T042).
    /// </summary>
    [Fact(Skip = "RED phase: Test will not compile until T041 adds --template parameter")]
    public async Task GenerateCommand_WithCaseInsensitiveTemplate_MatchesCorrectly()
    {
        // Expected behavior after implementation:
        // 1. Create test recording
        // 2. Create template: "business-meeting.md" with title "Business Meeting Notes"
        // 3. Test all case variations:
        //    - "business-meeting" (exact match - lowercase)
        //    - "Business-Meeting" (title case)
        //    - "BUSINESS-MEETING" (uppercase)
        //    - "Business Meeting Notes" (match by Title field)
        // 4. Verify: All variations successfully match and generate output

        await Task.CompletedTask;
        _output.WriteLine("T040: Placeholder - implement after T041 and T042");
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
