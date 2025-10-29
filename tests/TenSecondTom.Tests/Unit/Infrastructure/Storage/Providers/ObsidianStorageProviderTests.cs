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
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
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

        // Verify Obsidian-friendly subdirectories were created
        Directory.Exists(Path.Combine(_testVaultDirectory, "Daily Notes")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testVaultDirectory, "Weekly Reviews")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testVaultDirectory, "Templates")).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithSubdirectory_ShouldCreateNestedStructure()
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

        // Verify TST entries are isolated in subdirectory
        var tstPath = Path.Combine(_testVaultDirectory, subdirName);
        Directory.Exists(tstPath).Should().BeTrue("TST subdirectory should be created");
        Directory.Exists(Path.Combine(tstPath, "Daily Notes")).Should().BeTrue();
        Directory.Exists(Path.Combine(tstPath, "Weekly Reviews")).Should().BeTrue();
        Directory.Exists(Path.Combine(tstPath, "Templates")).Should().BeTrue();
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
    public async Task ValidateConfigurationAsync_WithReadOnlyVault_ShouldFail()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);

        // Make directory read-only (platform-specific test)
        if (!OperatingSystem.IsWindows())
        {
            var dirInfo = new DirectoryInfo(_testVaultDirectory);
            dirInfo.Attributes |= FileAttributes.ReadOnly;
        }

        var options = CreateOptions(_testVaultDirectory);
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);

        // Act
        var result = await provider.ValidateConfigurationAsync(CancellationToken.None);

        // Assert
        if (!OperatingSystem.IsWindows())
        {
            result.IsFailure.Should().BeTrue("validation should fail for read-only vault");
            result.Error.Should().Contain("not writable");
        }
        else
        {
            // Windows requires different permissions handling
            result.Should().Match<Result<string>>(r => r.IsSuccess || r.Error!.Contains("writable"));
        }
    }

    [Fact]
    public void ObsidianProvider_WithLegacyMemoryDirectory_ShouldUseAsVaultRoot()
    {
        // Arrange
        CreateValidObsidianVault(_testVaultDirectory);

        var options = Options.Create(new StorageOptions
        {
            RootDirectory = null, // Not set
#pragma warning disable CS0618 // Type or member is obsolete
            MemoryDirectory = _testVaultDirectory, // Legacy property for vault path
#pragma warning restore CS0618
            ProviderId = StorageProviderIds.Obsidian
        });

        // Act
        var provider = new ObsidianStorageProvider(options, Mock.Of<ILogger<ObsidianStorageProvider>>(), _loggerFactory);
        var initResult = provider.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Assert
        initResult.IsSuccess.Should().BeTrue("Obsidian provider should work with legacy MemoryDirectory as vault path");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testVaultDirectory))
            {
                // Remove read-only attribute if set
                var dirInfo = new DirectoryInfo(_testVaultDirectory);
                if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                }

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
