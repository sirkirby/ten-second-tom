namespace TenSecondTom.Features.Shell.Models;

/// <summary>
/// Encapsulates the outcome of a command execution.
/// Used for communication between command handlers and the REPL loop.
/// </summary>
public sealed record CommandResult
{
    /// <summary>
    /// True if the command completed without errors.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Success message or error description (user-friendly, no stack traces).
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Exception object if execution failed (for logging, not user display).
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Creates a successful result with optional message.
    /// </summary>
    public static CommandResult Success(string? message = null) =>
        new() { IsSuccess = true, Message = message };

    /// <summary>
    /// Creates a failure result with error message and optional exception.
    /// </summary>
    public static CommandResult Failure(string message, Exception? error = null) =>
        new() { IsSuccess = false, Message = message, Error = error };

    /// <summary>
    /// Creates a result indicating the command was interrupted via Ctrl+C.
    /// </summary>
    public static CommandResult Interrupted() =>
        new() { IsSuccess = true, Message = "(interrupted)" };
}
