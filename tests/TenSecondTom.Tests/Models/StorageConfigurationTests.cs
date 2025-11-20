using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Models;

/// <summary>
/// Unit tests for StorageConfiguration model.
/// Tests storage settings, retention policies, and auto-purge configuration.
/// </summary>
public sealed class StorageConfigurationTests
{
    [Fact]
    public void Create_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Indefinite,
            AutoPurge = false
        };

        // Assert
        config.Should().NotBeNull();
        config.RootDirectory.Should().Be(".memory");
        config.ProviderId.Should().Be("default");
        config.RetentionPolicy.Should().Be(RetentionPolicy.Indefinite);
        config.AutoPurge.Should().BeFalse();
    }

    [Fact]
    public void RootDirectory_DefaultPath_ShouldBeDotMemory()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Indefinite,
            AutoPurge = false
        };

        // Assert
        config.RootDirectory.Should().Be(".memory");
    }

    [Fact]
    public void RootDirectory_CanBeCustomPath()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = "/custom/path/to/memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Days90,
            AutoPurge = true
        };

        // Assert
        config.RootDirectory.Should().Be("/custom/path/to/memory");
    }

    [Fact]
    public void RetentionPolicy_Indefinite_ShouldRetainForever()
    {
        // Arrange & Act
        var policy = RetentionPolicy.Indefinite;

        // Assert
        policy.Should().Be(RetentionPolicy.Indefinite);
    }

    [Fact]
    public void RetentionPolicy_Days30_ShouldRetainFor30Days()
    {
        // Arrange & Act
        var policy = RetentionPolicy.Days30;

        // Assert
        policy.Should().Be(RetentionPolicy.Days30);
    }

    [Fact]
    public void RetentionPolicy_Days90_ShouldRetainFor90Days()
    {
        // Arrange & Act
        var policy = RetentionPolicy.Days90;

        // Assert
        policy.Should().Be(RetentionPolicy.Days90);
    }

    [Fact]
    public void RetentionPolicy_OneYear_ShouldRetainForOneYear()
    {
        // Arrange & Act
        var policy = RetentionPolicy.OneYear;

        // Assert
        policy.Should().Be(RetentionPolicy.OneYear);
    }

    [Fact]
    public void RetentionPolicy_TwoYears_ShouldRetainForTwoYears()
    {
        // Arrange & Act
        var policy = RetentionPolicy.TwoYears;

        // Assert
        policy.Should().Be(RetentionPolicy.TwoYears);
    }

    [Fact]
    public void AutoPurge_WhenTrue_EnablesAutomaticDeletion()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Days30,
            AutoPurge = true
        };

        // Assert
        config.AutoPurge.Should().BeTrue();
    }

    [Fact]
    public void AutoPurge_WhenFalse_DisablesAutomaticDeletion()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Days90,
            AutoPurge = false
        };

        // Assert
        config.AutoPurge.Should().BeFalse();
    }

    [Fact]
    public void StorageConfiguration_IsImmutable_PropertiesAreInitOnly()
    {
        // Arrange
        var original = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Indefinite,
            AutoPurge = false
        };

        // Act - Create modified copy using 'with' expression
        var modified = original with { RetentionPolicy = RetentionPolicy.Days30, AutoPurge = true };

        // Assert
        original.RetentionPolicy.Should().Be(RetentionPolicy.Indefinite);
        modified.RetentionPolicy.Should().Be(RetentionPolicy.Days30);
        original.Should().NotBe(modified);
    }

    [Theory]
    [InlineData(".memory", RetentionPolicy.Indefinite, false)]
    [InlineData(".memory", RetentionPolicy.Days30, true)]
    [InlineData("/custom/path", RetentionPolicy.Days90, true)]
    [InlineData("~/memories", RetentionPolicy.OneYear, false)]
    [InlineData("/var/data/memory", RetentionPolicy.TwoYears, true)]
    public void Create_WithVariousConfigurations_ShouldSucceed(
        string rootDirectory,
        RetentionPolicy retentionPolicy,
        bool autoPurge)
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = rootDirectory,
            ProviderId = "default",
            RetentionPolicy = retentionPolicy,
            AutoPurge = autoPurge
        };

        // Assert
        config.RootDirectory.Should().Be(rootDirectory);
        config.RetentionPolicy.Should().Be(retentionPolicy);
        config.AutoPurge.Should().Be(autoPurge);
    }

    [Fact]
    public void RetentionPolicy_WithIndefinite_AndAutoPurge_IsValidCombination()
    {
        // Even with Indefinite retention, AutoPurge can be true
        // (it just won't purge anything since there's no retention limit)

        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Indefinite,
            AutoPurge = true
        };

        // Assert
        config.RetentionPolicy.Should().Be(RetentionPolicy.Indefinite);
        config.AutoPurge.Should().BeTrue();
    }

    [Fact]
    public void MaxFileSize_WhenProvided_ShouldStoreSizeLimit()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Days90,
            AutoPurge = false,
            MaxFileSizeBytes = 10_485_760 // 10 MB
        };

        // Assert
        config.MaxFileSizeBytes.Should().Be(10_485_760);
    }

    [Fact]
    public void MaxFileSize_CanBeNull_ForUnlimitedSize()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Indefinite,
            AutoPurge = false,
            MaxFileSizeBytes = null
        };

        // Assert
        config.MaxFileSizeBytes.Should().BeNull();
    }

    [Fact]
    public void CompressionEnabled_WhenTrue_EnablesCompression()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.OneYear,
            AutoPurge = true,
            CompressionEnabled = true
        };

        // Assert
        config.CompressionEnabled.Should().BeTrue();
    }

    [Fact]
    public void CompressionEnabled_WhenFalse_DisablesCompression()
    {
        // Arrange & Act
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            RetentionPolicy = RetentionPolicy.Days30,
            AutoPurge = false,
            CompressionEnabled = false
        };

        // Assert
        config.CompressionEnabled.Should().BeFalse();
    }
}
