using Microsoft.Extensions.Logging;

namespace TenSecondTom.IntegrationTests.TestHelpers;

/// <summary>
/// Collection fixture for UserSecrets integration tests.
/// Provides test-wide cleanup of orphaned directories before and after test execution.
/// </summary>
/// <remarks>
/// This fixture runs once per test collection, performing:
/// - Pre-test cleanup of orphaned directories from previous failed/interrupted runs
/// - Post-test cleanup of any remaining test directories
///
/// Use with xUnit's [Collection] attribute on test classes:
/// <code>
/// [Collection(UserSecretsCollection.Name)]
/// public class MyTests
/// {
///     // Tests here will benefit from collection-wide cleanup
/// }
/// </code>
/// </remarks>
public sealed class UserSecretsCollectionFixture : IAsyncLifetime
{
    private readonly ILogger<UserSecretsCollectionFixture> _logger;

    public UserSecretsCollectionFixture()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<UserSecretsCollectionFixture>();
    }

    /// <summary>
    /// Runs before any tests in the collection execute.
    /// Performs cleanup of orphaned test directories from previous runs.
    /// </summary>
    public Task InitializeAsync()
    {
        _logger.LogInformation("UserSecrets collection fixture initializing - performing pre-test cleanup");
        CleanupAllTestDirectories("Pre-test cleanup");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs after all tests in the collection complete.
    /// Performs final cleanup of any remaining test directories.
    /// </summary>
    public Task DisposeAsync()
    {
        _logger.LogInformation("UserSecrets collection fixture disposing - performing post-test cleanup");
        CleanupAllTestDirectories("Post-test cleanup");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up all test UserSecrets directories.
    /// </summary>
    private void CleanupAllTestDirectories(string context)
    {
        var userSecretsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets");

        if (!Directory.Exists(userSecretsRoot))
        {
            _logger.LogDebug("{Context}: UserSecrets root directory does not exist", context);
            return;
        }

        try
        {
            // Find all test directories (both naming patterns)
            var testDirectories = Directory.GetDirectories(userSecretsRoot)
                .Where(dir =>
                {
                    var name = Path.GetFileName(dir);
                    return name.StartsWith("TenSecondTom-Test-", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith("tom-test-", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (testDirectories.Count == 0)
            {
                _logger.LogInformation("{Context}: No test directories found to clean up", context);
                return;
            }

            _logger.LogInformation(
                "{Context}: Found {Count} test directories to clean up",
                context, testDirectories.Count);

            int cleanedCount = 0;
            int failedCount = 0;

            foreach (var directory in testDirectories)
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    cleanedCount++;
                    _logger.LogDebug("Deleted: {Directory}", Path.GetFileName(directory));
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogWarning(
                        "Failed to delete {Directory}: {Error}",
                        Path.GetFileName(directory), ex.Message);
                }
            }

            _logger.LogInformation(
                "{Context} complete: Removed {Cleaned} directories, {Failed} failed",
                context, cleanedCount, failedCount);

            if (failedCount > 0)
            {
                _logger.LogWarning(
                    "Some directories could not be removed. " +
                    "Run '.scripts/cleanup-test-secrets.sh' for manual cleanup.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("{Context} failed: {Error}", context, ex.Message);
        }
    }
}

/// <summary>
/// xUnit collection definition for UserSecrets tests.
/// </summary>
[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711:Identifiers should not have incorrect suffix", Justification = "xUnit collection naming convention requires 'Collection' suffix")]
public sealed class UserSecretsCollection : ICollectionFixture<UserSecretsCollectionFixture>
{
    public const string Name = "UserSecrets Collection";
    // This class is never instantiated. It exists only to define the collection.
}
