using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Templates;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Templates;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.Models;
using Xunit;
using TenSecondTom.Features.Setup;

namespace TenSecondTom.IntegrationTests.Integration.Features.Setup;

/// <summary>
/// Integration tests for template installation during setup workflow.
/// Tests the full flow of installing default templates from embedded resources to filesystem.
/// </summary>
public sealed class SetupWithTemplatesIntegrationTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly Mock<ILogger<InstallDefaultTemplates.Handler>> _handlerLogger;
    private readonly Mock<ILogger<YamlFrontMatterParser>> _parserLogger;
    private readonly EmbeddedPromptTemplateLoader _embeddedLoader;
    private readonly string _testDirectory;

    public SetupWithTemplatesIntegrationTests()
    {
        _fileSystem = new MockFileSystem();
        _handlerLogger = new Mock<ILogger<InstallDefaultTemplates.Handler>>();
        _parserLogger = new Mock<ILogger<YamlFrontMatterParser>>();
        var yamlParser = new YamlFrontMatterParser(_parserLogger.Object);
        _embeddedLoader = new EmbeddedPromptTemplateLoader(baseDirectory: null, yamlParser: yamlParser);
        _testDirectory = "/Users/test/.memory/templates";
    }

    [Fact]
    public async Task InstallTemplates_EndToEnd_CreatesAllTemplateFiles()
    {
        // Arrange
        var handler = CreateHandler();

        var command = new InstallDefaultTemplates.Command
        {
            TargetDirectory = _testDirectory,
            OverwriteExisting = false
        };

        // Act
        Result<TemplateInstallationResult> result = await handler.Handle(
            command,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TemplatesInstalled.Should().Be(6);
        result.Value.TemplatesSkipped.Should().Be(0);
        result.Value.TemplatesFailed.Should().Be(0);
        result.Value.InstalledTemplateIds.Should().Contain("daily-summary");
        result.Value.InstalledTemplateIds.Should().Contain("daily-standup");
        result.Value.InstalledTemplateIds.Should().Contain("weekly-review");
        result.Value.InstalledTemplateIds.Should().Contain("business-meeting");

        // Verify files were created
        _fileSystem.Directory.Exists(_testDirectory).Should().BeTrue();
        _fileSystem.File.Exists(_fileSystem.Path.Combine(_testDirectory, "daily-summary.md")).Should().BeTrue();
        _fileSystem.File.Exists(_fileSystem.Path.Combine(_testDirectory, "daily-standup.md")).Should().BeTrue();
        _fileSystem.File.Exists(_fileSystem.Path.Combine(_testDirectory, "weekly-review.md")).Should().BeTrue();
        _fileSystem.File.Exists(_fileSystem.Path.Combine(_testDirectory, "business-meeting.md")).Should().BeTrue();

        // Verify content matches template format
        string dailyContent = await _fileSystem.File.ReadAllTextAsync(
            _fileSystem.Path.Combine(_testDirectory, "daily-summary.md"));

        // Content should start with YAML front matter
        dailyContent.Should().StartWith("---");
        dailyContent.Should().Contain("templateType: daily");
        dailyContent.Should().Contain("title: Daily Summary");
        dailyContent.Should().Contain("# Daily Summary Template");
    }

    [Fact]
    public async Task InstallTemplates_WhenAlreadyInstalled_SkipsExistingFiles()
    {
        // Arrange
        var handler = CreateHandler();

        var command = new InstallDefaultTemplates.Command
        {
            TargetDirectory = _testDirectory,
            OverwriteExisting = false
        };

        // First installation
        await handler.Handle(command, CancellationToken.None);

        // Act - Second installation
        Result<TemplateInstallationResult> result = await handler.Handle(
            command,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplatesInstalled.Should().Be(0);
        result.Value.TemplatesSkipped.Should().Be(6);
        result.Value.TemplatesFailed.Should().Be(0);
    }

    [Fact]
    public async Task InstallTemplates_WithOverwrite_ReplacesExistingFiles()
    {
        // Arrange
        var handler = CreateHandler();

        // Pre-create a modified file
        _fileSystem.AddDirectory(_testDirectory);
        string dailyPath = _fileSystem.Path.Combine(_testDirectory, "daily-summary.md");
        await _fileSystem.File.WriteAllTextAsync(dailyPath, "MODIFIED CONTENT");

        var command = new InstallDefaultTemplates.Command
        {
            TargetDirectory = _testDirectory,
            OverwriteExisting = true
        };

        // Act
        Result<TemplateInstallationResult> result = await handler.Handle(
            command,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplatesInstalled.Should().Be(6);
        result.Value.TemplatesSkipped.Should().Be(0);

        // Verify content was replaced
        string newContent = await _fileSystem.File.ReadAllTextAsync(dailyPath);
        newContent.Should().NotBe("MODIFIED CONTENT");
        newContent.Should().Contain("# Daily Summary Template");
    }

    [Fact]
    public async Task LoadAllTemplates_ReturnsTemplatesWithMetadata()
    {
        // Arrange
        var yamlParser = new YamlFrontMatterParser(_parserLogger.Object);
        var templateLoader = new EmbeddedPromptTemplateLoader(
            baseDirectory: null,
            yamlParser: yamlParser);

        // Act
        Result<List<TenSecondTom.Shared.Models.PromptTemplate>> result =
            await templateLoader.LoadAllTemplatesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(6);

        var dailyTemplate = result.Value.First(t => t.TemplateId == "daily-summary");
        dailyTemplate.Metadata.Should().NotBeNull();
        dailyTemplate.Metadata!.TemplateType.Should().Be(TenSecondTom.Shared.Models.TemplateType.Daily);
        dailyTemplate.Metadata.Title.Should().Be("Daily Summary");
        dailyTemplate.Metadata.Description.Should().NotBeNullOrEmpty();
        dailyTemplate.Source.Should().Be(TenSecondTom.Shared.Models.TemplateSource.Embedded);

        var weeklyTemplate = result.Value.First(t => t.TemplateId == "weekly-review");
        weeklyTemplate.Metadata.Should().NotBeNull();
        weeklyTemplate.Metadata!.TemplateType.Should().Be(TenSecondTom.Shared.Models.TemplateType.Weekly);
        weeklyTemplate.Source.Should().Be(TenSecondTom.Shared.Models.TemplateSource.Embedded);

        var businessMeetingTemplate = result.Value.First(t => t.TemplateId == "business-meeting");
        businessMeetingTemplate.Metadata.Should().NotBeNull();
        businessMeetingTemplate.Metadata!.TemplateType.Should().Be(TenSecondTom.Shared.Models.TemplateType.BusinessMeeting);
        businessMeetingTemplate.Source.Should().Be(TenSecondTom.Shared.Models.TemplateSource.Embedded);
    }

    private InstallDefaultTemplates.Handler CreateHandler()
    {
        var installerLogger = new Mock<ILogger<TemplateInstaller>>();
        var templateInstaller = new TemplateInstaller(
            _fileSystem,
            _embeddedLoader,
            installerLogger.Object);

        return new InstallDefaultTemplates.Handler(
            templateInstaller,
            _handlerLogger.Object);
    }
}
