namespace TenSecondTom.Infrastructure.Bootstrapping;

/// <summary>
/// Contract for feature-level migrations that run during application bootstrap.
/// Features implement this interface to perform one-time setup or migration tasks.
/// </summary>
/// <remarks>
/// Migrations are discovered via assembly scanning and executed automatically during startup.
/// This enables a drop-in pattern where features can declare their own migrations without
/// creating compile-time dependencies in the bootstrap logic.
///
/// Example usage in a feature:
/// <code>
/// // Features/MyFeature/Migrations/MyFeatureMigration.cs
/// public sealed class MyFeatureMigration : IFeatureMigration
/// {
///     public string FeatureName => "MyFeature";
///     public int Priority => 100;
///
///     public async Task&lt;bool&gt; MigrateAsync(IServiceProvider services, CancellationToken ct)
///     {
///         // Resolve dependencies and perform migration
///         var logger = services.GetRequiredService&lt;ILogger&lt;MyFeatureMigration&gt;&gt;();
///         // ... migration logic ...
///         return true; // Migration was performed
///     }
/// }
/// </code>
/// </remarks>
public interface IFeatureMigration
{
    /// <summary>
    /// The name of the feature this migration belongs to (for logging and identification).
    /// </summary>
    /// <example>"Templates", "Auth", "Setup"</example>
    string FeatureName { get; }

    /// <summary>
    /// Execution priority (lower numbers run first). Default: 100.
    /// </summary>
    /// <remarks>
    /// Use lower values for migrations that other features depend on:
    /// - 0-49: Infrastructure migrations (low-level setup)
    /// - 50-99: Core feature migrations (templates, auth)
    /// - 100+: Standard feature migrations
    /// </remarks>
    int Priority { get; }

    /// <summary>
    /// Executes the migration using services from the dependency injection container.
    /// </summary>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <param name="cancellationToken">Token to cancel the migration.</param>
    /// <returns>
    /// True if the migration was performed, false if it was skipped (e.g., already migrated).
    /// </returns>
    /// <remarks>
    /// Migrations should be idempotent - safe to run multiple times.
    /// If migration fails, throw an exception; the bootstrap process will log it and continue.
    /// </remarks>
    Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken);
}
