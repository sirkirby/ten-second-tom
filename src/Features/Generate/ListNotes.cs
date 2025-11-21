using MediatR;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Generate;

public static class ListNotes
{
    public record Query : IRequest<Result<IReadOnlyList<NoteListItem>>>;

    public class Handler(
        IFileSystem fileSystem,
        Microsoft.Extensions.Options.IOptions<StorageOptions> storageOptions,
        ILogger<Handler> logger) : IRequestHandler<Query, Result<IReadOnlyList<NoteListItem>>>
    {
        private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        private readonly ILogger<Handler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly string _noteDirectory = Path.Combine(storageOptions.Value.GetEffectiveStorageDirectory(), DirectoryNames.Note);

        public async Task<Result<IReadOnlyList<NoteListItem>>> Handle(Query request, CancellationToken cancellationToken)
        {
            if (!_fileSystem.Directory.Exists(_noteDirectory))
            {
                return Result<IReadOnlyList<NoteListItem>>.Failure($"Note directory not found: {_noteDirectory}");
            }

            var files = _fileSystem.Directory.GetFiles(_noteDirectory, "*.md", SearchOption.TopDirectoryOnly);
            var notes = new List<NoteListItem>();

            foreach (var filePath in files)
            {
                var filename = _fileSystem.Path.GetFileName(filePath);

                // Exclude generated files
                if (filename.EndsWith("_generated.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);

                    // Check for recording-id in front matter
                    if (HasRecordingId(content))
                    {
                        continue; // It's a recording, not a note
                    }

                    var fileInfo = _fileSystem.FileInfo.New(filePath);
                    notes.Add(new NoteListItem
                    {
                        FileName = Path.GetFileNameWithoutExtension(filename),
                        FilePath = filePath,
                        LastModified = fileInfo.LastWriteTime
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process file {Filename}", filename);
                }
            }

            return Result<IReadOnlyList<NoteListItem>>.Success(notes.OrderByDescending(n => n.LastModified).ToList());
        }

        private static bool HasRecordingId(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            // Simple check: does it contain "recording-id:" within the first block?
            // We could parse YAML properly, but a simple string check is faster and likely sufficient for now.
            // We should ensure it's in the front matter.

            if (!content.StartsWith("---")) return false;

            var endOfFrontMatter = content.IndexOf("---", 3);
            if (endOfFrontMatter == -1) return false;

            var frontMatter = content.Substring(0, endOfFrontMatter);
            return frontMatter.Contains("recording-id:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
