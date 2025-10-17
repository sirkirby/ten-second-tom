using System.IO.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Features.Templates.Services;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.Templates;

/// <summary>
/// Tests for the TemplateMigrationService.
/// Tests automatic template installation for existing users.
/// </summary>
public sealed class TemplateMigrationServiceTests
{
    private readonly Mock<IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>>> _mockTemplateHandler;
    private readonly Mock<ILogger<TemplateMigrationService>> _mockLogger;
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly TemplateMigrationService _service;

    public TemplateMigrationServiceTests()
    {
        _mockTemplateHandler = new Mock<IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>>>();
        _mockLogger = new Mock<ILogger<TemplateMigrationService>>();
        _mockFileSystem = new Mock<IFileSystem>();

        _service = new TemplateMigrationService(
            _mockTemplateHandler.Object,
            _mockLogger.Object,
            _mockFileSystem.Object);
    }

    [Fact]
    public async Task RunAutomaticMigration_WithNoMemoryDirectory_SkipsMigration()
    {
        // Arrange
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["Storage:MemoryDirectory"]).Returns((string?)null);

        // Act
        await _service.RunAutomaticMigrationAsync(mockConfiguration.Object, CancellationToken.None);

        // Assert
        _mockTemplateHandler.Verify(h => h.Handle(
            It.IsAny<InstallDefaultTemplatesCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAutomaticMigration_WithMemoryDirectory_PerformsMigration()
    {
        // Arrange
        string rootDirectory = "/test/memory";
        string templatesDirectory = Path.Combine(rootDirectory, "templates");

        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["Storage:MemoryDirectory"]).Returns(rootDirectory);

        _mockFileSystem.Setup(fs => fs.Directory.Exists(templatesDirectory))
            .Returns(false);

        _mockFileSystem.Setup(fs => fs.Path.Combine(rootDirectory, "templates"))
            .Returns(templatesDirectory);

        _mockTemplateHandler.Setup(h => h.Handle(
                It.IsAny<InstallDefaultTemplatesCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InstallDefaultTemplatesResult>.Success(new InstallDefaultTemplatesResult
            {
                TemplatesInstalled = 2,
                TemplatesSkipped = 0,
                TemplatesFailed = 0,
                InstalledTemplateIds = ["daily-summary", "weekly-review"]
            }));

        // Act
        await _service.RunAutomaticMigrationAsync(mockConfiguration.Object, CancellationToken.None);

        // Assert
        _mockTemplateHandler.Verify(h => h.Handle(
            It.IsAny<InstallDefaultTemplatesCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAutomaticMigration_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = async () => await _service.RunAutomaticMigrationAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAutomaticMigration_WhenMigrationFails_LogsWarningButDoesNotThrow()
    {
        // Arrange
        string rootDirectory = "/test/memory";
        string templatesDirectory = Path.Combine(rootDirectory, "templates");

        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["Storage:MemoryDirectory"]).Returns(rootDirectory);

        _mockFileSystem.Setup(fs => fs.Directory.Exists(templatesDirectory))
            .Returns(false);

        _mockFileSystem.Setup(fs => fs.Path.Combine(rootDirectory, "templates"))
            .Returns(templatesDirectory);

        _mockTemplateHandler.Setup(h => h.Handle(
                It.IsAny<InstallDefaultTemplatesCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        // Act
        var act = async () => await _service.RunAutomaticMigrationAsync(mockConfiguration.Object, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("migration failures should be caught");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Template migration failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
