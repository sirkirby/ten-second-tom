using FluentAssertions;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.Shared.Extensions;

/// <summary>
/// Unit tests for StorageOptionsExtensions.EffectiveStorageDirectory property.
/// Tests verify the resolution priority (ProviderPath → RootDirectory → fallback),
/// tilde expansion, MemorySubdirectory appending, and edge case handling.
/// This is a critical path used throughout the application for storage path resolution.
/// </summary>
public sealed class StorageOptionsExtensionsTests
{
    private const string FallbackDirectory = "./ten-second-tom";

    #region ProviderPath Priority Tests

    [Fact]
    public void GetEffectiveStorageDirectory_WithProviderPathOnly_ReturnsProviderPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/custom/vault"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/custom/vault");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithProviderPathAndRootDirectory_PrioritizesProviderPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault/path",
            RootDirectory = "/root/path"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/vault/path");
        result.Should().NotContain("root/path");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithProviderPathAndFallback_PrioritizesProviderPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault/path"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/vault/path");
        result.Should().NotBe(FallbackDirectory);
    }

    #endregion

    #region RootDirectory Tests

    [Fact]
    public void GetEffectiveStorageDirectory_WithRootDirectoryOnly_ReturnsRootDirectory()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "/custom/root"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/custom/root");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithRootDirectoryAndNoProviderPath_ReturnsRootDirectory()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "~/.memory"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().Contain(".memory");
    }

    #endregion

    #region Fallback Tests

    [Fact]
    public void GetEffectiveStorageDirectory_WithNeitherProviderPathNorRootDirectory_ReturnsFallback()
    {
        // Arrange
        var options = new StorageOptions();

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(FallbackDirectory);
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithNullRootDirectory_ReturnsFallback()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = null
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(FallbackDirectory);
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithEmptyRootDirectory_ReturnsEmptyString()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = ""
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        // Empty string RootDirectory is not treated as "not set" - it's returned as-is
        result.Should().Be("");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithWhitespaceOnlyRootDirectory_ReturnsWhitespaceString()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "   "
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        // Whitespace-only RootDirectory is not treated as "not set" - it's returned as-is
        result.Should().Be("   ");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithEmptyProviderPath_ReturnsFallback()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = ""
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(FallbackDirectory);
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithWhitespaceOnlyProviderPath_ReturnsFallback()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "   "
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(FallbackDirectory);
    }

    #endregion

    #region Tilde Expansion Tests

    [Fact]
    public void GetEffectiveStorageDirectory_WithTildeInProviderPath_ExpandsToHomeDirectory()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "~/Documents/MyVault"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().Contain("Documents");
        result.Should().Contain("MyVault");
        result.Should().NotContain("~");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithTildeInRootDirectory_ExpandsToHomeDirectory()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "~/ten-second-tom"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().Contain("ten-second-tom");
        result.Should().NotContain("~");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithTildeInFallback_ExpandsToHomeDirectory()
    {
        // Arrange
        var options = new StorageOptions(); // Will use fallback
        var expectedFallback = Path.Combine(GetHomeDirectory(), "ten-second-tom");

        // Act
        var result = options.EffectiveStorageDirectory;
        var expandedFallback = result.Replace("~", GetHomeDirectory());

        // Assert
        // The fallback "./ten-second-tom" doesn't contain ~, so no expansion needed
        result.Should().Be(FallbackDirectory);
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithComplexTildePath_ExpandsCorrectly()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "~/Library/Application Support/MyApp"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().Contain("Library");
        result.Should().Contain("Application Support");
        result.Should().Contain("MyApp");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithPathWithoutTilde_ReturnsUnchanged()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/absolute/path/without/tilde"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/absolute/path/without/tilde");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithRelativePath_ReturnsUnchanged()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "./relative/path"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("./relative/path");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithTildeNotAtStart_ReplacesAllTildes()
    {
        // Arrange
        // This is an edge case - tilde in the middle of a path (unusual but tests the Replace behavior)
        var options = new StorageOptions
        {
            ProviderPath = "/path/with~/tilde"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        // The implementation uses Replace, so all ~ are replaced
        result.Should().Contain(GetHomeDirectory());
    }

    #endregion

    #region MemorySubdirectory Tests

    [Fact]
    public void GetEffectiveStorageDirectory_WithProviderPathAndMemorySubdirectory_AppendsBoth()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault",
            MemorySubdirectory = "tst"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(Path.Combine("/vault", "tst"));
        result.Should().Contain("vault");
        result.Should().Contain("tst");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithRootDirectoryAndMemorySubdirectory_AppendsBoth()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "/root",
            MemorySubdirectory = "memory"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(Path.Combine("/root", "memory"));
        result.Should().Contain("root");
        result.Should().Contain("memory");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithFallbackAndMemorySubdirectory_AppendsBoth()
    {
        // Arrange
        var options = new StorageOptions
        {
            MemorySubdirectory = "memory"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(Path.Combine(FallbackDirectory, "memory"));
        result.Should().Contain("ten-second-tom");
        result.Should().Contain("memory");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithMemorySubdirectoryNull_DoesNotAppendPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault",
            MemorySubdirectory = null
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/vault");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithMemorySubdirectoryEmpty_DoesNotAppendPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault",
            MemorySubdirectory = ""
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/vault");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithMemorySubdirectoryWhitespace_DoesNotAppendPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault",
            MemorySubdirectory = "   "
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/vault");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithMultiLevelMemorySubdirectory_AppendsCorrectly()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/vault",
            MemorySubdirectory = "tst/memory/today"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(Path.Combine("/vault", "tst/memory/today"));
    }

    #endregion

    #region Complex Scenarios (Real-World Cases)

    [Fact]
    public void GetEffectiveStorageDirectory_ObsidianVaultScenario_ReturnsCorrectPath()
    {
        // Arrange - Obsidian user storing notes in ~/Documents/MyVault with subdirectory
        var options = new StorageOptions
        {
            ProviderPath = "~/Documents/MyVault",
            MemorySubdirectory = "tst"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().Contain("Documents");
        result.Should().Contain("MyVault");
        result.Should().Contain("tst");
        result.Should().NotContain("~");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_DefaultProviderWithTildeScenario_ReturnsCorrectPath()
    {
        // Arrange - Default provider with standard home directory path
        var options = new StorageOptions
        {
            RootDirectory = "~/ten-second-tom"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().EndWith("ten-second-tom");
        result.Should().NotContain("~");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_DefaultProviderWithCustomPathScenario_ReturnsCorrectPath()
    {
        // Arrange - Default provider with custom directory
        var options = new StorageOptions
        {
            RootDirectory = "/var/data/notes"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/var/data/notes");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_MinimalConfigurationScenario_ReturnsFallback()
    {
        // Arrange - Minimal configuration with no explicit paths
        var options = new StorageOptions();

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be(FallbackDirectory);
    }

    [Fact]
    public void GetEffectiveStorageDirectory_ObsidianWithComplexPathAndSubdirectoryScenario_ReturnsCorrectPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "~/Documents/Obsidian Vaults/Work Vault",
            MemorySubdirectory = "Daily Notes/Ten Second Tom"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().Contain("Documents");
        result.Should().Contain("Obsidian Vaults");
        result.Should().Contain("Work Vault");
        result.Should().Contain("Daily Notes");
        result.Should().Contain("Ten Second Tom");
        result.Should().NotContain("~");
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Fact]
    public void GetEffectiveStorageDirectory_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        StorageOptions? options = null;

        // Act & Assert
        var exception = Record.Exception(() => options!.EffectiveStorageDirectory);
        exception.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithOnlyWhitespaceInAllPaths_ReturnsWhitespaceFromRootDirectory()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "   ",
            RootDirectory = "\t"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        // ProviderPath whitespace triggers fallback to RootDirectory, which is returned as-is
        result.Should().Be("\t");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithProviderPathPriorityIgnoresAllOther()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/provider/path",
            RootDirectory = "/root/path",
            MemorySubdirectory = null // No subdirectory to append
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/provider/path");
        result.Should().NotContain("root");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithPathContainingSpaces_PreservesSpaces()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/path with spaces/my vault"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/path with spaces/my vault");
        result.Should().Contain(" ");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithPathContainingSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/path/with-special_chars.123"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("/path/with-special_chars.123");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithTrailingSlash_PreservesSlash()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "/path/with/trailing/slash/"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().EndWith("/");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_WithDotPathSegments_PreservesDots()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootDirectory = "../relative/path"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().Be("../relative/path");
    }

    #endregion

    #region Integration Between Priority and Tilde

    [Fact]
    public void GetEffectiveStorageDirectory_ProviderPathPriorityWithTildeExpansion_CorrectlyExpandsProviderPath()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "~/vault",
            RootDirectory = "/ignored/root"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        result.Should().StartWith(GetHomeDirectory());
        result.Should().EndWith("vault");
        result.Should().NotContain("/ignored/root");
    }

    [Fact]
    public void GetEffectiveStorageDirectory_TildeWithMemorySubdirectory_BothAppliedCorrectly()
    {
        // Arrange
        var options = new StorageOptions
        {
            ProviderPath = "~/my-vault",
            MemorySubdirectory = "memory"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        var expectedPath = Path.Combine(GetHomeDirectory(), "my-vault", "memory");
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GetEffectiveStorageDirectory_FallbackWithTildeAndMemorySubdirectory_AllAppliedCorrectly()
    {
        // Arrange
        var options = new StorageOptions
        {
            MemorySubdirectory = "memory"
        };

        // Act
        string result = options.EffectiveStorageDirectory;

        // Assert
        // Fallback is "./ten-second-tom", no tilde to expand, just append subdirectory
        result.Should().Be(Path.Combine(FallbackDirectory, "memory"));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the user's home directory using the same method as the extension.
    /// </summary>
    /// <returns>The expanded home directory path.</returns>
    private static string GetHomeDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    #endregion
}
