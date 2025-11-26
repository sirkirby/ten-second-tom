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
    /// Note: /today, /thisweek removed - use /note for quick notes
    /// Note: /setup removed - use /config all instead
    /// </summary>
    public static readonly CommandMetadata[] CommandCatalog =
    [
        // Core commands
        new() { Name = "/note", HelpText = "Capture a quick note without AI processing (list)", RequiresAuthentication = true },
        new() { Name = "/search", HelpText = "Search memory entries by text query", RequiresAuthentication = true },
        new() { Name = "/generate", HelpText = "Generate output from notes or recordings (note, recording)", RequiresAuthentication = true },

        // Audio commands
        new() { Name = "/audio", HelpText = "Audio configuration and management (config)", RequiresAuthentication = false },
        new() { Name = "/record", HelpText = "Record audio with transcription and save to library", RequiresAuthentication = true },
        new() { Name = "/transcribe", HelpText = "Transcribe recordings or external audio files", RequiresAuthentication = true },

        // Configuration commands (parent command with subcommands)
        new() { Name = "/config", HelpText = "View and manage configuration (show, set, all, llm, audio, storage)", RequiresAuthentication = false },
        new() { Name = "/llm", HelpText = "List and download models for configured LLM provider", RequiresAuthentication = true },

        // Auth commands (parent command with subcommands)
        new() { Name = "/auth", HelpText = "Authentication management (login, logout, config)", RequiresAuthentication = false },
        new() { Name = "/login", HelpText = "Authenticate with SSH key and create a session", RequiresAuthentication = false },
        new() { Name = "/logout", HelpText = "Log out and invalidate the current session", RequiresAuthentication = true },

        // Storage commands (parent command with subcommands)
        new() { Name = "/storage", HelpText = "Storage management (config, list-providers)", RequiresAuthentication = false },

        // Shell commands
        new() { Name = "/quit", HelpText = "Exit the shell", RequiresAuthentication = false, Aliases = ["/exit"] },
        new() { Name = "/help", HelpText = "Display available commands with descriptions", RequiresAuthentication = false },
        new() { Name = "/version", HelpText = "Display version information", RequiresAuthentication = false }
    ];
}
