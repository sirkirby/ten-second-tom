using FluentAssertions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenSecondTom.IntegrationTests.Integration.Workflows;

/// <summary>
/// Tests for Release workflow structure and configuration.
/// Validates workflow YAML against contract requirements from specs/002-as-per-the/contracts/release-workflow.md
/// </summary>
public sealed class ReleaseWorkflowTests
{
    private const string WorkflowPath = "../../../../../.github/workflows/release.yml";
    private readonly IDeserializer _yamlDeserializer;

    public ReleaseWorkflowTests()
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
            $"Release workflow file should exist at {Path.GetFullPath(WorkflowPath)}");
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
        name.Should().Be("Release", "workflow should be named 'Release'");
    }

    [Fact]
    public void Workflow_Should_TriggerOnTagPush()
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
    public void Workflow_Should_TriggerOnSemanticVersionTags()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var triggers = workflow["on"] as Dictionary<object, object>;
        var push = triggers?["push"] as Dictionary<object, object>;

        // Act
        object? tagsObj = null;
        var hasTags = push?.TryGetValue("tags", out tagsObj) ?? false;
        var tags = tagsObj as List<object> ?? [];
        var tagPatterns = tags.Select(t => t.ToString()).ToList();

        // Assert
        hasTags.Should().BeTrue("push trigger should specify tag patterns");
        tagPatterns.Should().Contain("v*.*.*", "workflow should trigger on semantic version tags (v*.*.*)");
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
        jobNames.Should().Contain("validate-version", "workflow should have a version validation job");
        jobNames.Should().Contain("download-artifacts", "workflow should have a download artifacts job");
        jobNames.Should().Contain("create-github-release", "workflow should have a GitHub release creation job");
        jobNames.Should().Contain("publish-homebrew", "workflow should have a Homebrew publication job");
    }

    [Fact]
    public void ValidateVersionJob_Should_RunOnUbuntu()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var validateJob = jobs?["validate-version"] as Dictionary<object, object>;

        // Act
        var runsOn = validateJob?["runs-on"]?.ToString();

        // Assert
        runsOn.Should().Be("ubuntu-latest", "validate-version job should run on ubuntu-latest");
    }

    [Fact]
    public void ValidateVersionJob_Should_HaveVersionOutput()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var validateJob = jobs?["validate-version"] as Dictionary<object, object>;

        // Act
        object? outputsObj = null;
        var hasOutputs = validateJob?.TryGetValue("outputs", out outputsObj) ?? false;
        var outputs = outputsObj as Dictionary<object, object>;
        var hasVersionOutput = outputs?.ContainsKey("version") ?? false;

        // Assert
        hasOutputs.Should().BeTrue("validate-version job should have outputs");
        hasVersionOutput.Should().BeTrue("validate-version job should output the validated version");
    }

    [Fact]
    public void ValidateVersionJob_Should_ExtractVersionFromTag()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var validateJob = jobs?["validate-version"] as Dictionary<object, object>;
        var steps = validateJob?["steps"] as List<object> ?? [];

        // Act
        var hasExtractStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?["name"]?.ToString() ?? "";
            return name.Contains("Extract version", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasExtractStep.Should().BeTrue("validate-version job should have a step to extract version from tag");
    }

    [Fact]
    public void ValidateVersionJob_Should_ValidateSemanticVersion()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var validateJob = jobs?["validate-version"] as Dictionary<object, object>;
        var steps = validateJob?["steps"] as List<object> ?? [];

        // Act
        var hasValidateStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?["name"]?.ToString() ?? "";
            return name.Contains("Validate", StringComparison.OrdinalIgnoreCase) &&
                   name.Contains("semantic", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasValidateStep.Should().BeTrue("validate-version job should validate semantic version format");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_DependOnValidateVersion()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;

        // Act
        var needs = buildJob?["needs"] as List<object> ??
                   (buildJob?["needs"] != null ? new List<object> { buildJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        buildJob.Should().NotBeNull("build-release-artifacts job should exist");
        dependencies.Should().Contain("validate-version", "build-release-artifacts job should depend on validate-version job");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_UseMatrixForPlatforms()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;

        // Act
        object? strategyObj = null;
        var hasStrategy = buildJob?.TryGetValue("strategy", out strategyObj) ?? false;
        var strategy = strategyObj as Dictionary<object, object>;
        var hasMatrix = strategy?.ContainsKey("matrix") ?? false;

        // Assert
        hasStrategy.Should().BeTrue("build-release-artifacts job should have a strategy");
        hasMatrix.Should().BeTrue("build-release-artifacts job should use matrix strategy for multiple platforms");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_BuildAllPlatforms()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;
        var strategy = buildJob?["strategy"] as Dictionary<object, object>;
        var matrix = strategy?["matrix"] as Dictionary<object, object>;

        // Act
        object? platformsObj = null;
        var hasPlatforms = matrix?.TryGetValue("platform", out platformsObj) ?? false;
        var platforms = platformsObj as List<object> ?? [];
        var platformList = platforms.Select(p =>
        {
            var platformDict = p as Dictionary<object, object>;
            return platformDict?["rid"]?.ToString() ?? p.ToString();
        }).ToList();

        // Assert
        hasPlatforms.Should().BeTrue("build matrix should include platforms");
        platformList.Should().Contain(rid => rid != null && rid.Contains("osx-x64"), "should build macOS x64");
        platformList.Should().Contain(rid => rid != null && rid.Contains("osx-arm64"), "should build macOS ARM64");
        platformList.Should().Contain(rid => rid != null && rid.Contains("win-x64"), "should build Windows x64");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_CalculateChecksums()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;
        var steps = buildJob?["steps"] as List<object> ?? [];

        // Act
        var hasChecksumStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?["name"]?.ToString() ?? "";
            var run = stepDict?["run"]?.ToString() ?? "";
            return name.Contains("checksum", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("sha256sum", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("shasum", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasChecksumStep.Should().BeTrue("build-release-artifacts job should calculate SHA256 checksums");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_VerifySizeLimit()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;
        var steps = buildJob?["steps"] as List<object> ?? [];

        // Act
        var hasSizeCheckStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?["name"]?.ToString() ?? "";
            var run = stepDict?["run"]?.ToString() ?? "";
            return name.Contains("size", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("50", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasSizeCheckStep.Should().BeTrue("build-release-artifacts job should verify executable size is <50MB");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_RunSmokeTests()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;
        var steps = buildJob?["steps"] as List<object> ?? [];

        // Act
        var hasSmokeTestStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?["name"]?.ToString() ?? "";
            var run = stepDict?["run"]?.ToString() ?? "";
            return name.Contains("smoke", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("--version", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasSmokeTestStep.Should().BeTrue("build-release-artifacts job should run smoke tests on executables");
    }

    [Fact]
    public void CreateGitHubReleaseJob_Should_DependOnDownloadArtifacts()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var releaseJob = jobs?["create-github-release"] as Dictionary<object, object>;

        // Act
        var needs = releaseJob?["needs"] as List<object> ??
                   (releaseJob?["needs"] != null ? new List<object> { releaseJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        releaseJob.Should().NotBeNull("create-github-release job should exist");
        dependencies.Should().Contain("download-artifacts", "create-github-release job should depend on download-artifacts job");
    }

    [Fact]
    public void CreateGitHubReleaseJob_Should_DownloadArtifacts()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var releaseJob = jobs?["create-github-release"] as Dictionary<object, object>;
        var steps = releaseJob?["steps"] as List<object> ?? [];

        // Act
        var hasDownloadStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var uses = stepDict?["uses"]?.ToString() ?? "";
            return uses.Contains("actions/download-artifact", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasDownloadStep.Should().BeTrue("create-github-release job should download build artifacts");
    }

    [Fact]
    public void CreateGitHubReleaseJob_Should_GenerateReleaseNotes()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var releaseJob = jobs?["create-github-release"] as Dictionary<object, object>;
        var steps = releaseJob?["steps"] as List<object> ?? [];

        // Act
        var hasReleaseNotesStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?.TryGetValue("name", out var nameObj) == true ? nameObj?.ToString() ?? "" : "";
            var run = stepDict?.TryGetValue("run", out var runObj) == true ? runObj?.ToString() ?? "" : "";
            return name.Contains("release notes", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("NOTES=", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("changelog", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasReleaseNotesStep.Should().BeTrue("create-github-release job should generate release notes");
    }

    [Fact]
    public void CreateGitHubReleaseJob_Should_CreateRelease()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var releaseJob = jobs?["create-github-release"] as Dictionary<object, object>;
        var steps = releaseJob?["steps"] as List<object> ?? [];

        // Act
        var hasCreateReleaseStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var uses = stepDict?.TryGetValue("uses", out var usesObj) == true ? usesObj?.ToString() ?? "" : "";
            var name = stepDict?.TryGetValue("name", out var nameObj) == true ? nameObj?.ToString() ?? "" : "";
            var run = stepDict?.TryGetValue("run", out var runObj) == true ? runObj?.ToString() ?? "" : "";
            return uses.Contains("create-release", StringComparison.OrdinalIgnoreCase) ||
                   uses.Contains("gh-release", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("gh release create", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Create release", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasCreateReleaseStep.Should().BeTrue("create-github-release job should create GitHub release");
    }

    [Fact]
    public void PublishHomebrewJob_Should_DependOnCreateRelease()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var homebrewJob = jobs?["publish-homebrew"] as Dictionary<object, object>;

        // Act
        var needs = homebrewJob?["needs"] as List<object> ??
                   (homebrewJob?["needs"] != null ? new List<object> { homebrewJob["needs"] } : []);
        var dependencies = needs.Select(n => n.ToString()).ToList();

        // Assert
        homebrewJob.Should().NotBeNull("publish-homebrew job should exist");
        dependencies.Should().Contain("create-github-release", "publish-homebrew job should depend on create-github-release job");
    }

    [Fact]
    public void PublishHomebrewJob_Should_UseHomebrewTapToken()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var homebrewJob = jobs?["publish-homebrew"] as Dictionary<object, object>;
        var steps = homebrewJob?["steps"] as List<object> ?? [];

        // Act
        var usesHomebrewToken = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var env = stepDict?.TryGetValue("env", out var envObj) == true ? envObj as Dictionary<object, object> : null;
            var with_ = stepDict?.TryGetValue("with", out var withObj) == true ? withObj as Dictionary<object, object> : null;
            
            var envValues = env?.Values.Select(v => v.ToString()).ToList() ?? [];
            var withValues = with_?.Values.Select(v => v.ToString()).ToList() ?? [];
            
            return envValues.Any(v => v != null && v.Contains("HOMEBREW_TAP_TOKEN", StringComparison.OrdinalIgnoreCase)) ||
                   withValues.Any(v => v != null && v.Contains("HOMEBREW_TAP_TOKEN", StringComparison.OrdinalIgnoreCase));
        });

        // Assert
        usesHomebrewToken.Should().BeTrue("publish-homebrew job should use HOMEBREW_TAP_TOKEN secret");
    }

    [Fact]
    public void PublishHomebrewJob_Should_UpdateFormula()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var homebrewJob = jobs?["publish-homebrew"] as Dictionary<object, object>;
        var steps = homebrewJob?["steps"] as List<object> ?? [];

        // Act
        var hasFormulaUpdateStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?.TryGetValue("name", out var nameObj) == true ? nameObj?.ToString() ?? "" : "";
            var run = stepDict?.TryGetValue("run", out var runObj) == true ? runObj?.ToString() ?? "" : "";
            return name.Contains("formula", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("formula", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("git push", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasFormulaUpdateStep.Should().BeTrue("publish-homebrew job should update Homebrew formula");
    }

    [Fact]
    public void Workflow_Should_HaveEnvironmentProtection()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;

        // Act
        var hasProductionEnvironment = jobs?.Values.Any(jobObj =>
        {
            var job = jobObj as Dictionary<object, object>;
            if (job?.TryGetValue("environment", out var environment) != true)
            {
                return false;
            }
            
            if (environment is string envString)
            {
                return envString == "production";
            }
            
            if (environment is Dictionary<object, object> envDict)
            {
                var name = envDict.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : null;
                return name == "production";
            }
            
            return false;
        }) ?? false;

        // Assert
        hasProductionEnvironment.Should().BeTrue("workflow should use 'production' environment for approval gates");
    }

    [Fact]
    public void Workflow_Should_HaveConcurrencyControl()
    {
        // Arrange
        var workflow = LoadWorkflow();

        // Act
        var hasConcurrency = workflow.TryGetValue("concurrency", out var concurrencyObj);
        var concurrency = concurrencyObj as Dictionary<object, object>;
        var group = concurrency?["group"]?.ToString();
        object? cancelObj = null;
        var cancelInProgress = concurrency?.TryGetValue("cancel-in-progress", out cancelObj) ?? false;
        var cancel = cancelObj?.ToString()?.ToUpperInvariant();

        // Assert
        hasConcurrency.Should().BeTrue("workflow should have concurrency control");
        group.Should().NotBeNullOrEmpty("concurrency group should be defined");
        cancel.Should().Be("FALSE", "release workflow should not cancel in-progress runs");
    }

    [Fact]
    public void ValidateVersionJob_Should_CheckForDuplicateVersion()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var validateJob = jobs?["validate-version"] as Dictionary<object, object>;
        var steps = validateJob?["steps"] as List<object> ?? [];

        // Act
        var hasDuplicateCheckStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?.TryGetValue("name", out var nameObj) == true ? nameObj?.ToString() ?? "" : "";
            var run = stepDict?.TryGetValue("run", out var runObj) == true ? runObj?.ToString() ?? "" : "";
            return name.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("exists", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("gh release view", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasDuplicateCheckStep.Should().BeTrue("validate-version job should check for duplicate version in releases");
    }

    [Fact(Skip = "Obsolete - build-release-artifacts job removed")]
    public void BuildReleaseArtifactsJob_Should_UploadArtifacts()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var buildJob = jobs?["build-release-artifacts"] as Dictionary<object, object>;
        var steps = buildJob?["steps"] as List<object> ?? [];

        // Act
        var hasUploadStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var uses = stepDict?["uses"]?.ToString() ?? "";
            return uses.Contains("actions/upload-artifact", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasUploadStep.Should().BeTrue("build-release-artifacts job should upload artifacts for subsequent jobs");
    }

    [Fact]
    public void PublishHomebrewJob_Should_ValidateFormulaSyntax()
    {
        // Arrange
        var workflow = LoadWorkflow();
        var jobs = workflow["jobs"] as Dictionary<object, object>;
        var homebrewJob = jobs?["publish-homebrew"] as Dictionary<object, object>;
        var steps = homebrewJob?["steps"] as List<object> ?? [];

        // Act
        var hasValidationStep = steps.Any(step =>
        {
            var stepDict = step as Dictionary<object, object>;
            var name = stepDict?.TryGetValue("name", out var nameObj) == true ? nameObj?.ToString() ?? "" : "";
            var run = stepDict?.TryGetValue("run", out var runObj) == true ? runObj?.ToString() ?? "" : "";
            return name.Contains("validate", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("brew audit", StringComparison.OrdinalIgnoreCase) ||
                   run.Contains("brew style", StringComparison.OrdinalIgnoreCase);
        });

        // Assert
        hasValidationStep.Should().BeTrue("publish-homebrew job should validate formula syntax");
    }

    /// <summary>
    /// Load and parse the workflow YAML file.
    /// </summary>
    private Dictionary<string, object> LoadWorkflow()
    {
        var workflowContent = File.ReadAllText(WorkflowPath);
        return _yamlDeserializer.Deserialize<Dictionary<string, object>>(workflowContent)
            ?? throw new InvalidOperationException("Failed to parse workflow YAML");
    }
}
