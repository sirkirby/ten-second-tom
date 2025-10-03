using FluentAssertions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenSecondTom.IntegrationTests.Integration.Workflows;

/// <summary>
/// Tests for Build workflow structure and configuration.
/// Validates workflow YAML against contract requirements from specs/002-as-per-the/contracts/build-workflow.md
/// </summary>
public sealed class BuildWorkflowTests
{
    private const string WorkflowPath = "../../../../../.github/workflows/build.yml";
    private readonly IDeserializer _yamlDeserializer;

    public BuildWorkflowTests()
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
            $"Build workflow file should exist at {Path.GetFullPath(WorkflowPath)}");
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
        name.Should().Be("Build", "workflow should be named 'Build'");
    }

    [Fact]
    public void Workflow_Should_TriggerOnPushToMain()
    {
        // Arrange
        var workflow = LoadWorkflow();

        // Act
        var hasOn = workflow.TryGetValue("on", out var onObj);
        var triggers = onObj as Dictionary<object, object>;
        var hasPush = triggers?.ContainsKey("push") ?? false;

        // Assert
        hasOn.Should().BeTrue("workflow should have trigger configuration");
        hasPush.Should().BeTrue("workflow should trigger on push events");
    }

    [Fact]
    public void Workflow_Should_TriggerOnlyOnMainBranch()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var triggers = workflow["on"] as Dictionary<object, object>;
        var push = triggers?["push"] as Dictionary<object, object>;

        // Act
        object? branchesObj = null;
        var hasBranches = push?.TryGetValue("branches", out branchesObj) ?? false;
        var branches = branchesObj as List<object> ?? [];
        var targetBranches = branches.Select(b => b.ToString()).ToList();

        // Assert
        hasBranches.Should().BeTrue("push trigger should specify target branches");
        targetBranches.Should().Contain("main", "workflow should trigger only on pushes to main branch");
        targetBranches.Should().HaveCount(1, "workflow should only trigger on main branch");
    }

    [Fact]
    public void Workflow_Should_HaveRequiredJobs()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var hasJobs = workflow.TryGetValue("jobs", out var jobsObj);
        var jobs = jobsObj as Dictionary<object, object>;

        // Act
        var jobNames = jobs?.Keys.Select(k => k.ToString()).ToList() ?? [];

        // Assert
        hasJobs.Should().BeTrue("workflow should have jobs");
        jobNames.Should().Contain("test", "workflow should have a test job");
        jobNames.Should().Contain("build-macos-x64", "workflow should have a macOS x64 build job");
        jobNames.Should().Contain("build-macos-arm64", "workflow should have a macOS ARM64 build job");
        jobNames.Should().Contain("build-windows-x64", "workflow should have a Windows x64 build job");
    }

    [Fact]
    public void TestJob_Should_RunOnUbuntu()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var testJob = jobs?["test"] as Dictionary<object, object>;

        // Act
        var runsOn = testJob?["runs-on"]?.ToString();

        // Assert
        runsOn.Should().Be("ubuntu-latest", "test job should run on ubuntu-latest for performance");
    }

    [Fact]
    public void BuildMacOSX64Job_Should_DependOnTestJob()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-macos-x64"] as Dictionary<object, object>;

        // Act
        var needs = buildJob?["needs"] as List<object> ?? 
                   (buildJob?["needs"] != null ? new List<object> { buildJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        buildJob.Should().NotBeNull("build-macos-x64 job should exist");
        dependencies.Should().Contain("test", "build-macos-x64 job should depend on test job");
    }

    [Fact]
    public void BuildMacOSARM64Job_Should_DependOnTestJob()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-macos-arm64"] as Dictionary<object, object>;

        // Act
        var needs = buildJob?["needs"] as List<object> ?? 
                   (buildJob?["needs"] != null ? new List<object> { buildJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        buildJob.Should().NotBeNull("build-macos-arm64 job should exist");
        dependencies.Should().Contain("test", "build-macos-arm64 job should depend on test job");
    }

    [Fact]
    public void BuildWindowsX64Job_Should_DependOnTestJob()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-windows-x64"] as Dictionary<object, object>;

        // Act
        var needs = buildJob?["needs"] as List<object> ?? 
                   (buildJob?["needs"] != null ? new List<object> { buildJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        buildJob.Should().NotBeNull("build-windows-x64 job should exist");
        dependencies.Should().Contain("test", "build-windows-x64 job should depend on test job");
    }

    [Fact]
    public void BuildMacOSX64Job_Should_RunOnMacOS()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-macos-x64"] as Dictionary<object, object>;

        // Act
        var runsOn = buildJob?["runs-on"]?.ToString();

        // Assert
        runsOn.Should().Be("macos-latest", "macOS x64 build job should run on macos-latest");
    }

    [Fact]
    public void BuildMacOSARM64Job_Should_RunOnMacOS()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-macos-arm64"] as Dictionary<object, object>;

        // Act
        var runsOn = buildJob?["runs-on"]?.ToString();

        // Assert
        runsOn.Should().Be("macos-latest", "macOS ARM64 build job should run on macos-latest");
    }

    [Fact]
    public void BuildWindowsX64Job_Should_RunOnWindows()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-windows-x64"] as Dictionary<object, object>;

        // Act
        var runsOn = buildJob?["runs-on"]?.ToString();

        // Assert
        runsOn.Should().Be("windows-latest", "Windows x64 build job should run on windows-latest");
    }

    [Fact]
    public void BuildJobs_Should_UploadArtifacts()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJobNames = new[] { "build-macos-x64", "build-macos-arm64", "build-windows-x64" };

        foreach (var jobName in buildJobNames)
        {
            var job = jobs?[jobName] as Dictionary<object, object>;
            var steps = job?["steps"] as List<object> ?? [];

            // Act
            var hasUploadStep = steps.Any(step =>
            {
                var stepDict = step as Dictionary<object, object>;
                if (stepDict == null || !stepDict.TryGetValue("uses", out var usesObj))
                    return false;
                var uses = usesObj?.ToString() ?? "";
                return uses.StartsWith("actions/upload-artifact", StringComparison.Ordinal);
            });

            // Assert
            hasUploadStep.Should().BeTrue($"{jobName} should upload artifacts");
        }
    }

    [Fact]
    public void Workflow_Should_HaveSmokeTestJobs()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var hasJobs = workflow.TryGetValue("jobs", out var jobsObj);
        var jobs = jobsObj as Dictionary<object, object>;

        // Act
        var jobNames = jobs?.Keys.Select(k => k.ToString()).ToList() ?? [];

        // Assert
        hasJobs.Should().BeTrue("workflow should have jobs");
        jobNames.Should().Contain("verify-macos-x64", "workflow should have a macOS x64 verification job");
        jobNames.Should().Contain("verify-macos-arm64", "workflow should have a macOS ARM64 verification job");
        jobNames.Should().Contain("verify-windows-x64", "workflow should have a Windows x64 verification job");
    }

    [Fact]
    public void VerifyMacOSX64Job_Should_DependOnBuildMacOSX64Job()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var verifyJob = jobs?["verify-macos-x64"] as Dictionary<object, object>;

        // Act
        var needs = verifyJob?["needs"] as List<object> ?? 
                   (verifyJob?["needs"] != null ? new List<object> { verifyJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        verifyJob.Should().NotBeNull("verify-macos-x64 job should exist");
        dependencies.Should().Contain("build-macos-x64", "verify-macos-x64 job should depend on build-macos-x64 job");
    }

    [Fact]
    public void VerifyMacOSARM64Job_Should_DependOnBuildMacOSARM64Job()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var verifyJob = jobs?["verify-macos-arm64"] as Dictionary<object, object>;

        // Act
        var needs = verifyJob?["needs"] as List<object> ?? 
                   (verifyJob?["needs"] != null ? new List<object> { verifyJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        verifyJob.Should().NotBeNull("verify-macos-arm64 job should exist");
        dependencies.Should().Contain("build-macos-arm64", "verify-macos-arm64 job should depend on build-macos-arm64 job");
    }

    [Fact]
    public void VerifyWindowsX64Job_Should_DependOnBuildWindowsX64Job()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var verifyJob = jobs?["verify-windows-x64"] as Dictionary<object, object>;

        // Act
        var needs = verifyJob?["needs"] as List<object> ?? 
                   (verifyJob?["needs"] != null ? new List<object> { verifyJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        verifyJob.Should().NotBeNull("verify-windows-x64 job should exist");
        dependencies.Should().Contain("build-windows-x64", "verify-windows-x64 job should depend on build-windows-x64 job");
    }

    [Fact]
    public void VerifyJobs_Should_DownloadArtifacts()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var verifyJobNames = new[] { "verify-macos-x64", "verify-macos-arm64", "verify-windows-x64" };

        foreach (var jobName in verifyJobNames)
        {
            var job = jobs?[jobName] as Dictionary<object, object>;
            var steps = job?["steps"] as List<object> ?? [];

            // Act
            var hasDownloadStep = steps.Any(step =>
            {
                var stepDict = step as Dictionary<object, object>;
                var uses = stepDict?["uses"]?.ToString() ?? "";
                return uses.StartsWith("actions/download-artifact", StringComparison.Ordinal);
            });

            // Assert
            hasDownloadStep.Should().BeTrue($"{jobName} should download artifacts");
        }
    }

    [Fact]
    public void Workflow_Should_HaveConcurrencyConfiguration()
    {
        // Arrange
        var workflow = LoadWorkflow();

        // Act
        var hasConcurrency = workflow.TryGetValue("concurrency", out var concurrencyObj);
        var concurrency = concurrencyObj as Dictionary<object, object>;

        // Assert
        hasConcurrency.Should().BeTrue("workflow should have concurrency configuration");
        concurrency.Should().NotBeNull("concurrency configuration should not be null");
        concurrency.Should().ContainKey("group", "concurrency should define a group");
    }

    [Fact]
    public void TestJob_Should_RunDotnetTest()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var testJob = jobs?["test"] as Dictionary<object, object>;
        var steps = testJob?["steps"] as List<object> ?? [];

        // Act
        var hasTestStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            if (stepDict == null || !stepDict.TryGetValue("run", out var runObj))
                return false;
            var run = runObj?.ToString() ?? "";
            return run.Contains("dotnet test", StringComparison.Ordinal);
        });

        // Assert
        hasTestStep.Should().BeTrue("test job should run dotnet test command");
    }

    [Fact]
    public void BuildJobs_Should_UseDotnetPublish()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJobNames = new[] { "build-macos-x64", "build-macos-arm64", "build-windows-x64" };

        foreach (var jobName in buildJobNames)
        {
            var job = jobs?[jobName] as Dictionary<object, object>;
            var steps = job?["steps"] as List<object> ?? [];

            // Act
            var hasPublishStep = steps.Any(step =>
            {
                var stepDict = step as Dictionary<object, object>;
                if (stepDict == null || !stepDict.TryGetValue("run", out var runObj))
                    return false;
                var run = runObj?.ToString() ?? "";
                return run.Contains("dotnet publish", StringComparison.Ordinal);
            });

            // Assert
            hasPublishStep.Should().BeTrue($"{jobName} should use dotnet publish command");
        }
    }

    [Fact]
    public void BuildJobs_Should_VerifyArtifactSize()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJobNames = new[] { "build-macos-x64", "build-macos-arm64", "build-windows-x64" };

        foreach (var jobName in buildJobNames)
        {
            var job = jobs?[jobName] as Dictionary<object, object>;
            var steps = job?["steps"] as List<object> ?? [];

            // Act
            var hasSizeCheckStep = steps.Any(step =>
            {
                var stepDict = step as Dictionary<object, object>;
                if (stepDict == null)
                    return false;
                
                stepDict.TryGetValue("name", out var nameObj);
                stepDict.TryGetValue("run", out var runObj);
                
                var name = nameObj?.ToString() ?? "";
                var run = runObj?.ToString() ?? "";
                
                return name.Contains("size", StringComparison.OrdinalIgnoreCase) || 
                       run.Contains("50MB", StringComparison.Ordinal) || 
                       run.Contains("52428800", StringComparison.Ordinal); // 50MB in bytes
            });

            // Assert
            hasSizeCheckStep.Should().BeTrue($"{jobName} should verify artifact size is less than 50MB");
        }
    }

    /// <summary>
    /// Helper method to load and parse workflow YAML.
    /// </summary>
    private Dictionary<string, object> LoadWorkflow()
    {
        var workflowContent = File.ReadAllText(WorkflowPath);
        return _yamlDeserializer.Deserialize<Dictionary<string, object>>(workflowContent);
    }
}
