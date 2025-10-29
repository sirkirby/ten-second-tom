using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Storage.Providers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Storage.Providers;

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
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
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
    public async Task InitializeAsync_WithValidDirectory_ShouldCreateStructure()
    {
        // Arrange
        var options = CreateOptions(_testDirectory);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("initialization should succeed with valid directory");

        // Verify directory structure
        Directory.Exists(_testDirectory).Should().BeTrue("root directory should be created");
        Directory.Exists(Path.Combine(_testDirectory, DirectoryNames.Today)).Should().BeTrue("today subdirectory should be created");
        Directory.Exists(Path.Combine(_testDirectory, DirectoryNames.ThisWeek)).Should().BeTrue("thisweek subdirectory should be created");
        Directory.Exists(Path.Combine(_testDirectory, DirectoryNames.Templates)).Should().BeTrue("templates subdirectory should be created");
        Directory.Exists(Path.Combine(_testDirectory, DirectoryNames.Config)).Should().BeTrue("config subdirectory should be created");
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
        var options = CreateOptions(testDir);
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("validation should succeed if parent directory exists");
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
    public void BackwardCompatibility_WithLegacyMemoryDirectory_ShouldWork()
    {
        // Arrange - simulate legacy configuration with MemoryDirectory only
        var options = Options.Create(new StorageOptions
        {
            RootDirectory = null, // Not set (new property)
#pragma warning disable CS0618 // Type or member is obsolete
            MemoryDirectory = _testDirectory, // Legacy property set
#pragma warning restore CS0618
            ProviderId = StorageProviderIds.Default
        });

        // Act
        var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);
        var initResult = provider.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Assert
        initResult.IsSuccess.Should().BeTrue("provider should work with legacy MemoryDirectory");
        Directory.Exists(_testDirectory).Should().BeTrue("directory should be created using legacy property");
    }

    [Fact]
    public void RootDirectory_TakesPrecedenceOver_LegacyMemoryDirectory()
    {
        // Arrange - both properties set, RootDirectory should win
        var rootDir = Path.Combine(Path.GetTempPath(), $"root-{Guid.NewGuid()}");
        var legacyDir = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid()}");

        var options = Options.Create(new StorageOptions
        {
            RootDirectory = rootDir,
#pragma warning disable CS0618 // Type or member is obsolete
            MemoryDirectory = legacyDir,
#pragma warning restore CS0618
            ProviderId = StorageProviderIds.Default
        });

        try
        {
            // Act
            var provider = new DefaultStorageProvider(options, Mock.Of<ILogger<DefaultStorageProvider>>(), _loggerFactory);
            var initResult = provider.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            initResult.IsSuccess.Should().BeTrue();
            Directory.Exists(rootDir).Should().BeTrue("RootDirectory should be used");
            Directory.Exists(legacyDir).Should().BeFalse("legacy MemoryDirectory should be ignored");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, true);
        }
    }

    [Fact]
    public async Task WithMemorySubdirectory_ShouldCreateNestedStructure()
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

        // Verify subdirectories are created under the memory subdirectory
        var memoryPath = Path.Combine(_testDirectory, subdirName);
        Directory.Exists(memoryPath).Should().BeTrue("memory subdirectory should be created");
        Directory.Exists(Path.Combine(memoryPath, DirectoryNames.Today)).Should().BeTrue();
        Directory.Exists(Path.Combine(memoryPath, DirectoryNames.ThisWeek)).Should().BeTrue();
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
