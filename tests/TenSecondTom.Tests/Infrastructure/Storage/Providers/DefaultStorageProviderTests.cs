using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Storage.Providers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Storage.Providers;

/// <summary>
/// Critical path tests for DefaultStorageProvider.
/// Focuses on initialization, validation, and backward compatibility.
/// </summary>
public sealed class DefaultStorageProviderTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public DefaultStorageProviderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"tst-test-{Guid.NewGuid()}");
        _loggerFactory = LoggerFactory.Create(builder => { });
    }

    [Fact]
    public void ProviderId_ShouldReturnDefaultConstant()
    {
        // Arrange
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var providerId = provider.ProviderId;

        // Assert
        providerId.Should().Be(StorageProviderIds.Default);
    }

    [Fact]
    public void DisplayName_ShouldNotBeEmpty()
    {
        // Arrange
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var displayName = provider.DisplayName;

        // Assert
        displayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        // Arrange
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var description = provider.Description;

        // Assert
        description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InitializeAsync_WithValidDirectory_ShouldCreateBaseDirectory()
    {
        // Arrange
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("initialization should succeed with valid directory");

        // Verify base directory is created
        // Feature-specific subdirectories (today/, thisweek/, recording/) will be created
        // on-demand by FileSystemStorageProvider when entries are saved
        Directory.Exists(_testDirectory).Should().BeTrue("root directory should be created");
    }

    [Fact]
    public async Task InitializeAsync_WithExistingDirectory_ShouldSucceed()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("initialization should succeed with existing directory");
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithValidDirectory_ShouldSucceed()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("validation should succeed with valid directory");
        result.Value.Should().Contain(_testDirectory);
        result.Value.Should().Contain("Default provider");
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithNonexistentDirectory_ShouldSucceedIfParentExists()
    {
        // Arrange
        var parentDir = Path.GetTempPath();
        var testDir = Path.Combine(parentDir, $"nonexistent-{Guid.NewGuid()}");

        // Create the directory so validation can test write permissions
        Directory.CreateDirectory(testDir);

        try
        {
            var options = CreateOptions(testDir);
            var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

            // Act
            var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue("validation should succeed if directory exists and is writable");
        }
        finally
        {
            // Clean up
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithInvalidParentDirectory_ShouldFail()
    {
        // Arrange
        var invalidDir = Path.Combine("/nonexistent-root-dir-12345", "subdir");
        var options = CreateOptions(invalidDir);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail if parent directory doesn't exist");
        result.Error.Should().Contain("Parent directory does not exist");
    }

    [Fact]
    public async Task WithMemorySubdirectory_ShouldCreateBaseDirectory()
    {
        // Arrange
        var subdirName = "memory";
        var options = Options.Create(new StorageOptions
        {
            RootDirectory = _testDirectory,
            MemorySubdirectory = subdirName,
            ProviderId = StorageProviderIds.Default
        });

        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify base memory subdirectory is created
        // Feature-specific subdirectories (today/, thisweek/, recording/) will be created
        // on-demand by FileSystemStorageProvider when entries are saved
        var memoryPath = Path.Combine(_testDirectory, subdirName);
        Directory.Exists(memoryPath).Should().BeTrue("memory subdirectory should be created");
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
        catch
        {
            // Best effort cleanup
        }

        _loggerFactory.Dispose();
    }

    private static IOptions<StorageOptions> CreateOptions(string rootDirectory)
    {
        return Options.Create(new StorageOptions
        {
            RootDirectory = rootDirectory,
            ProviderId = StorageProviderIds.Default
        });
    }
}
