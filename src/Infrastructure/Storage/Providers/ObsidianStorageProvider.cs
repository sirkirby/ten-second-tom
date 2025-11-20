using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage.Providers;

/// <summary>
/// Obsidian vault storage provider.
/// Stores memory entries in an Obsidian vault with optional subdirectory organization.
/// Compatible with Obsidian's file structure and YAML frontmatter format.
/// </summary>
public sealed class ObsidianStorageProvider : IStorageProvider
{
    private readonly FileSystemStorageProvider _innerProvider;
    private readonly IOptions<StorageOptions> _options;
    private readonly ILogger<ObsidianStorageProvider> _logger;

    /// <inheritdoc/>
    public string ProviderId => StorageProviderIds.Obsidian;

    /// <inheritdoc/>
    public string DisplayName => "Obsidian Vault";

    /// <inheritdoc/>
    public string Description => "Store entries in an Obsidian vault for seamless integration with your notes. Supports bidirectional sync and Obsidian's daily notes format.";

    /// <summary>
    /// Initializes a new instance of the <see cref="ObsidianStorageProvider"/> class.
    /// </summary>
    public ObsidianStorageProvider(
        IOptions<StorageOptions> options,
        ILogger<ObsidianStorageProvider> logger,
        ILoggerFactory loggerFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(loggerFactory);

        // Delegate to FileSystemStorageProvider for actual storage operations
        var innerLogger = loggerFactory.CreateLogger<FileSystemStorageProvider>();
        string baseDirectory = GetMemoryDirectory();
        _innerProvider = new FileSystemStorageProvider(baseDirectory, innerLogger);
    }

    /// <inheritdoc/>
    public Task<Result> InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            string vaultRoot = GetVaultRoot();

            // Validate Obsidian vault structure
            string obsidianDir = Path.Combine(vaultRoot, ".obsidian");
            if (!Directory.Exists(obsidianDir))
            {
                return Task.FromResult(Result.Failure($"Not a valid Obsidian vault: .obsidian directory not found at {vaultRoot}"));
            }

            // Get memory directory (vault root or subdirectory)
            string memoryDir = GetMemoryDirectory();

            // Create TST memory directory if it doesn't exist
            // Feature-specific subdirectories (today/, thisweek/, recording/) will be created
            // on-demand by FileSystemStorageProvider when entries are saved
            if (!Directory.Exists(memoryDir))
            {
                Directory.CreateDirectory(memoryDir);
                _logger.LogInformation("Created TST memory directory in vault: {Directory}", memoryDir);
            }

            _logger.LogInformation("Obsidian storage provider initialized successfully in vault: {VaultRoot}", vaultRoot);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Obsidian storage provider");
            return Task.FromResult(Result.Failure($"Obsidian vault initialization failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<string>> ValidateConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            string vaultRoot = GetVaultRoot();

            // Check if vault directory exists
            if (!Directory.Exists(vaultRoot))
            {
                return Task.FromResult(Result<string>.Failure($"Vault directory does not exist: {vaultRoot}"));
            }

            // Check for .obsidian directory
            string obsidianDir = Path.Combine(vaultRoot, ".obsidian");
            if (!Directory.Exists(obsidianDir))
            {
                return Task.FromResult(Result<string>.Failure(
                    $"Not a valid Obsidian vault: .obsidian directory not found at {vaultRoot}"));
            }

            // Test write permissions
            try
            {
                string testFile = Path.Combine(vaultRoot, ".tst-write-test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string>.Failure(
                    $"Vault is not writable: {ex.Message}"));
            }

            string memoryDir = GetMemoryDirectory();
            string subdirInfo = string.IsNullOrWhiteSpace(_options.Value.MemorySubdirectory)
                ? "(root level)"
                : $"(subdirectory: {_options.Value.MemorySubdirectory})";

            return Task.FromResult(Result<string>.Success(
                $"Obsidian vault: {vaultRoot} {subdirInfo}"));
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
    /// Gets the vault root directory from configuration.
    /// </summary>
    private string GetVaultRoot()
    {
        var options = _options.Value;

        // Use ProviderPath for vault location (preferred)
        string? vaultRoot = options.ProviderPath;

        // Fall back to RootDirectory for backward compatibility (pre-ProviderPath configurations)
        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            vaultRoot = options.RootDirectory;
        }

        // Fall back to legacy MemoryDirectory for even older configurations
        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            vaultRoot = options.MemoryDirectory;
        }

        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            throw new InvalidOperationException(
                "Storage.ProviderPath must be configured to point to an Obsidian vault. " +
                "Run 'tom setup' to configure your Obsidian vault path.");
        }

        return vaultRoot;
    }

    /// <summary>
    /// Gets the directory where memory entries should be stored.
    /// This may be the vault root or a subdirectory within the vault.
    /// </summary>
    private string GetMemoryDirectory()
    {
        string vaultRoot = GetVaultRoot();
        string? subdirectory = _options.Value.MemorySubdirectory;

        return string.IsNullOrWhiteSpace(subdirectory)
            ? vaultRoot
            : Path.Combine(vaultRoot, subdirectory);
    }

}
