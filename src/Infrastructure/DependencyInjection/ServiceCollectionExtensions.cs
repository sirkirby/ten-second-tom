using System.IO.Abstractions;
using Anthropic.SDK;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using TenSecondTom.Features.Auth.Handlers;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Queries;
using TenSecondTom.Features.Setup.Validation;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Features.Templates.Services;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Auth.SshProviders;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API for dependency injection")]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Ten Second Tom services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTenSecondTomServices(this IServiceCollection services)
    {
        // Add HttpClient support for API validators
        services.AddHttpClient();

        // Infrastructure services
        services.AddSingleton<IFileSystem, FileSystem>();

        services.AddSingleton<IMemoryStorageProvider>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<FileSystemStorageProvider>();

            string baseDirectory = configuration["TenSecondTom:MemoryDirectory"] ?? "./.memory";

            return new FileSystemStorageProvider(baseDirectory, logger);
        });

        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();

        // Register YAML parser for template metadata
        services.AddSingleton<YamlFrontMatterParser>();

        // Register template loader with YAML parser dependency
        services.AddSingleton<IPromptTemplateLoader>(serviceProvider =>
        {
            var yamlParser = serviceProvider.GetRequiredService<YamlFrontMatterParser>();
            return new EmbeddedPromptTemplateLoader(baseDirectory: null, yamlParser: yamlParser);
        });
        
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

            // Use factory to intelligently select authentication method
            var agentClient = serviceProvider.GetRequiredService<ISshAgentClient>();
            var sshAgentLogger = loggerFactory.CreateLogger<SshAgentAuthenticationService>();
            var sshKeyLogger = loggerFactory.CreateLogger<SshKeyAuthenticationService>();
            
            return AuthenticationServiceFactory.Create(
                configuration,
                agentClient,
                sshAgentLogger,
                sshKeyLogger);
        });

        // Register OpenAI ChatClient (lazy - only instantiated when OpenAI provider is actually used)
        services.AddTransient<ChatClient>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            
            // Use standard .NET configuration hierarchy: appsettings → user secrets → environment variables
            // Configuration system handles priority automatically (Llm:ApiKey or Llm__ApiKey env var)
            string? apiKey = configuration["Llm:ApiKey"];
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key not configured. Run 'tom setup' to configure your API key, " +
                    "or set Llm__ApiKey environment variable.");
            }

            string model = configuration["Llm:Model"] ?? 
                          configuration["TenSecondTom:OpenAI:Model"] ?? 
                          "gpt-4o";
            var openAIClient = new OpenAIClient(apiKey);
            return openAIClient.GetChatClient(model);
        });

        // Register Anthropic AnthropicClient (lazy - only instantiated when Anthropic provider is actually used)
        services.AddTransient<AnthropicClient>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            
            // Use standard .NET configuration hierarchy: appsettings → user secrets → environment variables
            // Configuration system handles priority automatically (Llm:ApiKey or Llm__ApiKey env var)
            string? apiKey = configuration["Llm:ApiKey"];
            
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
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var chatClient = serviceProvider.GetRequiredService<ChatClient>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<OpenAILlmProvider>();
            
            // Get configured model or use default from ModelRegistry
            string? configuredModel = configuration["Llm:Model"];
            string model = !string.IsNullOrWhiteSpace(configuredModel)
                ? configuredModel
                : TenSecondTom.Features.Setup.Models.ModelRegistry.GetDefault(
                    TenSecondTom.Features.Setup.Models.LlmProvider.OpenAI).Id;
            
            return new OpenAILlmProvider(chatClient, logger, model);
        });

        services.AddTransient<AnthropicLlmProvider>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var client = serviceProvider.GetRequiredService<AnthropicClient>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<AnthropicLlmProvider>();
            
            // Get configured model or use default from ModelRegistry
            string? configuredModel = configuration["Llm:Model"];
            string model = !string.IsNullOrWhiteSpace(configuredModel)
                ? configuredModel
                : TenSecondTom.Features.Setup.Models.ModelRegistry.GetDefault(
                    TenSecondTom.Features.Setup.Models.LlmProvider.Anthropic).Id;
            
            return new AnthropicLlmProvider(client, logger, model);
        });

        // Feature handlers
        services.AddTransient<CreateDailyEntryHandler>();
        services.AddTransient<CreateWeeklyReviewHandler>();
        services.AddTransient<SearchMemoriesQueryHandler>();
        services.AddTransient<LoginCommandHandler>();
        services.AddTransient<LogoutCommandHandler>();

        // Templates feature
        services.AddTransient<InstallDefaultTemplatesHandler>();
        services.AddTransient<Features.Templates.Handlers.IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>>>(
            sp => sp.GetRequiredService<InstallDefaultTemplatesHandler>());
        services.AddTransient<TemplateMigrationService>();

        // Setup feature services
        services.AddTransient<SetupCommandHandler>();
        services.AddTransient<ConfigCommandHandler>();
        
        // SSH Key Detectors - registered as both concrete types and interface for factory injection
        services.AddTransient<ISshKeyDetector, SystemSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, OnePasswordSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, SecretiveSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, FileSystemSshKeyDetector>();
        services.AddSingleton<ISshKeyDetectorFactory, SshKeyDetectorFactory>();
        
        // API Key Validators
        services.AddTransient<IApiKeyValidator, OpenAIApiKeyValidator>();
        services.AddTransient<IApiKeyValidator, AnthropicApiKeyValidator>();
        
        // Configuration Storage
        services.AddSingleton<IConfigurationStorageService, UserSecretsStorageService>();
        
        // Spectre.Console AnsiConsole for rich terminal UI
        services.AddSingleton<Spectre.Console.IAnsiConsole>(Spectre.Console.AnsiConsole.Console);
        
        // Setup Wizard UI
        services.AddTransient<ISetupWizardUI, SpectreConsoleSetupWizard>();

        // Shell services (Singletons for session persistence during app lifetime)
        services.AddSingleton<IReplLoop, ReplLoop>();
        services.AddSingleton<ICommandRouter, CommandRouter>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IAutocompleteEngine, AutocompleteEngine>();
        services.AddSingleton<IOutputPaginator, OutputPaginator>();

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
                #pragma warning disable CA1848 // Use LoggerMessage delegates for performance
                var generalLogger = logger.CreateLogger("EditorSelection");
                generalLogger.LogInformation(
                    "Using StreamBasedTextEditor directly (IsInputRedirected={IsRedirected}, TERM={Term})",
                    Console.IsInputRedirected,
                    Environment.GetEnvironmentVariable("TERM") ?? "not set"
                );
                #pragma warning restore CA1848
                return new StreamBasedTextEditor(sanitizer, streamLogger);
            }

            // Use FallbackTextEditor wrapper - tries Terminal.Gui, falls back to StreamBased on failure
            #pragma warning disable CA1848 // Use LoggerMessage delegates for performance
            var selectionLogger = logger.CreateLogger("EditorSelection");
            selectionLogger.LogInformation(
                "Using FallbackTextEditor (will try Terminal.Gui, fallback to StreamBased if needed)"
            );
            #pragma warning restore CA1848
            
            var primaryEditor = serviceProvider.GetRequiredService<TerminalGuiTextEditor>();
            var fallbackEditor = serviceProvider.GetRequiredService<StreamBasedTextEditor>();
            var fallbackLogger = logger.CreateLogger<FallbackTextEditor>();
            
            return new FallbackTextEditor(primaryEditor, fallbackEditor, fallbackLogger);
        });

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
