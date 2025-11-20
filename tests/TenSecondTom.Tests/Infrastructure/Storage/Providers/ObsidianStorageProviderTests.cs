using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Storage.Providers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Storage.Providers;

/// <summary>
/// Critical path tests for ObsidianStorageProvider.
/// Focuses on vault validation, initialization, and Obsidian-specific features.
/// </summary>
public sealed class ObsidianStorageProviderTests : IDisposable
{
    private readonly string _testVaultDirectory;
    private readonly ILoggerFactory _loggerFactory;

    public ObsidianStorageProviderTests()
    {
        _testVaultDirectory = Path.Combine(Path.GetTempPath(), $"obsidian-vault-{Guid.NewGuid()}");
        _loggerFactory = LoggerFactory.Create(builder => { });
    }

    [Fact]
    public void ProviderId_ShouldReturnObsidianConstant()
    {
        // Arrange
        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var providerId = provider.ProviderId;

        // Assert
        providerId.Should().Be(StorageProviderIds.Obsidian);
    }

    [Fact]
    public void DisplayName_ShouldContainObsidian()
    {
        // Arrange
        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var displayName = provider.DisplayName;

        // Assert
        displayName.Should().Contain("Obsidian");
    }

    [Fact]
    public async Task InitializeAsync_WithoutObsidianDirectory_ShouldFail()
    {
        // Arrange
        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("initialization should fail without .obsidian directory");
        result.Error.Should().Contain(".obsidian");
        result.Error.Should().Contain("Not a valid Obsidian vault");
    }

    [Fact]
    public async Task InitializeAsync_WithValidObsidianVault_ShouldSucceed()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);
        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("initialization should succeed with valid Obsidian vault");

        // Verify base vault is valid (no subdirectories pre-created)
        // Feature-specific subdirectories (today/, thisweek/, recording/) will be created
        // on-demand by FileSystemStorageProvider when entries are saved
        Directory.Exists(_testVaultDirectory).Should().BeTrue();
        Directory.Exists(Path.Combine(_testVaultDirectory, ".obsidian")).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithSubdirectory_ShouldCreateBaseDirectory()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);
        var subdirName = "ten-second-tom";
        var options = Options.Create(new StorageOptions
        {
            RootDirectory = _testVaultDirectory,
            MemorySubdirectory = subdirName,
            ProviderId = StorageProviderIds.Obsidian
        });

        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify TST base subdirectory is created
        // Feature-specific subdirectories (today/, thisweek/, recording/) will be created
        // on-demand by FileSystemStorageProvider when entries are saved
        var tstPath = Path.Combine(_testVaultDirectory, subdirName);
        Directory.Exists(tstPath).Should().BeTrue("TST subdirectory should be created");
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithoutObsidianDirectory_ShouldFail()
    {
        // Arrange
        Directory.CreateDirectory(_testVaultDirectory); // Create dir but no .obsidian
        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail without .obsidian directory");
        result.Error.Should().Contain(".obsidian");
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithValidVault_ShouldSucceed()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);
        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("validation should succeed with valid vault");
        result.Value.Should().Contain("Obsidian vault");
        result.Value.Should().Contain(_testVaultDirectory);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithSubdirectory_ShouldIncludeSubdirectoryInfo()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);
        var subdirName = "tst-notes";
        var options = Options.Create(new StorageOptions
        {
            RootDirectory = _testVaultDirectory,
            MemorySubdirectory = subdirName,
            ProviderId = StorageProviderIds.Obsidian
        });

        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("subdirectory");
        result.Value.Should().Contain(subdirName);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithNonexistentVault_ShouldFail()
    {
        // Arrange
        var nonexistentPath = Path.Combine(Path.GetTempPath(), "nonexistent-vault-" + Guid.NewGuid());
        var options = CreateOptions(nonexistentPath);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("validation should fail for nonexistent vault");
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ObsidianProvider_WithLegacyMemoryDirectory_ShouldUseAsVaultRoot()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);

        var options = Options.Create(new StorageOptions
        {
            RootDirectory = null, // Not set
            MemoryDirectory = _testVaultDirectory, // Legacy property for vault path
            ProviderId = StorageProviderIds.Obsidian
        });

        // Act
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);
        var initResult = await provider.InitializeAsync(CancellationToken.None);

        // Assert
        initResult.IsSuccess.Should().BeTrue("Obsidian provider should work with legacy MemoryDirectory as vault path");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testVaultDirectory))
            {
                Directory.Delete(_testVaultDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        _loggerFactory.Dispose();
    }

    private static IOptions<StorageOptions> CreateOptions(string vaultDirectory)
    {
        return Options.Create(new StorageOptions
        {
            RootDirectory = vaultDirectory,
            ProviderId = StorageProviderIds.Obsidian
        });
    }

    private static void CreateValidObsidianVault(string vaultPath)
    {
        Directory.CreateDirectory(vaultPath);
        Directory.CreateDirectory(Path.Combine(vaultPath, ".obsidian"));

        // Create minimal Obsidian configuration
        var appJsonPath = Path.Combine(vaultPath, ".obsidian", "app.json");
        File.WriteAllText(appJsonPath, "{}");
    }
}
