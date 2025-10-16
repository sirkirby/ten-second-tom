using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Templates.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup.Services;

/// <summary>
/// Coordinates application startup, including configuration validation and first-time setup.
/// This service encapsulates all setup-related startup logic to keep Program.cs thin.
/// </summary>
public sealed class ApplicationBootstrapper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApplicationBootstrapper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationBootstrapper"/> class.
    /// </summary>
    public ApplicationBootstrapper(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ApplicationBootstrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Bootstraps the application, handling configuration validation and first-time setup.
    /// </summary>
    /// <param name="args">Command-line arguments (used to determine if first-run setup should trigger).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating whether the application should continue, and an optional exit code.</returns>
    public async Task<BootstrapResult> BootstrapAsync(string[] args, CancellationToken cancellationToken)
    {
        bool isConfigured = ConfigurationChecker.IsConfigured(_configuration, _logger);

        // Run template migration for existing users (silent, automatic)
        if (isConfigured)
        {
            var migrationService = _serviceProvider.GetRequiredService<TemplateMigrationService>();
            await migrationService.RunAutomaticMigrationAsync(_configuration, cancellationToken)
                .ConfigureAwait(false);

            // Perform self-healing: check for missing templates directory and restore if needed
            var fileSystem = _serviceProvider.GetRequiredService<IFileSystem>();
            bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
                _configuration,
                fileSystem,
                _logger,
                cancellationToken).ConfigureAwait(false);

            if (healingPerformed)
            {
                _logger.LogInformation("Self-healing completed: Templates directory and defaults restored");
            }
        }

        // Validate model configuration if configured
        if (isConfigured && !ConfigurationChecker.ValidateModel(_configuration, _logger))
        {
            return await HandleInvalidConfigurationAsync(args, cancellationToken).ConfigureAwait(false);
        }

        // Check for first-time setup (no arguments and not configured)
        if (!isConfigured && args.Length == 0)
        {
            return await HandleFirstTimeSetupAsync(cancellationToken).ConfigureAwait(false);
        }

        // Configuration is valid, continue to normal app execution
        return BootstrapResult.ContinueExecution();
    }

    /// <summary>
    /// Handles invalid or outdated configuration by prompting the user to re-run setup.
    /// </summary>
    private async Task<BootstrapResult> HandleInvalidConfigurationAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string? errorMessage = ConfigurationChecker.GetModelValidationError(_configuration);
        if (errorMessage != null)
        {
            await Console.Error.WriteLineAsync().ConfigureAwait(false);
            await Console.Error.WriteLineAsync(errorMessage).ConfigureAwait(false);
            await Console.Error.WriteLineAsync().ConfigureAwait(false);
        }

        // Offer to re-run setup to fix the configuration
        bool shouldRunSetup = await PromptForSetupAsync(cancellationToken).ConfigureAwait(false);

        if (!shouldRunSetup)
        {
            _logger.LogInformation("User declined to run setup. Exiting.");
            return BootstrapResult.ExitEarly(1);
        }

        // Run setup to fix the configuration
        _logger.LogInformation("Running setup to fix invalid configuration");
        var setupResult = await RunSetupAsync(force: true, cancellationToken).ConfigureAwait(false);

        if (!setupResult.IsSuccess)
        {
            _logger.LogError("Setup failed: {Error}", setupResult.Error);
            await Console.Error.WriteLineAsync($"Setup failed: {setupResult.Error}").ConfigureAwait(false);
            return BootstrapResult.ExitEarly(1);
        }

        _logger.LogInformation("Configuration updated successfully");
        Console.WriteLine();
        Console.WriteLine("Configuration updated! You can now use Ten Second Tom.");
        Console.WriteLine();

        // If they were trying to run a command, suggest running it again
        if (args.Length > 0)
        {
            Console.WriteLine($"Please run your command again: tom {string.Join(" ", args)}");
        }

        return BootstrapResult.ExitEarly(0);
    }

    /// <summary>
    /// Handles first-time setup by launching the setup wizard.
    /// </summary>
    private async Task<BootstrapResult> HandleFirstTimeSetupAsync(CancellationToken cancellationToken)
    {
        // No arguments and not configured = first-run setup wizard
        _logger.LogInformation("First-run detected. Launching setup wizard...");
        Console.WriteLine();
        Console.WriteLine("Welcome to Ten Second Tom! 🎩");
        Console.WriteLine("Let's get you set up...");
        Console.WriteLine();

        // Run setup wizard
        var setupResult = await RunSetupAsync(force: false, cancellationToken).ConfigureAwait(false);

        if (!setupResult.IsSuccess)
        {
            _logger.LogError("Setup failed: {Error}", setupResult.Error);
            await Console.Error.WriteLineAsync($"Setup failed: {setupResult.Error}").ConfigureAwait(false);
            return BootstrapResult.ExitEarly(1);
        }

        _logger.LogInformation("Setup completed successfully");
        Console.WriteLine();
        Console.WriteLine("Setup complete! You can now use Ten Second Tom.");
        Console.WriteLine();
        Console.WriteLine("Try 'tom today' to record what you're working on.");
        return BootstrapResult.ExitEarly(0);
    }

    /// <summary>
    /// Runs the setup command with the specified options.
    /// </summary>
    private async Task<Result<Features.Setup.Models.ConfigurationSettings>> RunSetupAsync(bool force, CancellationToken cancellationToken)
    {
        var setupHandler = _serviceProvider.GetRequiredService<SetupCommandHandler>();
        var setupCommand = new SetupCommand
        {
            Force = force,
            NonInteractive = false,
            ExistingConfiguration = null
        };

        return await setupHandler.Handle(setupCommand, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prompts the user to run setup to fix invalid or outdated configuration.
    /// </summary>
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

/// <summary>
/// Result of the application bootstrap process.
/// </summary>
public sealed record BootstrapResult
{
    /// <summary>
    /// Gets a value indicating whether the application should continue execution.
    /// </summary>
    public bool ShouldContinue { get; init; }

    /// <summary>
    /// Gets the exit code if the application should exit early.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Creates a result indicating the application should continue execution.
    /// </summary>
    public static BootstrapResult ContinueExecution() => new() { ShouldContinue = true, ExitCode = 0 };

    /// <summary>
    /// Creates a result indicating the application should exit early with the specified code.
    /// </summary>
    public static BootstrapResult ExitEarly(int exitCode) => new() { ShouldContinue = false, ExitCode = exitCode };
}

