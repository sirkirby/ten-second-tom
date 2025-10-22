using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace TenSecondTom.IntegrationTests.TestHelpers;

/// <summary>
/// Base class for tests that use UserSecrets with robust cleanup.
/// Implements IAsyncLifetime for xUnit lifecycle management and provides
/// retry logic for cleanup to handle file locks and race conditions.
/// </summary>
/// <remarks>
/// This fixture addresses the common issue of orphaned UserSecrets directories
/// when tests fail, timeout, or are interrupted. It provides:
/// - Automatic cleanup with retry logic for file locks
/// - Pre-test cleanup to handle orphaned directories from previous runs
/// - Post-test cleanup even when tests fail
/// - Diagnostic logging for troubleshooting cleanup issues
/// </remarks>
public abstract class UserSecretsTestFixture : IAsyncLifetime
{
    private const int MaxCleanupRetries = 3;
    private const int CleanupRetryDelayMs = 100;

    protected string TestUserSecretsId { get; private set; } = string.Empty;
    protected ILogger? Logger { get; set; }

    /// <summary>
    /// Gets the path to the UserSecrets directory for this test instance.
    /// </summary>
    protected string UserSecretsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "UserSecrets",
        TestUserSecretsId);

    /// <summary>
    /// Initializes the fixture before tests run.
    /// Creates a unique UserSecrets ID and performs pre-test cleanup.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        // Generate unique test ID with recognizable prefix for cleanup scripts
        TestUserSecretsId = $"TenSecondTom-Test-{Guid.NewGuid()}";

        // Pre-test cleanup: Remove any orphaned directories from previous failed runs
        // This is defensive - in case previous test runs were interrupted
        CleanupOrphanedTestDirectories();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up the fixture after tests complete.
    /// Ensures UserSecrets directory is removed even if tests fail.
    /// </summary>
    public virtual Task DisposeAsync()
    {
        // Always attempt cleanup, even if tests failed
        CleanupUserSecretsDirectory();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up the UserSecrets directory for this test instance with retry logic.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cleanup must not throw")]
    protected void CleanupUserSecretsDirectory()
    {
        if (string.IsNullOrEmpty(TestUserSecretsId))
        {
            return;
        }

        var directoryPath = UserSecretsPath;

        if (!Directory.Exists(directoryPath))
        {
            Logger?.LogDebug("UserSecrets directory does not exist: {Path}", directoryPath);
            return;
        }

        // Attempt cleanup with retry logic
        for (int attempt = 1; attempt <= MaxCleanupRetries; attempt++)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);
                Logger?.LogDebug("Successfully cleaned up UserSecrets directory: {Path}", directoryPath);
                return;
            }
            catch (IOException ex) when (attempt < MaxCleanupRetries)
            {
                // Directory may be locked - retry after delay
                Logger?.LogWarning(
                    "Cleanup attempt {Attempt}/{Max} failed for {Path}: {Error}. Retrying after {Delay}ms...",
                    attempt, MaxCleanupRetries, directoryPath, ex.Message, CleanupRetryDelayMs);

                Thread.Sleep(CleanupRetryDelayMs);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxCleanupRetries)
            {
                // Permissions issue - retry after delay
                Logger?.LogWarning(
                    "Cleanup attempt {Attempt}/{Max} failed (access denied) for {Path}. Retrying after {Delay}ms...",
                    attempt, MaxCleanupRetries, directoryPath, CleanupRetryDelayMs);

                Thread.Sleep(CleanupRetryDelayMs);
            }
            catch (Exception cleanupException)
            {
                // Final attempt or unexpected error - log and continue
                Logger?.LogError(
                    "Failed to cleanup UserSecrets directory after {Attempt} attempt(s): {Path}. Error: {Error}. " +
                    "Run '.scripts/cleanup-test-secrets.sh' to manually cleanup orphaned directories.",
                    attempt, directoryPath, cleanupException.Message);
                return;
            }
        }

        // All retries exhausted
        Logger?.LogError(
            "Failed to cleanup UserSecrets directory after {Max} attempts: {Path}. " +
            "Directory may be left behind. Run '.scripts/cleanup-test-secrets.sh' to cleanup.",
            MaxCleanupRetries, directoryPath);
    }

    /// <summary>
    /// Cleans up orphaned test directories from previous test runs.
    /// This is a defensive measure to handle cases where previous tests were interrupted.
    /// </summary>
    /// <remarks>
    /// Only removes directories that match the test naming pattern (TenSecondTom-Test-* or tom-test-*).
    /// This runs before each test fixture initialization to ensure a clean state.
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Pre-cleanup is best-effort")]
    protected void CleanupOrphanedTestDirectories()
    {
        var userSecretsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets");

        if (!Directory.Exists(userSecretsRoot))
        {
            return;
        }

        try
        {
            // Find all orphaned test directories
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
                return;
            }

            Logger?.LogInformation(
                "Found {Count} orphaned test directories from previous runs. Cleaning up...",
                testDirectories.Count);

            int cleanedCount = 0;
            int failedCount = 0;

            foreach (var directory in testDirectories)
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    cleanedCount++;
                }
                catch (Exception ex)
                {
                    // Log but don't fail - cleanup is best-effort
                    Logger?.LogWarning(
                        "Failed to cleanup orphaned directory {Path}: {Error}",
                        directory, ex.Message);
                    failedCount++;
                }
            }

            Logger?.LogInformation(
                "Orphaned directory cleanup complete. Removed: {Cleaned}, Failed: {Failed}",
                cleanedCount, failedCount);

            if (failedCount > 0)
            {
                Logger?.LogWarning(
                    "Some orphaned directories could not be removed. " +
                    "Run '.scripts/cleanup-test-secrets.sh' for manual cleanup.");
            }
        }
        catch (Exception ex)
        {
            // Pre-cleanup failure should not fail tests
            Logger?.LogWarning(
                "Error during orphaned directory cleanup: {Error}. Continuing with tests.",
                ex.Message);
        }
    }

    /// <summary>
    /// Helper method to verify that UserSecrets directory was created.
    /// Useful for assertions in tests.
    /// </summary>
    protected bool UserSecretsDirectoryExists() => Directory.Exists(UserSecretsPath);

    /// <summary>
    /// Helper method to get files in the UserSecrets directory.
    /// Useful for test assertions.
    /// </summary>
    protected string[] GetUserSecretsFiles()
    {
        if (!Directory.Exists(UserSecretsPath))
        {
            return [];
        }

        return Directory.GetFiles(UserSecretsPath, "*.*", SearchOption.AllDirectories);
    }
}
