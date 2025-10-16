using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.DependencyInjection;
using TenSecondTom.Infrastructure.Logging;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Secrets;

namespace TenSecondTom;

/// <summary>
/// Entry point for the Ten Second Tom CLI application.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for errors).</returns>
    public static async Task<int> Main(string[] args)
    {
        ILoggerFactory? loggerFactory = null;
        CancellationTokenSource? cancellationTokenSource = null;
        
        try
        {
            // Setup global Ctrl+C handler
            cancellationTokenSource = new CancellationTokenSource();
            bool firstCancellation = true;
            
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                if (firstCancellation)
                {
                    // First Ctrl+C: Cancel gracefully
                    eventArgs.Cancel = true; // Prevent immediate termination
                    cancellationTokenSource.Cancel();
                    firstCancellation = false;
                    Console.Error.WriteLine("\nCancelling... Press Ctrl+C again to force exit.");
                }
                else
                {
                    // Second Ctrl+C: Force exit
                    eventArgs.Cancel = false; // Allow default behavior (immediate termination)
                }
            };

            // Load .env file if it exists (for development configuration)
            string envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (File.Exists(envFilePath))
            {
                Env.Load(envFilePath);
            }

            // Build configuration
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{EnvironmentHelper.GetCurrentEnvironment()}.json", optional: true, reloadOnChange: true);
            
            // Add User Secrets explicitly (for self-contained/trimmed binaries)
            // This doesn't rely on assembly reflection like AddUserSecrets<T>()
            string userSecretsPath = SecretsHelper.GetUserSecretsPath(ConfigurationKeys.UserSecretsId);
            if (File.Exists(userSecretsPath))
            {
                configurationBuilder.AddJsonFile(userSecretsPath, optional: true, reloadOnChange: true);
            }
            
            var configuration = configurationBuilder
                .AddEnvironmentVariables() // Load all environment variables
                .AddCommandLine(args)
                .Build();

            // Configure logging
            loggerFactory = LoggingConfiguration.ConfigureLogging(configuration);
            var logger = loggerFactory.CreateLogger("TenSecondTom.Program");

            logger.LogInformation("Ten Second Tom starting");
            
            // Build DI container
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(loggerFactory);
            services.AddLogging(); // Add logging services to enable ILogger<T> resolution
            
            // Infrastructure (cross-cutting concerns)
            services.AddInfrastructureServices();
            
            // Feature slices (vertical slice architecture)
            services.AddAllFeatures();
            
            using var serviceProvider = services.BuildServiceProvider();
            
            // Bootstrap application (handles setup, configuration validation, migrations)
            var bootstrapper = serviceProvider.GetRequiredService<ApplicationBootstrapper>();
            var bootstrapResult = await bootstrapper.BootstrapAsync(args, cancellationTokenSource.Token).ConfigureAwait(false);
            
            // If bootstrap determined we should exit early (setup ran, invalid config, etc.), honor that
            if (!bootstrapResult.ShouldContinue)
            {
                return bootstrapResult.ExitCode;
            }
            
            // Determine execution mode: shell or single command
            int exitCode;
            
            if (args.Length == 0)
            {
                // No arguments: Launch shell mode
                logger.LogInformation("Starting shell mode");
                var replLoop = serviceProvider.GetRequiredService<IReplLoop>();
                exitCode = await replLoop.RunAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
            else
            {
                // Arguments provided: Execute single command (existing behavior)
                logger.LogInformation("Executing single command mode");
                var rootCommand = CommandRegistry.BuildRootCommand(serviceProvider);
                var parseResult = rootCommand.Parse(args);
                exitCode = await parseResult.InvokeAsync().ConfigureAwait(false);
            }
            
            logger.LogInformation("Ten Second Tom completed with exit code {ExitCode}", exitCode);
            return exitCode;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Fatal error: {ex.Message}").ConfigureAwait(false);
            
            if (loggerFactory != null)
            {
                var logger = loggerFactory.CreateLogger("TenSecondTom.Program");
                logger.LogCritical(ex, "Fatal error during application execution");
            }
            
            return 1;
        }
        finally
        {
            // Dispose cancellation token source
            cancellationTokenSource?.Dispose();
            
            // Dispose logger factory
            loggerFactory?.Dispose();
            
            // Ensure all log messages are flushed
            LoggingConfiguration.CloseAndFlush();
        }
    }
}
