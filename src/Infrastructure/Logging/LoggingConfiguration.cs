using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace TenSecondTom.Infrastructure.Logging;

/// <summary>
/// Provides centralized logging configuration using Serilog.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog as the logging provider using settings from configuration.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>A configured <see cref="ILoggerFactory"/> instance.</returns>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item>Reads Serilog configuration from appsettings.json</item>
    /// <item>Configures Console and File sinks</item>
    /// <item>Sets up structured logging with enrichers</item>
    /// <item>Applies log level filtering per namespace</item>
    /// </list>
    /// </remarks>
    public static ILoggerFactory ConfigureLogging(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Configure Serilog from configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Create logger factory with Serilog provider
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        Log.Information("Logging configured successfully");

        return loggerFactory;
    }

    /// <summary>
    /// Closes and flushes the Serilog logger.
    /// Call this before application exit to ensure all log messages are written.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
