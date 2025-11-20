using System;
using System.IO.Abstractions;
using Anthropic.SDK;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using TenSecondTom.Shared.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Auth.SshProviders;
using TenSecondTom.Infrastructure.Behaviors;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Infrastructure.Templates;
using TenSecondTom.Shared.Abstractions.Templates;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Options.Validation;
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers strongly-typed Options classes and their validators using the .NET Options Pattern.
    /// This replaces stringly-typed IConfiguration access with compile-time safe options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTenSecondTomOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register LlmOptions with validation
        // NOTE: Don't use ValidateOnStart() - allow unconfigured state during first-time setup
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();

        // Register AuthOptions with validation
        // NOTE: Don't use ValidateOnStart() - allow unconfigured state during first-time setup
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();

        // Register StorageOptions with custom binding (RootDirectory is at root, other properties in Storage section)
        services.Configure<StorageOptions>(options =>
        {
            // Root directory is at root level: TenSecondTom:RootDirectory (or legacy MemoryDirectory)
            options.RootDirectory = configuration[ConfigurationKeys.RootDirectoryKey]
                ?? configuration[ConfigurationKeys.MemoryDirectoryKey]  // Legacy fallback
                ?? Path.Combine(".", DirectoryNames.ApplicationRoot);

            // Other properties are in Storage section: TenSecondTom:Storage
            var storageSection = configuration.GetSection(StorageOptions.SectionName);
            storageSection.Bind(options);

            // Backward compatibility: if legacy MemoryDirectory was explicitly set, preserve it
            if (configuration[ConfigurationKeys.MemoryDirectoryKey] != null)
            {
                options.MemoryDirectory = configuration[ConfigurationKeys.MemoryDirectoryKey];
            }
        });
        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

        // Register AppOptions (no validation needed - all have defaults)
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

        // Register AudioConfiguration (existing - keep as-is)
        services.AddOptions<Configuration.AudioConfiguration>()
            .BindConfiguration(ConfigurationKeys.AudioSectionKey)
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Adds infrastructure services (cross-cutting concerns) to the service collection.
    /// Feature-specific services should be registered using their respective feature extension methods.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Options registration is now handled by AddTenSecondTomOptions()
        // Keeping AudioConfiguration here for backward compatibility during migration

        // Register infrastructure subsystems
        services.AddAuthenticationInfrastructure();
        services.AddConfigurationInfrastructure();

        // Add HttpClient support for API validators
        services.AddHttpClient();

        // Infrastructure services
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<ConfigurationChecker>();

        // Register storage provider factory (assembly scanning)
        services.AddSingleton<IStorageProviderFactory, StorageProviderFactory>();

        // Register IStorageProvider (resolved via factory based on configuration)
        services.AddSingleton<IStorageProvider>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IStorageProviderFactory>();
            var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
            var logger = serviceProvider.GetRequiredService<ILogger<IStorageProvider>>();

            string providerId = options.Value.ProviderId;

            logger.LogInformation("Creating storage provider: {ProviderId}", providerId);

            var result = factory.CreateProvider(providerId);

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to create storage provider '{ProviderId}': {Error}. Falling back to default provider.",
                    providerId, result.Error);

                // Fallback to default provider
                result = factory.CreateProvider(StorageProviderIds.Default);

                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Failed to create default storage provider: {result.Error}");
                }
            }

            var provider = result.Value;

            // Initialize provider synchronously (acceptable for DI registration at startup)
            var initResult = provider.InitializeAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (!initResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Storage provider initialization failed: {initResult.Error}");
            }

            return provider;
        });

        // Register IMemoryStorageProvider as alias to IStorageProvider (backward compatibility)
        services.AddSingleton<IMemoryStorageProvider>(sp => sp.GetRequiredService<IStorageProvider>());

        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        // Register YAML parser for template metadata
        services.AddSingleton<YamlFrontMatterParser>();

        // Register template loaders with fallback chain: FileSystem → Embedded
        // This provides resilient template loading with graceful degradation
        services.AddSingleton<IPromptTemplateLoader>(serviceProvider =>
        {
            var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
            var yamlParser = serviceProvider.GetRequiredService<YamlFrontMatterParser>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            // Templates are in the configured root directory under templates/ subdirectory
            // TenSecondTom:RootDirectory is the root (e.g., ~/ten-second-tom or ./.memory)
            // Structure: {root}/templates/, {root}/today/, {root}/thisweek/
            string? rootDirectory = storageOptions.Value.RootDirectory;

            // Backward compatibility: fall back to MemoryDirectory
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = storageOptions.Value.MemoryDirectory;
            }

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = Path.Combine(".", DirectoryNames.ApplicationRoot);
            }

            string templatesDirectory = Path.Combine(rootDirectory, DirectoryNames.Templates);

            // Create FileSystem loader (primary)
            var fileSystemLogger = loggerFactory.CreateLogger<FileSystemTemplateLoader>();
            var fileSystemLoader = new FileSystemTemplateLoader(
                templatesDirectory,
                yamlParser,
                fileSystemLogger);

            // Create Embedded loader (fallback)
            var embeddedLoader = new EmbeddedPromptTemplateLoader(
                baseDirectory: rootDirectory,
                yamlParser: yamlParser);

            // Create Composite loader with fallback chain
            var compositeLogger = loggerFactory.CreateLogger<CompositeTemplateLoader>();
            return new CompositeTemplateLoader(
                fileSystemLoader,
                embeddedLoader,
                compositeLogger);
        });

        // Register EmbeddedPromptTemplateLoader separately for direct injection
        // (e.g., InstallDefaultTemplates needs direct access to embedded templates)
        services.AddSingleton<EmbeddedPromptTemplateLoader>(serviceProvider =>
        {
            var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
            var yamlParser = serviceProvider.GetRequiredService<YamlFrontMatterParser>();

            string? rootDirectory = storageOptions.Value.RootDirectory;

            // Backward compatibility: fall back to MemoryDirectory
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = storageOptions.Value.MemoryDirectory;
            }

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = Path.Combine(".", DirectoryNames.ApplicationRoot);
            }

            return new EmbeddedPromptTemplateLoader(
                baseDirectory: rootDirectory,
                yamlParser: yamlParser);
        });

        services.AddSingleton<ITemplateInstaller, TemplateInstaller>();

        // Register template provider abstraction
        // This decouples features from Templates feature by providing infrastructure-level template access
        services.AddSingleton<ITemplateProvider, TemplateProvider>();

        // Register SSH agent client
        services.AddSingleton<ISshAgentClient>(serviceProvider =>
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<SshAgentClient>();
            return new SshAgentClient(logger);
        });
        
        // Register authentication service (uses factory to select implementation)
        services.AddSingleton<IAuthenticationService>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            if (EnvironmentHelper.IsDevelopment(configuration))
            {
                var mockLogger = loggerFactory.CreateLogger<MockAuthenticationService>();
                return new MockAuthenticationService(mockLogger);
            }

            // Use factory with AuthOptions to intelligently select authentication method
            var authOptions = serviceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
            var agentClient = serviceProvider.GetRequiredService<ISshAgentClient>();
            var sshAgentLogger = loggerFactory.CreateLogger<SshAgentAuthenticationService>();
            var sshKeyLogger = loggerFactory.CreateLogger<SshKeyAuthenticationService>();

            // Note: Using GetAwaiter().GetResult() here is acceptable for DI registration
            // since this only runs once during application startup
            return AuthenticationServiceFactory.CreateAsync(
                authOptions,
                agentClient,
                sshAgentLogger,
                sshKeyLogger).GetAwaiter().GetResult();
        });

        // Register OpenAI ChatClient (lazy - only instantiated when OpenAI provider is actually used)
        services.AddTransient<ChatClient>(serviceProvider =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();

            string? apiKey = llmOptions.Value.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key not configured. Run 'tom setup' to configure your API key, " +
                    "or set TenSecondTom__Llm__ApiKey environment variable.");
            }

            string model = llmOptions.Value.Model ?? LlmConstants.OpenAIModels.GPTNano;
            var openAIClient = new OpenAIClient(apiKey);
            return openAIClient.GetChatClient(model);
        });

        // Register Anthropic AnthropicClient (lazy - only instantiated when Anthropic provider is actually used)
        services.AddTransient<AnthropicClient>(serviceProvider =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();

            string? apiKey = llmOptions.Value.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Don't throw - allow app to run if only using OpenAI
                // Return a dummy client that will fail if actually used
                return new AnthropicClient();
            }

            return new AnthropicClient(apiKey);
        });

        // LLM providers (now with dependencies)
        services.AddTransient<OpenAILlmProvider>(serviceProvider =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();
            var chatClient = serviceProvider.GetRequiredService<ChatClient>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<OpenAILlmProvider>();

            // Get configured model or use default from ModelRegistry
            string? configuredModel = llmOptions.Value.Model;
            string model = !string.IsNullOrWhiteSpace(configuredModel)
                ? configuredModel
                : ModelRegistry.GetDefault(LlmProvider.OpenAI).Id;

            return new OpenAILlmProvider(chatClient, logger, model);
        });

        services.AddTransient<AnthropicLlmProvider>(serviceProvider =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();
            var client = serviceProvider.GetRequiredService<AnthropicClient>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<AnthropicLlmProvider>();

            // Get configured model or use default from ModelRegistry
            string? configuredModel = llmOptions.Value.Model;
            string model = !string.IsNullOrWhiteSpace(configuredModel)
                ? configuredModel
                : ModelRegistry.GetDefault(LlmProvider.Anthropic).Id;

            return new AnthropicLlmProvider(client, logger, model);
        });

        services.AddTransient<LocalOpenAiCompatibleLlmProvider>(serviceProvider =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<LocalOpenAiCompatibleLlmProvider>();

            // Configure extended timeout for local LLMs (they can take 10+ minutes for long recordings)
            // Default HttpClient timeout is 100 seconds, which is insufficient
            httpClient.Timeout = TimeSpan.FromMinutes(15);

            // Get configured model or use default
            string? configuredModel = llmOptions.Value.Model;
            string model = !string.IsNullOrWhiteSpace(configuredModel)
                ? configuredModel
                : "local-model"; // Default fallback

            // Get configured base URL or use default
            string baseUrl = "http://127.0.0.1:8080/v1"; // Default
            if (llmOptions.Value.Providers.TryGetValue("LocalOpenAiCompatible", out var providerConfig) &&
                providerConfig.TryGetValue("BaseUrl", out var configuredBaseUrl))
            {
                baseUrl = configuredBaseUrl;
            }

            return new LocalOpenAiCompatibleLlmProvider(httpClient, logger, model, baseUrl);
        });

        // Spectre.Console AnsiConsole for rich terminal UI
        services.AddSingleton<Spectre.Console.IAnsiConsole>(Spectre.Console.AnsiConsole.Console);

        // Template selection UI (T045/T046)
        services.AddTransient<ITemplateSelectionUI, TemplateSelectionUI>();

        // Text editing services (T028)
        services.AddSingleton<InputSanitizer>();
        
        // Register both editor implementations
        services.AddTransient<TerminalGuiTextEditor>();
        services.AddTransient<StreamBasedTextEditor>();
        
        services.AddTransient<IInteractiveTextEditor>(serviceProvider =>
        {
            var sanitizer = serviceProvider.GetRequiredService<InputSanitizer>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>();

            // Check if we should use fallback editor directly (skip Terminal.Gui attempt)
            bool useStreamBased = Console.IsInputRedirected 
                || Environment.GetEnvironmentVariable("TERM") == "dumb"
                || !IsInteractiveTerminal();

            if (useStreamBased)
            {
                var streamLogger = logger.CreateLogger<StreamBasedTextEditor>();
                var generalLogger = logger.CreateLogger("EditorSelection");
                generalLogger.LogDebug(
                    "Using StreamBasedTextEditor directly (IsInputRedirected={IsRedirected}, TERM={Term})",
                    Console.IsInputRedirected,
                    Environment.GetEnvironmentVariable("TERM") ?? "not set"
                );
                return new StreamBasedTextEditor(sanitizer, streamLogger);
            }

            // Use FallbackTextEditor wrapper - tries Terminal.Gui, falls back to StreamBased on failure
            var selectionLogger = logger.CreateLogger("EditorSelection");
            selectionLogger.LogDebug(
                "Text editor initialized with Terminal.Gui (StreamBased fallback available if needed)"
            );
            
            var primaryEditor = serviceProvider.GetRequiredService<TerminalGuiTextEditor>();
            var fallbackEditor = serviceProvider.GetRequiredService<StreamBasedTextEditor>();
            var fallbackLogger = logger.CreateLogger<FallbackTextEditor>();
            
            return new FallbackTextEditor(primaryEditor, fallbackEditor, fallbackLogger);
        });

        return services;
    }

    /// <summary>
    /// Registers application services using assembly scanning for automatic discovery.
    /// This includes MediatR handlers, FluentValidation validators, and pipeline behaviors.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Assembly scanning eliminates manual handler/validator registration. To add new features:
    /// 1. Create a handler implementing IRequestHandler&lt;TRequest, TResponse&gt;
    /// 2. Create a validator inheriting AbstractValidator&lt;TRequest&gt; (optional)
    /// 3. MediatR and FluentValidation will automatically discover and register them.
    ///
    /// Pipeline behaviors execute in registration order:
    /// 1. RequestLoggingPipelineBehavior - Logs all requests (outermost)
    /// 2. ValidationPipelineBehavior - Validates input (before handler)
    /// 3. Handler - Executes business logic
    ///
    /// MediatR License: For distributed CLI applications like Ten Second Tom, the MediatR
    /// license warning is suppressed via logging configuration (see appsettings.json).
    /// This is the recommended approach per MediatR documentation for client applications.
    /// </remarks>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register MediatR with assembly scanning and pipeline behaviors
        // Scans the main application assembly containing all features
        //
        // License Note: The MediatR license warning is suppressed via Serilog configuration
        // in appsettings.json ("LuckyPennySoftware.MediatR.License": "None"). This is the
        // recommended approach for distributed client applications per MediatR documentation.
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

            // Register pipeline behaviors in execution order
            // Behaviors execute in the order they're registered
            config.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>)); // Outermost - logs everything
            config.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));     // Inner - validates before handler
        });

        // Register FluentValidation with assembly scanning to auto-discover all validators
        // includeInternalTypes: true allows validators to be internal for better encapsulation
        services.AddValidatorsFromAssembly(
            typeof(ServiceCollectionExtensions).Assembly,
            includeInternalTypes: true);

        return services;
    }

    /// <summary>
    /// Checks if the current terminal supports interactive TUI applications.
    /// </summary>
    private static bool IsInteractiveTerminal()
    {
        // Check if stdin/stdout are both console
        if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            return true;
        }

        return false;
    }
}
