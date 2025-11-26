using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Abstractions.UI;

/// <summary>
/// Interface for setup wizard user interface
/// Abstracts the interactive prompting logic for testability
/// </summary>
public interface ISetupWizardUI
{
    /// <summary>
    /// Prompts user to select an SSH key from available options
    /// </summary>
    Task<SshKeyInfo?> PromptForSshKeyAsync(
        IReadOnlyList<SshKeyInfo> availableKeys,
        SshKeyInfo? currentKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to select an LLM provider
    /// </summary>
    Task<LlmProvider?> PromptForLlmProviderAsync(
        LlmProvider? currentProvider,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to select a model for the given provider
    /// </summary>
    /// <param name="provider">The LLM provider to select a model for</param>
    /// <param name="currentModelId">The currently configured model ID, if any</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The selected model, or null if cancelled</returns>
    Task<SupportedModel?> PromptForModelAsync(
        LlmProvider provider,
        string? currentModelId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter an API key (masked input)
    /// </summary>
    Task<string?> PromptForApiKeyAsync(
        LlmProvider provider,
        string? currentApiKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter memory directory path (legacy - use PromptForRootDirectoryAsync for new code)
    /// </summary>
    [Obsolete("Use PromptForRootDirectoryAsync instead. This method is for backward compatibility only.", false)]
    Task<string?> PromptForMemoryDirectoryAsync(
        string? currentDirectory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter root directory path for all Ten Second Tom data
    /// </summary>
    Task<string?> PromptForRootDirectoryAsync(
        string? currentDirectory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to select a storage provider
    /// </summary>
    /// <param name="availableProviders">List of available storage providers</param>
    /// <param name="currentProviderId">Currently selected provider ID, if any</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected storage provider metadata, or null if cancelled</returns>
    Task<Infrastructure.Storage.StorageProviderMetadata?> PromptForStorageProviderAsync(
        IReadOnlyList<Infrastructure.Storage.StorageProviderMetadata> availableProviders,
        string? currentProviderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter Obsidian vault path with validation
    /// </summary>
    /// <param name="currentPath">Current vault path, if any</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vault path, or null if cancelled</returns>
    Task<string?> PromptForObsidianVaultPathAsync(
        string? currentPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user for a subdirectory name within the root directory
    /// </summary>
    /// <param name="prompt">The question to ask the user</param>
    /// <param name="currentValue">Current subdirectory value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Subdirectory name, or null if cancelled or empty string if user wants root level</returns>
    Task<string?> PromptForSubdirectoryAsync(
        string prompt,
        string? currentValue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to select log level
    /// </summary>
    Task<Microsoft.Extensions.Logging.LogLevel?> PromptForLogLevelAsync(
        Microsoft.Extensions.Logging.LogLevel? currentLevel,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter retention days
    /// </summary>
    Task<int?> PromptForRetentionDaysAsync(
        int? currentDays,
        CancellationToken cancellationToken);

    /// <summary>
    /// Displays configuration summary and prompts for confirmation
    /// </summary>
    Task<bool> ShowSummaryAndConfirmAsync(
        Features.Setup.Models.SetupSummary summary,
        CancellationToken cancellationToken);

    /// <summary>
    /// Shows a step header (e.g., "Step 1 of 8: SSH Key Configuration")
    /// </summary>
    void ShowStepHeader(int currentStep, int totalSteps, string stepName);

    /// <summary>
    /// Shows a status message during long-running operations
    /// </summary>
    void ShowStatus(string message);

    /// <summary>
    /// Runs an async operation with a progress bar.
    /// The operation receives a progress callback that reports percentage (0-100).
    /// </summary>
    /// <param name="taskDescription">Description shown next to progress bar</param>
    /// <param name="operation">The async operation that reports progress (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RunWithProgressAsync(
        string taskDescription,
        Func<Action<double>, Task> operation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Shows a success message
    /// </summary>
    void ShowSuccess(string message);

    /// <summary>
    /// Shows an error message
    /// </summary>
    void ShowError(string message);

    /// <summary>
    /// Shows a warning message
    /// </summary>
    void ShowWarning(string message);

    /// <summary>
    /// Prompts user to enter input volume multiplier
    /// </summary>
    /// <param name="currentValue">Current input volume value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected volume, or null if cancelled</returns>
    Task<double?> PromptForInputVolumeAsync(
        double? currentValue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user for a boolean setting with enable/disable options
    /// </summary>
    /// <param name="prompt">The question to ask the user</param>
    /// <param name="currentValue">Current boolean value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected boolean value, or null if cancelled</returns>
    Task<bool?> PromptForBooleanAsync(
        string prompt,
        bool? currentValue,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter an integer value within a range
    /// </summary>
    /// <param name="prompt">The question to ask the user</param>
    /// <param name="currentValue">Current integer value</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected integer, or null if cancelled</returns>
    Task<int?> PromptForIntAsync(
        string prompt,
        int? currentValue,
        int min,
        int max,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to select a speech-to-text provider
    /// </summary>
    /// <param name="currentProvider">Current STT provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected STT provider, or null if cancelled</returns>
    Task<string?> PromptForSttProviderAsync(
        string? currentProvider,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prompts user to enter an API key for the STT provider
    /// </summary>
    /// <param name="provider">The STT provider requiring an API key</param>
    /// <param name="currentApiKey">Current API key, if any</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>API key, or null if cancelled</returns>
    Task<string?> PromptForSttApiKeyAsync(
        string provider,
        string? currentApiKey,
        CancellationToken cancellationToken);



    /// <summary>
    /// Prompts user for a generic string input
    /// </summary>
    /// <param name="prompt">The prompt text to display</param>
    /// <param name="defaultValue">Optional default value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User input, or null if cancelled</returns>
    Task<string?> PromptForStringAsync(
        string prompt,
        string? defaultValue,
        CancellationToken cancellationToken);
    /// <summary>
    /// Prompts user to select an item from a list of options
    /// </summary>
    /// <typeparam name="T">The type of item to select</typeparam>
    /// <param name="prompt">The prompt text to display</param>
    /// <param name="options">The list of available options</param>
    /// <param name="displaySelector">Function to get display string for an item</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected item, or default(T) if cancelled</returns>
    Task<T?> PromptForSelectionAsync<T>(
        string prompt,
        IReadOnlyList<T> options,
        Func<T, string> displaySelector,
        CancellationToken cancellationToken)
        where T : class;
    /// <summary>
    /// Prompts user for local LLM configuration with inline connectivity verification.
    /// Similar to PromptForObsidianVaultPathAsync, this validates the configuration before returning.
    /// </summary>
    /// <param name="currentBaseUrl">Current base URL, if any</param>
    /// <param name="currentModel">Current model name, if any</param>
    /// <param name="httpClientFactory">HTTP client factory for making verification requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (baseUrl, modelName), or null if cancelled</returns>
    Task<(string baseUrl, string modelName)?> PromptForLocalLlmConfigurationAsync(
        string? currentBaseUrl,
        string? currentModel,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken);
}
