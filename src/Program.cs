using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.DependencyInjection;
using TenSecondTom.Infrastructure.Logging;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "CLI application, localization not required")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates for improved performance", Justification = "Startup logging, performance not critical")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top-level exception handler")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Console application, no synchronization context")]
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<object>(optional: true)
                .AddEnvironmentVariables()
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
            services.AddTenSecondTomServices();
            
            using var serviceProvider = services.BuildServiceProvider();
            
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
