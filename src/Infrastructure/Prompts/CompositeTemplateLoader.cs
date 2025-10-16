using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Composite template loader that implements a fallback chain for resilient template loading.
/// Attempts to load templates from the primary loader first, falling back to the fallback loader if needed.
/// </summary>
/// <remarks>
/// Fallback order:
/// 1. Primary loader (typically FileSystem templates for user customizations)
/// 2. Fallback loader (typically Embedded templates for defaults)
///
/// This ensures graceful degradation when template files are missing or corrupted.
/// Fallback operations are logged for observability.
/// </remarks>
public sealed class CompositeTemplateLoader : IPromptTemplateLoader
{
    private readonly IPromptTemplateLoader _primaryLoader;
    private readonly IPromptTemplateLoader _fallbackLoader;
    private readonly ILogger<CompositeTemplateLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTemplateLoader"/> class.
    /// </summary>
    /// <param name="primaryLoader">Primary loader (typically filesystem for user customizations).</param>
    /// <param name="fallbackLoader">Fallback loader (typically embedded for defaults).</param>
    /// <param name="logger">Logger for diagnostics and fallback notifications.</param>
    public CompositeTemplateLoader(
        IPromptTemplateLoader primaryLoader,
        IPromptTemplateLoader fallbackLoader,
        ILogger<CompositeTemplateLoader> logger)
    {
        _primaryLoader = primaryLoader ?? throw new ArgumentNullException(nameof(primaryLoader));
        _fallbackLoader = fallbackLoader ?? throw new ArgumentNullException(nameof(fallbackLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<PromptTemplate>> LoadTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Result<PromptTemplate>.Failure("Template ID cannot be null or empty");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Attempt to load from primary loader first
        Result<PromptTemplate> primaryResult = await _primaryLoader.LoadTemplateAsync(
            templateId,
            cancellationToken).ConfigureAwait(false);

        if (primaryResult.IsSuccess)
        {
            _logger.LogDebug("Successfully loaded template '{TemplateId}' from primary loader", templateId);
            return primaryResult;
        }

        // Primary failed - log and attempt fallback
        _logger.LogWarning(
            "Failed to load template '{TemplateId}' from primary loader: {Error}. Falling back to fallback loader.",
            templateId,
            primaryResult.Error);

        Result<PromptTemplate> fallbackResult = await _fallbackLoader.LoadTemplateAsync(
            templateId,
            cancellationToken).ConfigureAwait(false);

        if (fallbackResult.IsSuccess)
        {
            _logger.LogInformation(
                "Successfully loaded template '{TemplateId}' from fallback loader",
                templateId);
            return fallbackResult;
        }

        // Both loaders failed
        _logger.LogError(
            "Failed to load template '{TemplateId}' from both primary and fallback loaders. " +
            "Primary error: {PrimaryError}. Fallback error: {FallbackError}",
            templateId,
            primaryResult.Error,
            fallbackResult.Error);

        return Result<PromptTemplate>.Failure(
            $"Template '{templateId}' not found. Primary: {primaryResult.Error}. Fallback: {fallbackResult.Error}");
    }

    /// <inheritdoc />
    public async Task<Result<List<PromptTemplate>>> LoadAllTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Load from primary loader first
        Result<List<PromptTemplate>> primaryResult = await _primaryLoader.LoadAllTemplatesAsync(
            cancellationToken).ConfigureAwait(false);

        // If primary returns templates (even if empty list), use that
        if (primaryResult.IsSuccess && primaryResult.Value.Count > 0)
        {
            _logger.LogDebug(
                "Successfully loaded {Count} templates from primary loader",
                primaryResult.Value.Count);
            return primaryResult;
        }

        // Primary returned no templates - log and fall back
        if (primaryResult.IsSuccess && primaryResult.Value.Count == 0)
        {
            _logger.LogWarning(
                "No templates found in primary loader. Falling back to fallback loader.");
        }
        else
        {
            _logger.LogWarning(
                "Failed to load templates from primary loader: {Error}. Falling back to fallback loader.",
                primaryResult.Error);
        }

        Result<List<PromptTemplate>> fallbackResult = await _fallbackLoader.LoadAllTemplatesAsync(
            cancellationToken).ConfigureAwait(false);

        if (fallbackResult.IsSuccess)
        {
            _logger.LogInformation(
                "Successfully loaded {Count} templates from fallback loader",
                fallbackResult.Value.Count);
            return fallbackResult;
        }

        // Both failed
        _logger.LogError(
            "Failed to load templates from both primary and fallback loaders. " +
            "Primary error: {PrimaryError}. Fallback error: {FallbackError}",
            primaryResult.Error ?? "No templates found",
            fallbackResult.Error);

        // Return empty list rather than failure - graceful degradation
        return Result<List<PromptTemplate>>.Success(new List<PromptTemplate>());
    }
}
