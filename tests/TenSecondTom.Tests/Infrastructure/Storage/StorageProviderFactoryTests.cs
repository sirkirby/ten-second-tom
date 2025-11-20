using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Infrastructure.Storage.Providers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Storage;

/// <summary>
/// Critical path tests for StorageProviderFactory.
/// Focuses on provider discovery, creation, and error handling.
/// </summary>
public sealed class StorageProviderFactoryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<StorageProviderFactory> _logger;
    private readonly StorageProviderFactory _factory;

    public StorageProviderFactoryTests()
    {
        // Setup service provider with required dependencies
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging();

        // Add storage options
        services.Configure<StorageOptions>(options =>
        {
            options.RootDirectory = "./test-storage";
            options.ProviderId = StorageProviderIds.Default;
        });

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<StorageProviderFactory>>();
        _factory = new StorageProviderFactory(_serviceProvider, _logger);
    }

    [Fact]
    public void GetAvailableProviders_ShouldDiscoverDefaultAndObsidianProviders()
    {
        // Act
        var providers = _factory.GetAvailableProviders();

        // Assert
        providers.Should().NotBeEmpty("factory should discover providers via assembly scanning");
        providers.Should().HaveCountGreaterThanOrEqualTo(2, "at minimum default and obsidian providers should be discovered");

        // Verify default provider exists
        var defaultProvider = providers.FirstOrDefault(p =>
            p.ProviderId.Equals(StorageProviderIds.Default, StringComparison.OrdinalIgnoreCase));
        defaultProvider.Should().NotBeNull("default provider must be discovered");
        defaultProvider!.DisplayName.Should().NotBeNullOrWhiteSpace();
        defaultProvider.Description.Should().NotBeNullOrWhiteSpace();

        // Verify Obsidian provider exists
        var obsidianProvider = providers.FirstOrDefault(p =>
            p.ProviderId.Equals(StorageProviderIds.Obsidian, StringComparison.OrdinalIgnoreCase));
        obsidianProvider.Should().NotBeNull("obsidian provider must be discovered");
        obsidianProvider!.DisplayName.Should().NotBeNullOrWhiteSpace();
        obsidianProvider.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateProvider_WithDefaultProviderId_ShouldReturnDefaultProvider()
    {
        // Act
        var result = _factory.CreateProvider(StorageProviderIds.Default);

        // Assert
        result.IsSuccess.Should().BeTrue("factory should successfully create default provider");
        result.Value.Should().NotBeNull();
        result.Value.ProviderId.Should().Be(StorageProviderIds.Default);
        result.Value.Should().BeOfType<DefaultStorageProvider>();
    }

    [Fact]
    public void CreateProvider_WithObsidianProviderId_ShouldReturnObsidianProvider()
    {
        // Act
        var result = _factory.CreateProvider(StorageProviderIds.Obsidian);

        // Assert
        result.IsSuccess.Should().BeTrue("factory should successfully create obsidian provider");
        result.Value.Should().NotBeNull();
        result.Value.ProviderId.Should().Be(StorageProviderIds.Obsidian);
        result.Value.Should().BeOfType<ObsidianStorageProvider>();
    }

    [Theory]
    [InlineData("DEFAULT")] // Uppercase
    [InlineData("default")] // Lowercase
    [InlineData("Default")] // Mixed case
    public void CreateProvider_ShouldBeCaseInsensitive(string providerId)
    {
        // Act
        var result = _factory.CreateProvider(providerId);

        // Assert
        result.IsSuccess.Should().BeTrue("provider ID matching should be case-insensitive");
        result.Value.ProviderId.Should().Be(StorageProviderIds.Default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateProvider_WithNullOrEmptyProviderId_ShouldReturnFailure(string? providerId)
    {
        // Act
        var result = _factory.CreateProvider(providerId!);

        // Assert
        result.IsFailure.Should().BeTrue("empty provider ID should fail");
        result.Error.Should().Contain("ProviderId cannot be null or empty");
    }

    [Fact]
    public void CreateProvider_WithUnknownProviderId_ShouldReturnFailure()
    {
        // Arrange
        const string unknownProviderId = "nonexistent-provider";

        // Act
        var result = _factory.CreateProvider(unknownProviderId);

        // Assert
        result.IsFailure.Should().BeTrue("unknown provider should fail");
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain(unknownProviderId);
        result.Error.Should().Contain("Available providers:");
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
        {
            _ = new StorageProviderFactory(null!, _logger);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceProvider");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
        {
            _ = new StorageProviderFactory(_serviceProvider, null!);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
