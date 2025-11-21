using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage.Providers;

/// <summary>
/// Default file system storage provider.
/// Stores memory entries in a hierarchical directory structure under the configured root directory.
/// </summary>
public sealed class DefaultStorageProvider : IStorageProvider
{
    private readonly FileSystemStorageProvider _innerProvider;
    private readonly IOptions<StorageOptions> _options;
    private readonly ILogger<DefaultStorageProvider> _logger;

    /// <inheritdoc/>
    public string ProviderId => StorageProviderIds.Default;

    /// <inheritdoc/>
    public string DisplayName => "Default File System";

    /// <inheritdoc/>
    public string Description => "Stores memory entries in a hierarchical directory structure. Best for general use and backward compatibility with existing installations.";

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultStorageProvider"/> class.
    /// </summary>
    public DefaultStorageProvider(
        IOptions<StorageOptions> options,
        ILogger<DefaultStorageProvider> logger,
        ILoggerFactory loggerFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(loggerFactory);

        // Delegate to FileSystemStorageProvider for actual storage operations
        var innerLogger = loggerFactory.CreateLogger<FileSystemStorageProvider>();
        string baseDirectory = GetBaseDirectory();
        _innerProvider = new FileSystemStorageProvider(baseDirectory, innerLogger);
    }

    /// <inheritdoc/>
    public Task<Result> InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            string baseDirectory = GetBaseDirectory();

            // Create base storage directory if it doesn't exist
            // Feature-specific subdirectories (today/, thisweek/, recording/) will be created
            // on-demand by FileSystemStorageProvider when entries are saved
            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
                _logger.LogInformation("Created storage directory: {Directory}", baseDirectory);
            }

            _logger.LogInformation("Storage provider initialized successfully at: {Directory}", baseDirectory);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize storage provider");
            return Task.FromResult(Result.Failure($"Storage initialization failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<string>> ValidateConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            string baseDirectory = GetBaseDirectory();

            // Check if path is valid
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return Task.FromResult(Result<string>.Failure("Storage directory not configured"));
            }

            // Get full path and validate it
            string fullPath = Path.GetFullPath(baseDirectory);

            if (!Directory.Exists(fullPath))
            {
                // Check if parent directory exists and is accessible
                string? parentDir = Path.GetDirectoryName(fullPath);
                if (parentDir != null && !Directory.Exists(parentDir))
                {
                    return Task.FromResult(Result<string>.Failure(
                        $"Parent directory does not exist: {parentDir}"));
                }
            }

            // Test write permissions by attempting to create a test file
            try
            {
                string testFile = Path.Combine(fullPath, ".tst-write-test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string>.Failure(
                    $"Directory is not writable: {ex.Message}"));
            }

            return Task.FromResult(Result<string>.Success(
                $"Storage directory: {fullPath} (Default provider)"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string>.Failure(
                $"Configuration validation failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<MemoryEntry>> SaveAsync(MemoryEntry entry, CancellationToken cancellationToken)
        => _innerProvider.SaveAsync(entry, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<MemoryEntry>>> GetEntriesAsync(
        string command, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        => _innerProvider.GetEntriesAsync(command, startDate, endDate, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<int>> CountEntriesAsync(string command, DateTime targetDate, CancellationToken cancellationToken)
        => _innerProvider.CountEntriesAsync(command, targetDate, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<MemoryEntry>>> SearchEntriesAsync(
        string query, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
        => _innerProvider.SearchEntriesAsync(query, startDate, endDate, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<int>> DeleteEntriesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        => _innerProvider.DeleteEntriesAsync(startDate, endDate, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<int>> PurgeExpiredEntriesAsync(RetentionPolicy retentionPolicy, CancellationToken cancellationToken)
        => _innerProvider.PurgeExpiredEntriesAsync(retentionPolicy, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<MemoryEntry?>> GetEntryByIdAsync(string entryId, CancellationToken cancellationToken)
        => _innerProvider.GetEntryByIdAsync(entryId, cancellationToken);

    /// <summary>
    /// Gets the base directory for storage operations using the centralized resolution logic.
    /// </summary>
    private string GetBaseDirectory()
    {
        return _options.Value.GetEffectiveStorageDirectory();
    }
}
