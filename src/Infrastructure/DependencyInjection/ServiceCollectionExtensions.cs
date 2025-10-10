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
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Auth.SshProviders;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;

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
        services.AddSingleton<IMemoryStorageProvider>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<FileSystemStorageProvider>();
            
            string baseDirectory = configuration["TenSecondTom:MemoryDirectory"] ?? "./.memory";
            
            return new FileSystemStorageProvider(baseDirectory, logger);
        });
        
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<IPromptTemplateLoader, EmbeddedPromptTemplateLoader>();
        
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

        // Register OpenAI ChatClient
        services.AddSingleton<ChatClient>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            string? apiKey = configuration["OPENAI_API_KEY"] ?? 
                            Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key not configured. Set OPENAI_API_KEY environment variable or add to configuration.");
            }

            string model = configuration["TenSecondTom:OpenAI:Model"] ?? "gpt-4o";
            var openAIClient = new OpenAIClient(apiKey);
            return openAIClient.GetChatClient(model);
        });

        // Register Anthropic AnthropicClient
        services.AddSingleton<AnthropicClient>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            string? apiKey = configuration["ANTHROPIC_API_KEY"] ?? 
                            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            
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
            
            string model = configuration["TenSecondTom:OpenAI:Model"] ?? "gpt-4o";
            return new OpenAILlmProvider(chatClient, logger, model);
        });

        services.AddTransient<AnthropicLlmProvider>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var client = serviceProvider.GetRequiredService<AnthropicClient>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<AnthropicLlmProvider>();
            
            string model = configuration["TenSecondTom:Anthropic:Model"] ?? "claude-3-5-sonnet-20241022";
            return new AnthropicLlmProvider(client, logger, model);
        });

        // Feature handlers
        services.AddTransient<CreateDailyEntryHandler>();
        services.AddTransient<CreateWeeklyReviewHandler>();
        services.AddTransient<SearchMemoriesQueryHandler>();
        services.AddTransient<LoginCommandHandler>();
        services.AddTransient<LogoutCommandHandler>();

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

        return services;
    }
}
