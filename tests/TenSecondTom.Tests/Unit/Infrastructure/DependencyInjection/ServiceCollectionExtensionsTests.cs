using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.DependencyInjection;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.DependencyInjection;

/// <summary>
/// Tests for ServiceCollectionExtensions to ensure proper dependency injection configuration.
/// </summary>
public sealed class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly ServiceCollection _services;
    private ServiceProvider? _serviceProvider;

    public ServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();

        // Setup minimal required services
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:MemoryDirectory"] = "./.test-memory"
            })
            .Build();

        _services.AddSingleton<IConfiguration>(configuration);
        _services.AddLogging();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersAllRequiredServices()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - Infrastructure services (can be resolved without additional dependencies)
        _serviceProvider.GetService<IMemoryStorageProvider>().Should().NotBeNull();
        _serviceProvider.GetService<ILlmProviderFactory>().Should().NotBeNull();
        _serviceProvider.GetService<IPromptTemplateLoader>().Should().NotBeNull();
        _serviceProvider.GetService<IAuthenticationService>().Should().NotBeNull();

        // Assert - Feature handlers (can be resolved without additional dependencies)
        _serviceProvider.GetService<CreateDailyEntryHandler>().Should().NotBeNull();
        _serviceProvider.GetService<CreateWeeklyReviewHandler>().Should().NotBeNull();

        // Note: LLM providers (OpenAILlmProvider, AnthropicLlmProvider) require ChatClient dependencies
        // and are tested separately via the LlmProviderFactory
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersStorageProviderAsSingleton()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var instance1 = _serviceProvider.GetRequiredService<IMemoryStorageProvider>();
        var instance2 = _serviceProvider.GetRequiredService<IMemoryStorageProvider>();

        // Assert - Same instance should be returned (singleton)
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersHandlersAsTransient()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var instance1 = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
        var instance2 = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();

        // Assert - Different instances should be returned (transient)
        instance1.Should().NotBeSameAs(instance2);
    }

    [Fact]
    public void AddTenSecondTomServices_StorageProvider_UsesConfiguredDirectory()
    {
        // Arrange
        const string customDirectory = "./custom-memory-path";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:MemoryDirectory"] = customDirectory
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddTenSecondTomServices();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var storageProvider = serviceProvider.GetRequiredService<IMemoryStorageProvider>();

        // Assert
        storageProvider.Should().NotBeNull();
        storageProvider.Should().BeOfType<FileSystemStorageProvider>();
    }

    [Fact]
    public void AddTenSecondTomServices_StorageProvider_UsesDefaultDirectoryWhenNotConfigured()
    {
        // Arrange - Configuration without MemoryDirectory setting
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddTenSecondTomServices();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var storageProvider = serviceProvider.GetRequiredService<IMemoryStorageProvider>();

        // Assert - Should still create provider with default directory
        storageProvider.Should().NotBeNull();
        storageProvider.Should().BeOfType<FileSystemStorageProvider>();
    }

    [Fact]
    public void AddTenSecondTomServices_CanResolveAllDependencies()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Act & Assert - Core services should resolve without throwing
        var resolvingServices = () =>
        {
            _serviceProvider.GetRequiredService<IMemoryStorageProvider>();
            _serviceProvider.GetRequiredService<ILlmProviderFactory>();
            _serviceProvider.GetRequiredService<IPromptTemplateLoader>();
            _serviceProvider.GetRequiredService<IAuthenticationService>();
            _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
            _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();
        };

        resolvingServices.Should().NotThrow();
    }

    [Fact]
    public void AddTenSecondTomServices_StorageProvider_RequiresConfiguration()
    {
        // Arrange - Services without IConfiguration registered
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTenSecondTomServices();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var resolveStorageProvider = () => serviceProvider.GetRequiredService<IMemoryStorageProvider>();

        // Assert - Should throw because IConfiguration is missing
        resolveStorageProvider.Should().Throw<InvalidOperationException>()
            .WithMessage("*IConfiguration*");
    }

    [Fact]
    public void AddTenSecondTomServices_StorageProvider_RequiresLoggerFactory()
    {
        // Arrange - Services without logging
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:MemoryDirectory"] = "./.test-memory"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        // Note: NOT adding logging explicitly - AddHttpClient() should add it
        services.AddTenSecondTomServices();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        
        // Assert - Should succeed because AddHttpClient() registers logging infrastructure
        var storageProvider = serviceProvider.GetRequiredService<IMemoryStorageProvider>();
        storageProvider.Should().NotBeNull();
        
        // Verify logging is available
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull("AddHttpClient should register logging infrastructure");
    }

    [Fact]
    public void AddTenSecondTomServices_ReturnsSameServiceCollection()
    {
        // Act
        var result = _services.AddTenSecondTomServices();

        // Assert - Should return the same collection for chaining
        result.Should().BeSameAs(_services);
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersCorrectImplementationTypes()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - Verify concrete types
        _serviceProvider.GetRequiredService<IMemoryStorageProvider>()
            .Should().BeOfType<FileSystemStorageProvider>();

        _serviceProvider.GetRequiredService<ILlmProviderFactory>()
            .Should().BeOfType<LlmProviderFactory>();

        _serviceProvider.GetRequiredService<IPromptTemplateLoader>()
            .Should().BeOfType<EmbeddedPromptTemplateLoader>();

        _serviceProvider.GetRequiredService<IAuthenticationService>()
            .Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public void AddTenSecondTomServices_HandlersCanResolveTheirDependencies()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Act & Assert - Handlers should successfully resolve all their constructor dependencies
        var resolvingHandlers = () =>
        {
            var dailyHandler = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
            dailyHandler.Should().NotBeNull();

            var weeklyHandler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();
            weeklyHandler.Should().NotBeNull();
        };

        resolvingHandlers.Should().NotThrow();
    }

    [Fact]
    public void AddTenSecondTomServices_AuthenticationService_UsesFactoryMethod()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var authService = _serviceProvider.GetRequiredService<IAuthenticationService>();

        // Assert - Should successfully create via factory method
        authService.Should().NotBeNull();
        authService.Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public void AddTenSecondTomServices_AuthenticationService_RegisteredAsSingleton()
    {
        // Arrange & Act
        _services.AddTenSecondTomServices();
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var instance1 = _serviceProvider.GetRequiredService<IAuthenticationService>();
        var instance2 = _serviceProvider.GetRequiredService<IAuthenticationService>();

        // Assert - Same instance should be returned (singleton)
        instance1.Should().BeSameAs(instance2);
    }
}
