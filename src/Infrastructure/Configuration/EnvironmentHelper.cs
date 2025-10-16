using Microsoft.Extensions.Configuration;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Provides centralized environment detection logic.
/// Eliminates duplicate environment checking code across the application.
/// </summary>
public static class EnvironmentHelper
{
    /// <summary>
    /// Gets the current application environment name.
    /// Checks configuration first, then environment variables, defaulting to Production.
    /// </summary>
    /// <param name="configuration">Optional configuration to check first.</param>
    /// <returns>The environment name (Development, Production, etc.).</returns>
    public static string GetCurrentEnvironment(IConfiguration? configuration = null)
    {
        return configuration?[ConfigurationKeys.DotNetEnvironment]
            ?? Environment.GetEnvironmentVariable(ConfigurationKeys.DotNetEnvironment)
            ?? EnvironmentNames.Production;
    }

    /// <summary>
    /// Determines if the current environment is Development.
    /// </summary>
    /// <param name="configuration">Optional configuration to check.</param>
    /// <returns>True if environment is Development, false otherwise.</returns>
    public static bool IsDevelopment(IConfiguration? configuration = null)
    {
        return GetCurrentEnvironment(configuration)
            .Equals(EnvironmentNames.Development, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the current environment is Production.
    /// </summary>
    /// <param name="configuration">Optional configuration to check.</param>
    /// <returns>True if environment is Production, false otherwise.</returns>
    public static bool IsProduction(IConfiguration? configuration = null)
    {
        return GetCurrentEnvironment(configuration)
            .Equals(EnvironmentNames.Production, StringComparison.OrdinalIgnoreCase);
    }
}
