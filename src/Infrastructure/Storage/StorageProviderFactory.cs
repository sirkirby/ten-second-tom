using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Factory for creating and discovering storage provider implementations.
/// Uses assembly scanning to automatically discover all IStorageProvider implementations.
/// </summary>
public sealed class StorageProviderFactory : IStorageProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StorageProviderFactory> _logger;
    private readonly IReadOnlyDictionary<string, Type> _providers;

    /// <summary>
    /// Initializes a new instance of the StorageProviderFactory.
    /// Discovers all IStorageProvider implementations via assembly scanning.
    /// </summary>
    public StorageProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<StorageProviderFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providers = DiscoverProviders();
    }

    /// <inheritdoc/>
    public Result<IStorageProvider> CreateProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return Result<IStorageProvider>.Failure("ProviderId cannot be null or empty");
        }

        if (!_providers.TryGetValue(providerId.ToLowerInvariant(), out Type? providerType))
        {
            var availableProviders = string.Join(", ", _providers.Keys);
            return Result<IStorageProvider>.Failure(
                $"Storage provider '{providerId}' not found. Available providers: {availableProviders}");
        }

        try
        {
            var provider = (IStorageProvider)ActivatorUtilities.CreateInstance(_serviceProvider, providerType);
            _logger.LogInformation("Created storage provider: {ProviderId} ({DisplayName})",
                provider.ProviderId, provider.DisplayName);
            return Result<IStorageProvider>.Success(provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create storage provider: {ProviderId}", providerId);
            return Result<IStorageProvider>.Failure($"Failed to create provider: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<StorageProviderMetadata> GetAvailableProviders()
    {
        var metadata = new List<StorageProviderMetadata>();

        foreach (var kvp in _providers)
        {
            try
            {
                // Create temporary instance to read metadata
                var provider = (IStorageProvider)ActivatorUtilities.CreateInstance(_serviceProvider, kvp.Value);
                metadata.Add(new StorageProviderMetadata(
                    provider.ProviderId,
                    provider.DisplayName,
                    provider.Description));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read metadata for provider type: {TypeName}", kvp.Value.Name);
            }
        }

        return metadata;
    }

    /// <summary>
    /// Discovers all IStorageProvider implementations via assembly scanning.
    /// </summary>
    private Dictionary<string, Type> DiscoverProviders()
    {
        var providers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(IStorageProvider).Assembly;

        var providerTypes = assembly.GetTypes()
            .Where(t => typeof(IStorageProvider).IsAssignableFrom(t)
                && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        foreach (var type in providerTypes)
        {
            try
            {
                // Create temporary instance to read ProviderId
                var instance = (IStorageProvider)ActivatorUtilities.CreateInstance(_serviceProvider, type);
                string providerId = instance.ProviderId.ToLowerInvariant();

                if (providers.TryGetValue(providerId, out var existingType))
                {
                    _logger.LogWarning("Duplicate ProviderId '{ProviderId}' found for types {Type1} and {Type2}. Using first registration.",
                        providerId, existingType.Name, type.Name);
                    continue;
                }

                providers[providerId] = type;
                _logger.LogDebug("Discovered storage provider: {ProviderId} ({TypeName})", providerId, type.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to instantiate provider type {TypeName} during discovery", type.Name);
            }
        }

        _logger.LogInformation("Discovered {Count} storage providers", providers.Count);
        return providers;
    }
}
