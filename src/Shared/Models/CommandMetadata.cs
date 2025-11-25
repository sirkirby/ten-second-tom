namespace TenSecondTom.Shared.Models;

/// <summary>
/// Describes an available slash command for autocomplete and help display.
/// </summary>
public sealed record CommandMetadata
{
    /// <summary>
    /// Command name including slash prefix (e.g., "/today").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Brief description for user guidance and help display.
    /// </summary>
    public required string HelpText { get; init; }

    /// <summary>
    /// Alternative names for the command (e.g., ["/exit"] for "/quit").
    /// </summary>
    public string[]? Aliases { get; init; }

    /// <summary>
    /// True if the command requires an active authentication session.
    /// </summary>
    public bool RequiresAuthentication { get; init; }

    /// <summary>
    /// Validates command metadata constraints.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Name) &&
        Name.StartsWith('/') &&
        Name.Length is >= 2 and <= 20 &&
        !Name.Contains(' ') &&
        !string.IsNullOrWhiteSpace(HelpText) &&
        HelpText.Length is >= 10 and <= 200 &&
        (Aliases == null || Aliases.All(a => a.StartsWith('/') && !a.Contains(' ')));

    /// <summary>
    /// Static catalog of all available commands.
    /// </summary>
    public static readonly CommandMetadata[] CommandCatalog =
    [
        new() { Name = "/note", HelpText = "Capture a quick note without AI processing", RequiresAuthentication = true },
        new() { Name = "/today", HelpText = "Capture today's reflection with 3-5 prompts", RequiresAuthentication = true },
        new() { Name = "/thisweek", HelpText = "Generate a weekly review from recent daily entries", RequiresAuthentication = true },
        new() { Name = "/search", HelpText = "Search memory entries by text query", RequiresAuthentication = true },
        new() { Name = "/record", HelpText = "Record audio with transcription and save to library", RequiresAuthentication = true },
        new() { Name = "/transcribe", HelpText = "Transcribe existing note/recording audio into the library", RequiresAuthentication = true },
        new() { Name = "/llm", HelpText = "List and download models for configured LLM provider", RequiresAuthentication = true },
        new() { Name = "/generate", HelpText = "Generate output from a recording using a prompt template", RequiresAuthentication = true },
        new() { Name = "/setup", HelpText = "Run guided setup wizard to configure Ten Second Tom", RequiresAuthentication = false },
        new() { Name = "/config", HelpText = "View and manage configuration settings", RequiresAuthentication = false },
        new() { Name = "/login", HelpText = "Authenticate with SSH key and create a session", RequiresAuthentication = false },
        new() { Name = "/logout", HelpText = "Log out and invalidate the current session", RequiresAuthentication = true },
        new() { Name = "/quit", HelpText = "Exit the shell", RequiresAuthentication = false, Aliases = ["/exit"] },
        new() { Name = "/help", HelpText = "Display available commands with descriptions", RequiresAuthentication = false },
        new() { Name = "/version", HelpText = "Display version information", RequiresAuthentication = false }
    ];
}
