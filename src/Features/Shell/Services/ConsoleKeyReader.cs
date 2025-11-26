namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Abstracts console key reading for testability.
/// </summary>
public interface IConsoleKeyReader
{
    /// <summary>
    /// Gets a value indicating whether a key press is available in the input stream.
    /// </summary>
    bool KeyAvailable { get; }

    /// <summary>
    /// Obtains the next key pressed by the user.
    /// </summary>
    /// <param name="intercept">True to not display the pressed key.</param>
    /// <returns>Information about the key pressed.</returns>
    ConsoleKeyInfo ReadKey(bool intercept);

    /// <summary>
    /// Gets a value indicating whether input has been redirected (non-interactive).
    /// </summary>
    bool IsInputRedirected { get; }
}

/// <summary>
/// Default implementation using System.Console.
/// </summary>
public sealed class SystemConsoleKeyReader : IConsoleKeyReader
{
    /// <inheritdoc/>
    public bool KeyAvailable => Console.KeyAvailable;

    /// <inheritdoc/>
    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

    /// <inheritdoc/>
    public bool IsInputRedirected => Console.IsInputRedirected;
}
