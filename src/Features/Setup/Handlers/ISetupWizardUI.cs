using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Handlers;

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
    /// Prompts user to enter memory directory path
    /// </summary>
    Task<string?> PromptForMemoryDirectoryAsync(
        string? currentDirectory,
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
        ConfigurationSettings settings,
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
}
