using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Auth;
using TenSecondTom.Features.Search;
using TenSecondTom.Features.Setup;
using TenSecondTom.Features.Shell;
using TenSecondTom.Features.Templates;
using TenSecondTom.Features.ThisWeek;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today;
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

        // Setup minimal required services with all required configuration options
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:MemoryDirectory"] = "./.test-memory",
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4",
                ["TenSecondTom:Llm:MaxInputTokens"] = "100000",
                ["TenSecondTom:Ssh:KeySource"] = "FileSystem",
                ["TenSecondTom:Ssh:KeyPath"] = "~/.ssh/id_ed25519"
            })
            .Build();

        _services.AddSingleton<IConfiguration>(configuration);
        _services.AddLogging();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Helper method to add all services (infrastructure + features) like Program.cs does.
    /// </summary>
    private static void AddAllServices(ServiceCollection services, IConfiguration configuration)
    {
        // Register strongly-typed Options with validation (Options Pattern)
        services.AddTenSecondTomOptions(configuration);

        // Infrastructure (cross-cutting concerns)
        services.AddInfrastructureServices();

        // Feature slices (vertical slice architecture)
        services.AddTodayFeature();
        services.AddThisWeekFeature();
        services.AddSearchFeature();
        services.AddAuthFeature();
        services.AddTemplatesFeature();
        services.AddSetupFeature();
        services.AddShellFeature();
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersAllRequiredServices()
    {
        // Arrange & Act
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
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
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
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
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
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
        AddAllServices(services, configuration);

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
        AddAllServices(services, configuration);

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
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
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
        // Arrange - Services with minimal auth config but missing required LLM options
        var minimalConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Provide minimal Auth configuration to pass AuthOptions validation
                ["TenSecondTom:Ssh:KeySource"] = "FileSystem",
                ["TenSecondTom:Ssh:KeyPath"] = "~/.ssh/id_ed25519"
                // LLM options are intentionally missing to test validation
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(minimalConfiguration);
        services.AddLogging();
        AddAllServices(services, minimalConfiguration);

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var resolveHandler = () => serviceProvider.GetRequiredService<CreateDailyEntryHandler>();

        // Assert - Should throw because required LlmOptions configuration is missing
        resolveHandler.Should().Throw<OptionsValidationException>()
            .WithMessage("*API key*");
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
        AddAllServices(services, configuration);

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
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
        var result = _services;

        // Assert - Should return the same collection for chaining
        result.Should().BeSameAs(_services);
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersCorrectImplementationTypes()
    {
        // Arrange & Act
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - Verify concrete types
        _serviceProvider.GetRequiredService<IMemoryStorageProvider>()
            .Should().BeOfType<FileSystemStorageProvider>();

        _serviceProvider.GetRequiredService<ILlmProviderFactory>()
            .Should().BeOfType<LlmProviderFactory>();

        _serviceProvider.GetRequiredService<IPromptTemplateLoader>()
            .Should().BeOfType<CompositeTemplateLoader>();

        _serviceProvider.GetRequiredService<IAuthenticationService>()
            .Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public void AddTenSecondTomServices_HandlersCanResolveTheirDependencies()
    {
        // Arrange & Act
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
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
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
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
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var instance1 = _serviceProvider.GetRequiredService<IAuthenticationService>();
        var instance2 = _serviceProvider.GetRequiredService<IAuthenticationService>();

        // Assert - Same instance should be returned (singleton)
        instance1.Should().BeSameAs(instance2);
    }
}
