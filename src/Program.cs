using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.DependencyInjection;
using TenSecondTom.Infrastructure.Logging;
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
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{EnvironmentHelper.GetCurrentEnvironment()}.json", optional: true, reloadOnChange: true);
            
            // Add User Secrets explicitly (for self-contained/trimmed binaries)
            // This doesn't rely on assembly reflection like AddUserSecrets<T>()
            string userSecretsId = "ten-second-tom-secrets";
            string userSecretsPath = SecretsHelper.GetUserSecretsPath(userSecretsId);
            if (File.Exists(userSecretsPath))
            {
                configurationBuilder.AddJsonFile(userSecretsPath, optional: true, reloadOnChange: true);
            }
            
            var configuration = configurationBuilder
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
            
            // Check if first-run setup is needed (only when no args or entering shell mode)
            // Commands like help, config, setup, version should always work without configuration
            bool isConfigured = ConfigurationChecker.IsConfigured(configuration, logger);
            
            // Validate model configuration if configured
            if (isConfigured && !ConfigurationChecker.ValidateModel(configuration, logger))
            {
                string? errorMessage = ConfigurationChecker.GetModelValidationError(configuration);
                if (errorMessage != null)
                {
                    await Console.Error.WriteLineAsync().ConfigureAwait(false);
                    await Console.Error.WriteLineAsync(errorMessage).ConfigureAwait(false);
                    await Console.Error.WriteLineAsync().ConfigureAwait(false);
                }
                
                // Offer to re-run setup to fix the configuration
                bool shouldRunSetup = await PromptForSetupAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                
                if (!shouldRunSetup)
                {
                    logger.LogInformation("User declined to run setup. Exiting.");
                    return 1;
                }
                
                // Run setup to fix the configuration
                logger.LogInformation("Running setup to fix invalid configuration");
                var setupHandler = serviceProvider.GetRequiredService<SetupCommandHandler>();
                var setupCommand = new SetupCommand
                {
                    Force = true, // Force re-configuration
                    NonInteractive = false,
                    ExistingConfiguration = null // Will load from storage within setup handler
                };
                
                var setupResult = await setupHandler.Handle(setupCommand, cancellationTokenSource.Token).ConfigureAwait(false);
                
                if (!setupResult.IsSuccess)
                {
                    logger.LogError("Setup failed: {Error}", setupResult.Error);
                    await Console.Error.WriteLineAsync($"Setup failed: {setupResult.Error}").ConfigureAwait(false);
                    return 1;
                }
                
                logger.LogInformation("Configuration updated successfully");
                Console.WriteLine();
                Console.WriteLine("Configuration updated! You can now use Ten Second Tom.");
                Console.WriteLine();
                
                // If they were trying to run a command, suggest running it again
                if (args.Length > 0)
                {
                    Console.WriteLine($"Please run your command again: tom {string.Join(" ", args)}");
                }
                
                return 0;
            }
            
            if (!isConfigured && args.Length == 0)
            {
                // No arguments and not configured = first-run setup wizard
                logger.LogInformation("First-run detected. Launching setup wizard...");
                Console.WriteLine();
                Console.WriteLine("Welcome to Ten Second Tom! 🎩");
                Console.WriteLine("Let's get you set up...");
                Console.WriteLine();
                
                // Run setup wizard
                var setupHandler = serviceProvider.GetRequiredService<SetupCommandHandler>();
                var setupCommand = new SetupCommand
                {
                    Force = false,
                    NonInteractive = false,
                    ExistingConfiguration = null
                };
                
                var setupResult = await setupHandler.Handle(setupCommand, cancellationTokenSource.Token).ConfigureAwait(false);
                
                if (!setupResult.IsSuccess)
                {
                    logger.LogError("Setup failed: {Error}", setupResult.Error);
                    await Console.Error.WriteLineAsync($"Setup failed: {setupResult.Error}").ConfigureAwait(false);
                    return 1;
                }
                
                logger.LogInformation("Setup completed successfully");
                Console.WriteLine();
                Console.WriteLine("Setup complete! You can now use Ten Second Tom.");
                Console.WriteLine();
                Console.WriteLine("Try 'tom today' to record what you're working on.");
                return 0;
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

    /// <summary>
    /// Prompts the user to run setup to fix invalid or outdated configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user wants to run setup, false otherwise</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "CLI tool - localization not required")]
    private static async Task<bool> PromptForSetupAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Your configuration appears to be invalid or outdated. Would you like to run setup again? (y/n): ");
        Console.ResetColor();

        // Check if console input is redirected (e.g., piped from echo or file)
        if (Console.IsInputRedirected)
        {
            // Read from stdin (supports piped input)
            string? input = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine();
                return false;
            }
            
            char firstChar = char.ToLowerInvariant(input.Trim()[0]);
            Console.WriteLine(firstChar); // Echo the response
            return firstChar == 'y';
        }

        // Interactive mode - use ReadKey for better UX
        while (!cancellationToken.IsCancellationRequested)
        {
            var key = Console.ReadKey(intercept: true);
            
            if (key.Key == ConsoleKey.Y)
            {
                Console.WriteLine("y");
                return await Task.FromResult(true).ConfigureAwait(false);
            }
            
            if (key.Key == ConsoleKey.N)
            {
                Console.WriteLine("n");
                return await Task.FromResult(false).ConfigureAwait(false);
            }
            
            // Invalid input - beep and continue
            Console.Beep();
        }
        
        // Cancelled
        Console.WriteLine();
        return false;
    }
}
