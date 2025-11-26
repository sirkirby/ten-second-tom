using Spectre.Console;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Console input wrapper that throws PromptCancelledException on Escape key.
/// Used with CancellablePrompt to enable escape-key cancellation of Spectre.Console prompts.
/// </summary>
public sealed class EscapeCancellableInput : IAnsiConsoleInput
{
    /// <summary>
    /// Checks if a key is available in the input buffer.
    /// </summary>
    public bool IsKeyAvailable() => Console.KeyAvailable;

    /// <summary>
    /// Reads a key from the console, throwing PromptCancelledException if Escape is pressed.
    /// </summary>
    /// <param name="intercept">True to intercept the key (not display it).</param>
    /// <returns>The key info, or throws if Escape was pressed.</returns>
    /// <exception cref="PromptCancelledException">Thrown when Escape key is pressed.</exception>
    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        var key = Console.ReadKey(intercept);

        if (key.Key == ConsoleKey.Escape)
        {
            throw new PromptCancelledException();
        }

        return key;
    }

    /// <summary>
    /// Async version of ReadKey. Spectre.Console prompts are synchronous, so this
    /// delegates to the sync version.
    /// </summary>
    /// <param name="intercept">True to intercept the key (not display it).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The key info, or throws if Escape was pressed.</returns>
    public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        // Spectre.Console prompts are synchronous, so we use sync implementation
        return Task.FromResult(ReadKey(intercept));
    }
}
