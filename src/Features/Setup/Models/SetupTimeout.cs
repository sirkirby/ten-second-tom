namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Configuration for setup wizard timeouts
/// Controls how long various operations can take
/// </summary>
public sealed class SetupTimeout
{
    /// <summary>
    /// Gets or sets the timeout for SSH key detection (default: 5 seconds)
    /// </summary>
    public TimeSpan SshKeyDetectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the timeout for API validation per attempt (default: 10 seconds)
    /// </summary>
    public TimeSpan ApiValidationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum total time for the entire setup process (default: 2 minutes)
    /// </summary>
    public TimeSpan TotalSetupTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Creates default timeout configuration
    /// </summary>
    public static SetupTimeout CreateDefault() => new();

    /// <summary>
    /// Creates timeout configuration from appsettings values
    /// </summary>
    public static SetupTimeout FromConfiguration(
        int sshKeyDetectionTimeoutSeconds = 5,
        int apiValidationTimeoutSeconds = 10,
        int totalSetupTimeoutSeconds = 120)
    {
        return new SetupTimeout
        {
            SshKeyDetectionTimeout = TimeSpan.FromSeconds(sshKeyDetectionTimeoutSeconds),
            ApiValidationTimeout = TimeSpan.FromSeconds(apiValidationTimeoutSeconds),
            TotalSetupTimeout = TimeSpan.FromSeconds(totalSetupTimeoutSeconds)
        };
    }

    /// <summary>
    /// Validates that all timeouts are positive
    /// </summary>
    public bool IsValid()
    {
        return SshKeyDetectionTimeout > TimeSpan.Zero
            && ApiValidationTimeout > TimeSpan.Zero
            && TotalSetupTimeout > TimeSpan.Zero
            && TotalSetupTimeout >= SshKeyDetectionTimeout
            && TotalSetupTimeout >= ApiValidationTimeout;
    }
}
