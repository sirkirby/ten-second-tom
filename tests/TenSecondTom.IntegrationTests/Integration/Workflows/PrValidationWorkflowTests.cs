using FluentAssertions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenSecondTom.IntegrationTests.Integration.Workflows;

/// <summary>
/// Tests for PR Validation workflow structure and configuration.
/// Validates workflow YAML against contract requirements from specs/002-as-per-the/contracts/pr-validation-workflow.md
/// </summary>
public sealed class PrValidationWorkflowTests
{
    private const string WorkflowPath = "../../../../../.github/workflows/pr-validation.yml";
    private readonly IDeserializer _yamlDeserializer;

    public PrValidationWorkflowTests()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    [Fact]
    public void WorkflowFile_Should_Exist()
    {
        // Arrange & Act
        var workflowFileInfo = new FileInfo(WorkflowPath);

        // Assert
        workflowFileInfo.Exists.Should().BeTrue(
            $"PR validation workflow file should exist at {Path.GetFullPath(WorkflowPath)}");
    }

    [Fact]
    public void WorkflowFile_Should_BeValidYaml()
    {
        // Arrange
        var workflowContent = File.ReadAllText(WorkflowPath);

        // Act
        var parseAction = () => _yamlDeserializer.Deserialize<Dictionary<string, object>>(workflowContent);

        // Assert
        parseAction.Should().NotThrow<Exception>("workflow file should contain valid YAML syntax");
    }

    [Fact]
    public void Workflow_Should_HaveCorrectName()
    {
        // Arrange
        var workflow = LoadWorkflow();

        // Act
        var hasName = workflow.TryGetValue("name", out var nameObj);
        var name = nameObj?.ToString();

        // Assert
        hasName.Should().BeTrue("workflow should have a 'name' property");
        name.Should().Be("PR Validation", "workflow should be named 'PR Validation'");
    }

    [Fact]
    public void Workflow_Should_TriggerOnPullRequest()
    {
        // Arrange
        var workflow = LoadWorkflow();

        // Act
        var hasOn = workflow.TryGetValue("on", out var onObj);
        var triggers = onObj as Dictionary<object, object>;
        var hasPullRequest = triggers?.ContainsKey("pull_request") ?? false;

        // Assert
        hasOn.Should().BeTrue("workflow should have trigger configuration");
        hasPullRequest.Should().BeTrue("workflow should trigger on pull_request events");
    }

    [Fact]
    public void Workflow_Should_TriggerOnMainBranch()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var triggers = workflow["on"] as Dictionary<object, object>;
        var pullRequest = triggers?["pull_request"] as Dictionary<object, object>;

        // Act
        object? branchesObj = null;
        var hasBranches = pullRequest?.TryGetValue("branches", out branchesObj) ?? false;
        var branches = branchesObj as List<object> ?? [];
        var targetsBranches = branches.Select(b => b.ToString()).ToList();

