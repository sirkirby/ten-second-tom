using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Service responsible for automatically purging old memory entries based on retention policy.
/// Runs on application startup if auto-purge is enabled.
/// </summary>
public sealed class AutoPurgeService
{
    private readonly IMemoryStorageProvider _storageProvider;
    private readonly StorageConfiguration _configuration;
    private readonly ILogger<AutoPurgeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoPurgeService"/> class.
    /// </summary>
    /// <param name="storageProvider">The storage provider to use for purging entries.</param>
    /// <param name="configuration">The storage configuration containing retention policy.</param>
    /// <param name="logger">The logger for recording purge operations.</param>
    public AutoPurgeService(
        IMemoryStorageProvider storageProvider,
        StorageConfiguration configuration,
        ILogger<AutoPurgeService> logger)
    {
        _storageProvider = storageProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Purges entries older than the configured retention period.
    /// Skips purge if auto-purge is disabled or retention policy is Indefinite.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing purge summary on success, or error message on failure.</returns>
    public async Task<Result<AutoPurgeResult>> PurgeAsync(CancellationToken cancellationToken)
    {
        // Skip if auto-purge is disabled
        if (!_configuration.AutoPurge)
        {
            _logger.LogInformation("Auto-purge is disabled, skipping purge operation");
            return Result<AutoPurgeResult>.Success(new AutoPurgeResult
            {
                EntriesDeleted = 0,
                WasSkipped = true,
                RetentionPolicy = _configuration.RetentionPolicy
            });
        }

        // Skip if retention policy is Indefinite
        if (_configuration.RetentionPolicy == RetentionPolicy.Indefinite)
        {
            _logger.LogInformation("Retention policy is Indefinite, skipping purge operation");
            return Result<AutoPurgeResult>.Success(new AutoPurgeResult
            {
                EntriesDeleted = 0,
                WasSkipped = true,
                RetentionPolicy = _configuration.RetentionPolicy
            });
        }

        // Calculate cutoff date based on retention policy
        DateTime cutoffDate = CalculateCutoffDate(_configuration.RetentionPolicy);

        _logger.LogInformation(
            "Starting auto-purge operation with retention policy {Policy}, cutoff date: {CutoffDate:yyyy-MM-dd}",
            _configuration.RetentionPolicy,
            cutoffDate);

        // Purge expired entries
        Result<int> purgeResult = await _storageProvider
            .PurgeExpiredEntriesAsync(_configuration.RetentionPolicy, cancellationToken)
            .ConfigureAwait(false);

        if (!purgeResult.IsSuccess)
        {
            _logger.LogError("Auto-purge operation failed: {Error}", purgeResult.Error);
            return Result<AutoPurgeResult>.Failure(purgeResult.Error ?? "Unknown error");
        }

        _logger.LogInformation(
            "Auto-purge completed successfully: deleted {Count} entries older than {CutoffDate:yyyy-MM-dd}",
            purgeResult.Value,
            cutoffDate);

        return Result<AutoPurgeResult>.Success(new AutoPurgeResult
        {
            EntriesDeleted = purgeResult.Value,
            WasSkipped = false,
            RetentionPolicy = _configuration.RetentionPolicy,
            CutoffDate = cutoffDate
        });
    }

    /// <summary>
    /// Calculates the cutoff date based on the retention policy.
    /// Entries older than this date should be purged.
    /// </summary>
    private static DateTime CalculateCutoffDate(RetentionPolicy policy)
    {
        DateTime now = DateTime.UtcNow;

        return policy switch
        {
            RetentionPolicy.Days30 => now.AddDays(-30),
            RetentionPolicy.Days90 => now.AddDays(-90),
            RetentionPolicy.OneYear => now.AddYears(-1),
            RetentionPolicy.TwoYears => now.AddYears(-2),
            RetentionPolicy.Indefinite => DateTime.MinValue, // Should never reach here due to check above
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown retention policy")
        };
    }
}

/// <summary>
/// Result of an auto-purge operation.
/// </summary>
public sealed record AutoPurgeResult
{
    /// <summary>
    /// Gets the number of entries that were deleted.
    /// </summary>
    public required int EntriesDeleted { get; init; }

    /// <summary>
    /// Gets a value indicating whether the purge operation was skipped
    /// (due to being disabled or indefinite retention).
    /// </summary>
    public required bool WasSkipped { get; init; }

    /// <summary>
    /// Gets the retention policy that was applied.
    /// </summary>
    public required RetentionPolicy RetentionPolicy { get; init; }

    /// <summary>
    /// Gets the cutoff date used for purging (entries older than this were deleted).
    /// Null if purge was skipped.
    /// </summary>
    public DateTime? CutoffDate { get; init; }
}
