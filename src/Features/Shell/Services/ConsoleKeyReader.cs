namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Abstracts console key reading and output for testability.
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

    /// <summary>
    /// Gets the width of the console window.
    /// </summary>
    int WindowWidth { get; }

    /// <summary>
    /// Gets the current cursor row position.
    /// </summary>
    int CursorTop { get; }

    /// <summary>
    /// Writes a string to the console.
    /// </summary>
    void Write(string value);

    /// <summary>
    /// Writes a character to the console.
    /// </summary>
    void Write(char value);

    /// <summary>
    /// Writes a line terminator to the console.
    /// </summary>
    void WriteLine();

    /// <summary>
    /// Sets the cursor position.
    /// </summary>
    void SetCursorPosition(int left, int top);

    /// <summary>
    /// Writes text with Spectre.Console markup.
    /// </summary>
    void WriteMarkup(string markup);
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

    /// <inheritdoc/>
    public int WindowWidth => Console.WindowWidth;

    /// <inheritdoc/>
    public int CursorTop => Console.CursorTop;

    /// <inheritdoc/>
    public void Write(string value) => Console.Write(value);

    /// <inheritdoc/>
    public void Write(char value) => Console.Write(value);

    /// <inheritdoc/>
    public void WriteLine() => Console.WriteLine();

    /// <inheritdoc/>
    public void SetCursorPosition(int left, int top) => Console.SetCursorPosition(left, top);

    /// <inheritdoc/>
    public void WriteMarkup(string markup) => Spectre.Console.AnsiConsole.Markup(markup);
}
