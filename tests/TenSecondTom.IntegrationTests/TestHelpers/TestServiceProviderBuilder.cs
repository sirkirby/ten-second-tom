using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.IntegrationTests.TestHelpers;

/// <summary>
/// Builder for creating test service providers with mocked dependencies.
/// </summary>
public sealed class TestServiceProviderBuilder
{
    private readonly ServiceCollection _services;
    private IConfiguration? _configuration;
    private ILlmProvider? _llmProvider;
    private IAuthenticationService? _authService;
    private IInteractiveTextEditor? _textEditor;
    private string _memoryBasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestServiceProviderBuilder"/> class.
    /// </summary>
    public TestServiceProviderBuilder()
    {
        _services = new ServiceCollection();
        _memoryBasePath = Path.Combine(Path.GetTempPath(), "ten-second-tom-tests", Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Sets a custom configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use.</param>
    /// <returns>The builder for chaining.</returns>
    public TestServiceProviderBuilder WithConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
        return this;
    }

    /// <summary>
    /// Sets a custom LLM provider.
    /// </summary>
    /// <param name="llmProvider">The LLM provider to use.</param>
    /// <returns>The builder for chaining.</returns>
    public TestServiceProviderBuilder WithLlmProvider(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
        return this;
    }

    /// <summary>
    /// Sets a custom authentication service.
    /// </summary>
    /// <param name="authService">The authentication service to use.</param>
    /// <returns>The builder for chaining.</returns>
    public TestServiceProviderBuilder WithAuthenticationService(IAuthenticationService authService)
    {
        _authService = authService;
        return this;
    }

    /// <summary>
    /// Sets a custom text editor service.
    /// </summary>
    /// <param name="textEditor">The text editor to use.</param>
    /// <returns>The builder for chaining.</returns>
    public TestServiceProviderBuilder WithTextEditor(IInteractiveTextEditor textEditor)
    {
        _textEditor = textEditor;
        return this;
    }

    /// <summary>
    /// Sets the memory base path for file storage.
    /// </summary>
    /// <param name="basePath">The base path for .memory directory.</param>
    /// <returns>The builder for chaining.</returns>
    public TestServiceProviderBuilder WithMemoryBasePath(string basePath)
    {
        _memoryBasePath = basePath;
        return this;
    }

    /// <summary>
    /// Builds the service provider with all configured services.
    /// </summary>
    /// <returns>The configured service provider.</returns>
    public ServiceProvider Build()
    {
        // Configuration
        if (_configuration == null)
        {
            var configBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenSecondTom:Storage:RootDirectory"] = _memoryBasePath,
                    ["TenSecondTom:Auth:KeySource"] = "FileSystem",
                    ["TenSecondTom:Auth:KeyPath"] = "~/.ssh/id_ed25519",
                    ["TenSecondTom:Llm:Provider"] = "OpenAI",
                    ["TenSecondTom:Llm:ApiKey"] = "test-key",
                    ["TenSecondTom:Llm:Model"] = "gpt-4",
                    ["TenSecondTom:DefaultProvider"] = "MockProvider",
                    ["DOTNET_ENVIRONMENT"] = "Development" // Set to development for MockAuthenticationService
                });
            _configuration = configBuilder.Build();
        }

        _services.AddSingleton(_configuration);

        // Logging (use console for test output visibility)
        _services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise in test output
            builder.AddConsole();
        });

        // Add infrastructure and feature services
        _services.AddInfrastructureServices();
        _services.AddApplicationServices();  // Registers MediatR and FluentValidation
        _services.AddTodayFeature();
        _services.AddThisWeekFeature();
        _services.AddSearchFeature();
        _services.AddAuthFeature();
        _services.AddTemplatesFeature();
        _services.AddSetupFeature(_configuration);
        _services.AddShellFeature();

        // Override with mocked services if provided
        if (_llmProvider != null)
        {
            // Remove registered LLM providers and add mock
            var llmDescriptors = _services.Where(d => d.ServiceType == typeof(ILlmProvider)).ToList();
            foreach (var descriptor in llmDescriptors)
            {
                _services.Remove(descriptor);
            }
            _services.AddSingleton(_llmProvider);
        }

        if (_authService != null)
        {
            // Remove registered auth service and add custom
            var authDescriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IAuthenticationService));
            if (authDescriptor != null)
            {
                _services.Remove(authDescriptor);
            }
            _services.AddSingleton(_authService);
        }

        if (_textEditor != null)
        {
            // Remove registered text editor and add custom
            var editorDescriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(IInteractiveTextEditor));
            if (editorDescriptor != null)
            {
                _services.Remove(editorDescriptor);
            }
            _services.AddSingleton(_textEditor);
        }

        return _services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a default test service provider with mocked dependencies.
    /// </summary>
    /// <returns>A configured service provider for testing.</returns>
    public static ServiceProvider CreateDefault()
    {
        return new TestServiceProviderBuilder()
            .WithLlmProvider(MockLlmProvider.WithDailySummaryResponse())
            .Build();
    }
}
