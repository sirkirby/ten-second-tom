using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Storage;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Note;

/// <summary>
/// Creates a quick note entry without LLM processing.
/// Notes are simpler than daily entries and store raw user content.
/// </summary>
public static class CreateNote
{
    /// <summary>
    /// Command to create a quick note entry.
    /// Captures user's raw content without AI enhancement.
    /// </summary>
    public sealed record Command : IRequest<Result<Shared.Models.Note>>
    {
        /// <summary>
        /// Gets the note content.
        /// Must not be null, empty, or whitespace-only.
        /// </summary>
        public required string Content { get; init; }

        /// <summary>
        /// Gets a value indicating whether this note was captured via voice.
        /// </summary>
        public bool IsVoiceNote { get; init; }

        /// <summary>
        /// Gets the audio file path if this note was captured via voice.
        /// Optional - only set when IsVoiceNote is true.
        /// </summary>
        public string? AudioFilePath { get; init; }
    }

    /// <summary>
    /// Handles the creation of note entries.
    /// Orchestrates validation, authentication, and storage without LLM interaction.
    /// </summary>
    public sealed class Handler(
        IMemoryStorageProvider storage,
        IAuthenticationService authService,
        IOptions<StorageOptions> storageOptions,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<Shared.Models.Note>>
    {
        /// <summary>
        /// Handles the CreateNote command to create a new note entry.
        /// </summary>
        /// <param name="request">The command containing note content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result containing the created Note or an error.</returns>
        public async Task<Result<Shared.Models.Note>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // 1. Validate command
            Result<Shared.Models.Note> validationResult = ValidateCommand(request);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // 2. Check authentication
            bool isAuthenticated = await authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
            if (!isAuthenticated)
            {
                return Result<Shared.Models.Note>.Failure("Authentication required. Please authenticate first.");
            }

            // 3. Determine entry number for today in the note directory
            // Notes use the "note" command name for counting to share numbering with today entries (future)
            DateTime today = DateTime.UtcNow.Date;
            Result<int> countResult = await storage.CountEntriesAsync(CommandNames.Note, today, cancellationToken).ConfigureAwait(false);
            if (!countResult.IsSuccess)
            {
                return Result<Shared.Models.Note>.Failure($"Failed to determine entry number: {countResult.Error}");
            }

            int entryNumber = countResult.Value + 1;

            // 4. Create Note (no LLM processing required)
            var note = new Shared.Models.Note
            {
                EntryId = $"{CommandNames.Note}-{today:MM-dd-yyyy}-{entryNumber}",
                Command = CommandNames.Note,
                Timestamp = DateTimeOffset.UtcNow,
                EntryNumber = entryNumber,
                Content = request.Content.Trim(),
                IsVoiceNote = request.IsVoiceNote,
                AudioFilePath = request.AudioFilePath
            };

            // 5. Save to storage
            Result<bool> saveResult = await SaveNoteAsync(note, cancellationToken).ConfigureAwait(false);
            if (!saveResult.IsSuccess)
            {
                return Result<Shared.Models.Note>.Failure($"Failed to save note: {saveResult.Error}");
            }

            logger.LogInformation("Created note entry {EntryId}", note.EntryId);
            return Result<Shared.Models.Note>.Success(note);
        }

        /// <summary>
        /// Validates the CreateNote command.
        /// </summary>
        private static Result<Shared.Models.Note> ValidateCommand(Command request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Result<Shared.Models.Note>.Failure("Note content cannot be null, empty, or whitespace");
            }

            if (request.IsVoiceNote && string.IsNullOrWhiteSpace(request.AudioFilePath))
            {
                return Result<Shared.Models.Note>.Failure("Audio file path is required for voice notes");
            }

            return Result<Shared.Models.Note>.Success(null!); // Validation passed, but no note created yet
        }

        /// <summary>
        /// Saves a note to storage by constructing the markdown file content.
        /// </summary>
        private async Task<Result<bool>> SaveNoteAsync(
            Shared.Models.Note note,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get the effective storage directory using extension method
                var storageRoot = storageOptions.Value.GetEffectiveStorageDirectory();

                // Build markdown content for the note
                var markdownContent = BuildNoteMarkdown(note);

                // Get the file path and ensure directory exists
                var directoryPath = Path.Combine(storageRoot, DirectoryNames.Note);
                Directory.CreateDirectory(directoryPath);

                // Write the file
                var filePath = Path.Combine(storageRoot, note.FilePath);
                await File.WriteAllTextAsync(filePath, markdownContent, cancellationToken).ConfigureAwait(false);

                logger.LogDebug("Saved note to {FilePath}", filePath);
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save note {EntryId}", note.EntryId);
                return Result<bool>.Failure($"Failed to save note: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds the markdown content for a note file.
        /// </summary>
        private static string BuildNoteMarkdown(Shared.Models.Note note)
        {
            var lines = new List<string>
            {
                $"# Note - {note.Timestamp:yyyy-MM-dd HH:mm:ss}",
                "",
                $"**Entry ID:** {note.EntryId}",
                $"**Type:** {(note.IsVoiceNote ? "Voice Note" : "Text Note")}",
                $"**Entry Number:** {note.EntryNumber}",
                ""
            };

            if (note.IsVoiceNote && !string.IsNullOrWhiteSpace(note.AudioFilePath))
            {
                lines.Add($"**Audio File:** {note.AudioFilePath}");
                lines.Add("");
            }

            lines.Add("## Content");
            lines.Add("");
            lines.Add(note.Content);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