        // Assert
        hasBranches.Should().BeTrue("pull_request trigger should specify target branches");
        targetsBranches.Should().Contain("main", "workflow should trigger on pull requests to main branch");
    }

    [Fact]
    public void Workflow_Should_HaveRequiredJobs()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var requiredJobs = new[] { "BUILD", "TEST", "COVERAGE", "VALIDATE" };

        // Act
        var hasJobs = workflow.TryGetValue("jobs", out var jobsObj);
        var jobs = jobsObj as Dictionary<object, object>;
        var jobNames = jobs?.Keys.Select(k => k.ToString()?.ToUpperInvariant()).ToList() ?? [];

        // Assert
        hasJobs.Should().BeTrue("workflow should have jobs defined");
        jobs.Should().NotBeNull("jobs should be a valid dictionary");
        
        foreach (var requiredJob in requiredJobs)
        {
            jobNames.Should().Contain(requiredJob, $"workflow should have '{requiredJob}' job");
        }
    }

    [Fact]
    public void BuildJob_Should_HaveCorrectConfiguration()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build"] as Dictionary<object, object>;

        // Act & Assert
        buildJob.Should().NotBeNull("build job should exist");
        
        var hasRunsOn = buildJob!.TryGetValue("runs-on", out var runsOnObj);
        var runsOn = runsOnObj?.ToString();
        
        hasRunsOn.Should().BeTrue("build job should specify runner");
        runsOn.Should().Be("ubuntu-latest", "build job should run on ubuntu-latest");
        
        var hasSteps = buildJob.TryGetValue("steps", out var stepsObj);
        var steps = stepsObj as List<object>;
        
        hasSteps.Should().BeTrue("build job should have steps defined");
        steps.Should().NotBeNullOrEmpty("build job should have at least one step");
    }

    [Fact]
    public void TestJob_Should_DependOnBuildJob()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var testJob = jobs?["test"] as Dictionary<object, object>;

        // Act
        object? needsObj = null;
        var hasNeeds = testJob?.TryGetValue("needs", out needsObj) ?? false;
        var needs = needsObj as List<object> ?? (needsObj != null ? [needsObj] : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        hasNeeds.Should().BeTrue("test job should have dependencies defined");
        dependencies.Should().Contain("build", "test job should depend on build job");
    }

    [Fact]
    public void CoverageJob_Should_DependOnBuildJob()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var coverageJob = jobs?["coverage"] as Dictionary<object, object>;

        // Act
        object? needsObj = null;
        var hasNeeds = coverageJob?.TryGetValue("needs", out needsObj) ?? false;
        var needs = needsObj as List<object> ?? (needsObj != null ? [needsObj] : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        hasNeeds.Should().BeTrue("coverage job should have dependencies defined");
        dependencies.Should().Contain("build", "coverage job should depend on build job");
    }

    [Fact]
    public void ValidateJob_Should_DependOnAllJobs()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var validateJob = jobs?["validate"] as Dictionary<object, object>;
        var requiredDependencies = new[] { "build", "test", "coverage" };

        // Act
        object? needsObj = null;
        var hasNeeds = validateJob?.TryGetValue("needs", out needsObj) ?? false;
        var needs = needsObj as List<object> ?? [];
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        hasNeeds.Should().BeTrue("validate job should have dependencies defined");
        
        foreach (var requiredDep in requiredDependencies)
        {
            dependencies.Should().Contain(requiredDep, 
                $"validate job should depend on {requiredDep} job");
        }
    }

    [Fact]
    public void TestJob_Should_RunWithDotnetTest()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var testJob = jobs?["test"] as Dictionary<object, object>;
        var steps = testJob?["steps"] as List<object>;

        // Act
        var hasTestStep = steps?.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            if (stepDict?.TryGetValue("run", out var runObj) ?? false)
            {
                var runCommand = runObj?.ToString() ?? string.Empty;
                return runCommand.Contains("dotnet test", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }) ?? false;

        // Assert
        steps.Should().NotBeNullOrEmpty("test job should have steps");
        hasTestStep.Should().BeTrue("test job should execute 'dotnet test' command");
    }

    [Fact]
    public void CoverageJob_Should_EnforceCoverageThreshold()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var coverageJob = jobs?["coverage"] as Dictionary<object, object>;
        var steps = coverageJob?["steps"] as List<object>;

        // Act
        var hasCoverageCheck = steps?.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            if (stepDict?.TryGetValue("run", out var runObj) ?? false)
            {
                var runCommand = runObj?.ToString() ?? string.Empty;
                // Check for coverage threshold enforcement (80% or 0.8 or similar)
                return runCommand.Contains("80", StringComparison.OrdinalIgnoreCase) || 
                       runCommand.Contains("0.8", StringComparison.OrdinalIgnoreCase) || 
                       (runCommand.Contains("coverage", StringComparison.OrdinalIgnoreCase) && 
                        runCommand.Contains("threshold", StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }) ?? false;

        // Assert
        steps.Should().NotBeNullOrEmpty("coverage job should have steps");
        hasCoverageCheck.Should().BeTrue(
            "coverage job should enforce 80% minimum coverage threshold");
    }

    [Fact]
    public void Workflow_Should_HaveConcurrencyConfiguration()
    {
        // Arrange
        var workflow = LoadWorkflow();

        // Act
        var hasConcurrency = workflow.TryGetValue("concurrency", out var concurrencyObj);

        // Assert
        hasConcurrency.Should().BeTrue(
            "workflow should have concurrency configuration to cancel outdated runs");
    }

    /// <summary>
    /// Helper method to load and parse the workflow YAML file.
    /// </summary>
    private Dictionary<string, object> LoadWorkflow()
    {
        var workflowContent = File.ReadAllText(WorkflowPath);
        var workflow = _yamlDeserializer.Deserialize<Dictionary<string, object>>(workflowContent);
        
        workflow.Should().NotBeNull("workflow file should contain valid YAML");
        
        return workflow!;
    }
}
