using Spectre.Console;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Provides escape-key cancellable prompts using Spectre.Console.
/// Wraps standard prompts to detect Escape key and return null/throw OperationCanceledException.
/// </summary>
public static class CancellablePrompt
{
    /// <summary>
    /// Shows a selection prompt that can be cancelled with Escape key.
    /// </summary>
    /// <typeparam name="T">The type of items to select from.</typeparam>
    /// <param name="configure">Action to configure the selection prompt.</param>
    /// <returns>Selected item, or default if Escape was pressed.</returns>
    public static T? Selection<T>(Action<SelectionPrompt<T>> configure) where T : class
    {
        var prompt = new SelectionPrompt<T>();
        configure(prompt);

        // Create a console with cancellable input
        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a selection prompt that can be cancelled with Escape key.
    /// For value types, returns a nullable.
    /// </summary>
    /// <typeparam name="T">The type of items to select from.</typeparam>
    /// <param name="configure">Action to configure the selection prompt.</param>
    /// <returns>Selected item, or null if Escape was pressed.</returns>
    public static T? SelectionValue<T>(Action<SelectionPrompt<T>> configure) where T : struct
    {
        var prompt = new SelectionPrompt<T>();
        configure(prompt);

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a text prompt that can be cancelled with Escape key.
    /// </summary>
    /// <param name="promptText">The prompt text to display.</param>
    /// <param name="configure">Optional action to further configure the text prompt.</param>
    /// <returns>Entered text, or null if Escape was pressed.</returns>
    public static string? Text(string promptText, Action<TextPrompt<string>>? configure = null)
    {
        var prompt = new TextPrompt<string>(promptText);
        configure?.Invoke(prompt);

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a confirmation prompt that can be cancelled with Escape key.
    /// </summary>
    /// <param name="message">The confirmation message to display.</param>
    /// <param name="defaultValue">Default value if user just presses Enter.</param>
    /// <returns>True/false for user choice, null if Escape was pressed.</returns>
    public static bool? Confirm(string message, bool defaultValue = true)
    {
        var prompt = new ConfirmationPrompt(message)
        {
            DefaultValue = defaultValue
        };

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a multi-selection prompt that can be cancelled with Escape key.
    /// </summary>
    /// <typeparam name="T">The type of items to select from.</typeparam>
    /// <param name="configure">Action to configure the multi-selection prompt.</param>
    /// <returns>List of selected items, or null if Escape was pressed.</returns>
    public static List<T>? MultiSelection<T>(Action<MultiSelectionPrompt<T>> configure) where T : notnull
    {
        var prompt = new MultiSelectionPrompt<T>();
        configure(prompt);

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates an IAnsiConsole with escape-cancellable input.
    /// Uses EscapeCancellableConsole wrapper to intercept Escape key presses.
    /// </summary>
    private static EscapeCancellableConsole CreateCancellableConsole()
    {
        return new EscapeCancellableConsole();
    }
}

/// <summary>
/// Exception thrown when user presses Escape to cancel a prompt.
/// </summary>
public sealed class PromptCancelledException : OperationCanceledException
{
    public PromptCancelledException() : base("Prompt cancelled by user (Escape key)")
    {
    }
}
