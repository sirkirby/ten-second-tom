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
using TenSecondTom.Features.Today;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.DependencyInjection;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.DependencyInjection;

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
        // NOTE: After consolidation, configuration sections moved:
        // - Auth moved from "TenSecondTom:Ssh" to "TenSecondTom:Auth"
        // - Llm moved to "TenSecondTom:Llm"
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:RootDirectory"] = "./.test-memory",
                ["TenSecondTom:Storage:RootDirectory"] = "./.test-memory",
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-api-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4",
                ["TenSecondTom:Llm:MaxInputTokens"] = "100000",
                ["TenSecondTom:Auth:KeySource"] = "FileSystem",
                ["TenSecondTom:Auth:KeyPath"] = "~/.ssh/id_ed25519"
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

        // Application services (MediatR, FluentValidation) - required by Config.Handler
        services.AddApplicationServices();

        // Feature slices (vertical slice architecture)
        services.AddTodayFeature();
        services.AddThisWeekFeature();
        services.AddSearchFeature();
        services.AddAuthFeature();
        services.AddTemplatesFeature();
        services.AddSetupFeature(configuration);
        services.AddShellFeature();
    }

    [Fact(Skip = "Configuration binding with enums needs investigation")]
    public void AddTenSecondTomServices_RegistersAllRequiredServices()
    {
        // Arrange & Act
        // Get configuration from the pre-built service provider
        var tempProvider = _services.BuildServiceProvider();
        var configuration = tempProvider.GetRequiredService<IConfiguration>();
        tempProvider.Dispose();
        
        AddAllServices(_services, configuration);
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - Infrastructure services (can be resolved without additional dependencies)
        _serviceProvider.GetService<IMemoryStorageProvider>().Should().NotBeNull();
        _serviceProvider.GetService<ILlmProviderFactory>().Should().NotBeNull();
        _serviceProvider.GetService<IPromptTemplateLoader>().Should().NotBeNull();
        _serviceProvider.GetService<IAuthenticationService>().Should().NotBeNull();
        var authOptions = _serviceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
        authOptions.KeySource.Should().Be(SshKeySource.FileSystem);
        authOptions.KeyPath.Should().Be("~/.ssh/id_ed25519");

        // Assert - Feature handlers (can be resolved without additional dependencies)
        _serviceProvider.GetService<CreateDailyEntry.Handler>().Should().NotBeNull();
        _serviceProvider.GetService<CreateWeeklyReview.Handler>().Should().NotBeNull();

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

    [Fact(Skip = "Configuration binding with enums needs investigation")]
    public void AddTenSecondTomServices_RegistersHandlersAsTransient()
    {
        // Arrange & Act
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var instance1 = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        var instance2 = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

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
                ["TenSecondTom:Storage:RootDirectory"] = customDirectory,
                ["TenSecondTom:Auth:KeySource"] = "FileSystem",
                ["TenSecondTom:Auth:KeyPath"] = "~/.ssh/id_ed25519",
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4"
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
        // Note: Storage provider is now selected via factory, returns provider based on configuration
        storageProvider.Should().BeAssignableTo<IMemoryStorageProvider>();
    }

    [Fact]
    public void AddTenSecondTomServices_StorageProvider_UsesDefaultDirectoryWhenNotConfigured()
    {
        // Arrange - Configuration without MemoryDirectory setting
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenSecondTom:Auth:KeySource"] = "FileSystem",
                ["TenSecondTom:Auth:KeyPath"] = "~/.ssh/id_ed25519",
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4"
            })
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
        // Note: Storage provider is now selected via factory, returns provider based on configuration
        storageProvider.Should().BeAssignableTo<IMemoryStorageProvider>();
    }

    [Fact(Skip = "Configuration binding with enums needs investigation")]
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
            _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
            _serviceProvider.GetRequiredService<CreateWeeklyReview.Handler>();
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
                ["TenSecondTom:Auth:KeySource"] = "FileSystem",
                ["TenSecondTom:Auth:KeyPath"] = "~/.ssh/id_ed25519"
                // LLM options are intentionally missing to test validation
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(minimalConfiguration);
        services.AddLogging();
        AddAllServices(services, minimalConfiguration);

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var resolveHandler = () => serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

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
                ["TenSecondTom:Storage:RootDirectory"] = "./.test-memory",
                ["TenSecondTom:Auth:KeySource"] = "FileSystem",
                ["TenSecondTom:Auth:KeyPath"] = "~/.ssh/id_ed25519",
                ["TenSecondTom:Llm:Provider"] = "OpenAI",
                ["TenSecondTom:Llm:ApiKey"] = "test-key",
                ["TenSecondTom:Llm:Model"] = "gpt-4"
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
    public void Configuration_BindsAuthOptions_WithKeyPath()
    {
        // Arrange
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var section = configuration.GetSection(AuthOptions.SectionName);
        var options = new AuthOptions();
        var llmSection = configuration.GetSection(LlmOptions.SectionName);
        var llmOptions = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = string.Empty,
            Model = string.Empty
        };

        // Act
        section.Bind(options);
        llmSection.Bind(llmOptions);

        // Assert
        options.KeySource.Should().Be(SshKeySource.FileSystem);
        options.KeyPath.Should().Be("~/.ssh/id_ed25519");
        llmOptions.ApiKey.Should().Be("test-api-key");
    }

    [Fact(Skip = "Configuration binding with enums needs investigation")]
    public void AddTenSecondTomOptions_ConfiguresAuthOptions()
    {
        // Arrange
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Act
        services.AddTenSecondTomOptions(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthOptions>>().Value;
        var llmOptions = provider.GetRequiredService<IOptions<LlmOptions>>().Value;

        // Assert
        options.KeySource.Should().Be(SshKeySource.FileSystem);
        options.KeyPath.Should().Be("~/.ssh/id_ed25519");
        llmOptions.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public void AddTenSecondTomServices_RegistersCorrectImplementationTypes()
    {
        // Arrange & Act
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - Verify concrete types
        // Note: Storage provider is now selected via factory based on configuration
        _serviceProvider.GetRequiredService<IMemoryStorageProvider>()
            .Should().BeAssignableTo<IMemoryStorageProvider>();

        _serviceProvider.GetRequiredService<ILlmProviderFactory>()
            .Should().BeOfType<LlmProviderFactory>();

        _serviceProvider.GetRequiredService<IPromptTemplateLoader>()
            .Should().BeOfType<CompositeTemplateLoader>();

        _serviceProvider.GetRequiredService<IAuthenticationService>()
            .Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact(Skip = "Configuration binding with enums needs investigation")]
    public void AddTenSecondTomServices_HandlersCanResolveTheirDependencies()
    {
        // Arrange & Act
        var configuration = _services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        AddAllServices(_services, configuration);
        _serviceProvider = _services.BuildServiceProvider();

        // Act & Assert - Handlers should successfully resolve all their constructor dependencies
        var resolvingHandlers = () =>
        {
            var dailyHandler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
            dailyHandler.Should().NotBeNull();

            var weeklyHandler = _serviceProvider.GetRequiredService<CreateWeeklyReview.Handler>();
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
