using TenSecondTom.Shared.Contracts;
using System.IO.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Features.Templates.Services;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.Integration.Features.Templates;

/// <summary>
/// Integration tests for template migration functionality.
/// Tests the automatic installation of templates for existing users.
/// </summary>
public sealed class TemplateMigrationIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly IServiceProvider _serviceProvider;
    private readonly TemplateMigrationService _migrationService;

    public TemplateMigrationIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"tom-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Setup DI container
        var services = new ServiceCollection();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<YamlFrontMatterParser>();
        services.AddSingleton<IPromptTemplateLoader>(sp =>
        {
            var yamlParser = sp.GetRequiredService<YamlFrontMatterParser>();
            return new EmbeddedPromptTemplateLoader(null, yamlParser);
        });
        services.AddSingleton<IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>>, InstallDefaultTemplatesHandler>();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Register TemplateMigrationService
        services.AddTransient<TemplateMigrationService>();

        _serviceProvider = services.BuildServiceProvider();
        _migrationService = _serviceProvider.GetRequiredService<TemplateMigrationService>();
    }

    [Fact]
    public async Task Migration_WhenTemplatesDirectoryDoesNotExist_CreatesAndInstallsTemplates()
    {
        // Arrange
        string rootDirectory = _testDirectory;
        string templatesDirectory = Path.Combine(rootDirectory, "templates");

        var storageOptions = new StorageOptions { MemoryDirectory = rootDirectory };
        var mockOptions = new Mock<IOptions<StorageOptions>>();
        mockOptions.Setup(o => o.Value).Returns(storageOptions);

        // Act
        await _migrationService.RunAutomaticMigrationAsync(
            mockOptions.Object,
            CancellationToken.None);

        // Assert
        Directory.Exists(templatesDirectory).Should().BeTrue();
        File.Exists(Path.Combine(templatesDirectory, "daily-summary.md")).Should().BeTrue();
        File.Exists(Path.Combine(templatesDirectory, "daily-standup.md")).Should().BeTrue();
        File.Exists(Path.Combine(templatesDirectory, "weekly-review.md")).Should().BeTrue();
        File.Exists(Path.Combine(templatesDirectory, "business-meeting.md")).Should().BeTrue();

        // Verify content
        string dailyContent = await File.ReadAllTextAsync(Path.Combine(templatesDirectory, "daily-summary.md"));
        dailyContent.Should().Contain("Daily Summary");

        string standupContent = await File.ReadAllTextAsync(Path.Combine(templatesDirectory, "daily-standup.md"));
        standupContent.Should().Contain("Daily Standup");

        string weeklyContent = await File.ReadAllTextAsync(Path.Combine(templatesDirectory, "weekly-review.md"));
        weeklyContent.Should().Contain("Weekly Review");

        string meetingContent = await File.ReadAllTextAsync(Path.Combine(templatesDirectory, "business-meeting.md"));
        meetingContent.Should().Contain("Business Meeting");
    }

    [Fact]
    public async Task Migration_WhenCalledTwice_SecondCallDoesNothing()
    {
        // Arrange
        string memoryDirectory = _testDirectory;
        var storageOptions = new StorageOptions { MemoryDirectory = memoryDirectory };
        var mockOptions = new Mock<IOptions<StorageOptions>>();
        mockOptions.Setup(o => o.Value).Returns(storageOptions);

        // Act - First migration
        await _migrationService.RunAutomaticMigrationAsync(
            mockOptions.Object,
            CancellationToken.None);

        // Get creation time of a template file
        var templatePath = Path.Combine(_testDirectory, "templates", "daily-summary.md");
        var firstCreationTime = File.GetLastWriteTimeUtc(templatePath);

        // Wait a tiny bit to ensure different timestamps if file is recreated
        await Task.Delay(100);

        // Act - Second migration
        await _migrationService.RunAutomaticMigrationAsync(
            mockOptions.Object,
            CancellationToken.None);

        // Assert - File should not have been touched
        var secondCreationTime = File.GetLastWriteTimeUtc(templatePath);
        secondCreationTime.Should().Be(firstCreationTime, "template should not be recreated if it already exists");
    }

    [Fact]
    public async Task Migration_WhenUserHasCustomizedTemplates_PreservesCustomizations()
    {
        // Arrange
        string rootDirectory = _testDirectory;
        string templatesDirectory = Path.Combine(rootDirectory, "templates");
        Directory.CreateDirectory(templatesDirectory);

        // Create customized daily-summary
        string customContent = "---\ntemplate_id: daily-summary\n---\n# MY CUSTOM DAILY TEMPLATE";
        await File.WriteAllTextAsync(
            Path.Combine(templatesDirectory, "daily-summary.md"),
            customContent);

        var storageOptions = new StorageOptions { MemoryDirectory = rootDirectory };
        var mockOptions = new Mock<IOptions<StorageOptions>>();
        mockOptions.Setup(o => o.Value).Returns(storageOptions);

        // Act
        await _migrationService.RunAutomaticMigrationAsync(
            mockOptions.Object,
            CancellationToken.None);

        // Assert
        // Verify custom template preserved
        string dailyContent = await File.ReadAllTextAsync(Path.Combine(templatesDirectory, "daily-summary.md"));
        dailyContent.Should().Be(customContent, "customized template should be preserved");

        // Verify missing template installed
        File.Exists(Path.Combine(templatesDirectory, "weekly-review.md")).Should().BeTrue();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch (IOException)
        {
            // Ignore cleanup errors - test directory might be in use
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore cleanup errors - permission issues
        }

        (_serviceProvider as IDisposable)?.Dispose();
    }
}
