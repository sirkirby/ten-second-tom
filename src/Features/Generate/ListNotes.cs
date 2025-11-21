using MediatR;
using Microsoft.Extensions.Logging;
using System.Globalization;
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

                    // Try to parse date from YAML front matter, fall back to file LastWriteTime
                    var fileInfo = _fileSystem.FileInfo.New(filePath);
                    DateTimeOffset lastModified;
                    if (TryParseDateFromFrontMatter(content, out var parsedDate))
                    {
                        lastModified = parsedDate;
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Could not parse date from YAML front matter for {Filename}, using file LastWriteTime",
                            filename);
                        lastModified = new DateTimeOffset(fileInfo.LastWriteTime);
                    }

                    notes.Add(new NoteListItem
                    {
                        FileName = Path.GetFileNameWithoutExtension(filename),
                        FilePath = filePath,
                        LastModified = lastModified
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

        /// <summary>
        /// Tries to parse the date from the YAML front matter of a note file.
        /// </summary>
        /// <param name="content">The file content with YAML front matter.</param>
        /// <param name="date">The parsed date if successful.</param>
        /// <returns>True if the date was successfully parsed, false otherwise.</returns>
        private bool TryParseDateFromFrontMatter(string content, out DateTimeOffset date)
        {
            date = default;

            try
            {
                // Check if content starts with front matter
                if (!content.TrimStart().StartsWith("---", StringComparison.Ordinal))
                {
                    return false;
                }

                // Split to extract front matter block
                var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

                // Find first and second --- delimiters
                int firstDelim = -1, secondDelim = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim() == "---")
                    {
                        if (firstDelim == -1)
                            firstDelim = i;
                        else if (secondDelim == -1)
                        {
                            secondDelim = i;
                            break;
                        }
                    }
                }

                if (firstDelim == -1 || secondDelim == -1)
                {
                    return false;
                }

                // Extract YAML content between delimiters
                var yamlLines = lines.Skip(firstDelim + 1).Take(secondDelim - firstDelim - 1);
                var yamlContent = string.Join("\n", yamlLines);

                // Use YamlDotNet to deserialize the front matter
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.HyphenatedNamingConvention.Instance)
                    .Build();

                var frontMatter = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                // Look for either 'timestamp' or 'date' field (recordings use 'timestamp', notes use 'date')
                object? dateValue = null;
                if (frontMatter != null)
                {
                    if (!frontMatter.TryGetValue("timestamp", out dateValue))
                    {
                        frontMatter.TryGetValue("date", out dateValue);
                    }
                }

                if (dateValue != null)
                {
                    // Try to parse the date string
                    if (dateValue is string dateStr)
                    {
                        // Try ISO 8601 format first (used by recordings: 2025-10-27T17:39:44.5350960+00:00)
                        if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        {
                            return true;
                        }

                        // Try yyyy-MM-dd HH:mm:ss format (legacy/notes format)
                        if (DateTimeOffset.TryParseExact(
                            dateStr,
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out date))
                        {
                            return true;
                        }
                    }
                    else if (dateValue is DateTime dateTime)
                    {
                        date = new DateTimeOffset(dateTime);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse date from YAML front matter");
                return false;
            }
        }
    }
}
