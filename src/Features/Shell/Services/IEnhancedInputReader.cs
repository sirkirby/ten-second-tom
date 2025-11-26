namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Provides enhanced input reading for REPL with Tab completion, history navigation, and escape support.
/// Dependencies injected via constructor following project patterns.
/// </summary>
public interface IEnhancedInputReader
{
    /// <summary>
    /// Checks if enhanced input reader is available (interactive terminal).
    /// </summary>
    /// <returns>True if terminal supports interactive input, false otherwise.</returns>
    bool IsAvailable();

    /// <summary>
    /// Reads user input with Tab completion, history navigation, and escape key support.
    /// Uses IAutocompleteEngine and ISessionManager injected via constructor.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>Command string if submitted, null if cancelled (Escape key).</returns>
    Task<string?> ReadInputAsync(CancellationToken cancellationToken = default);
}
