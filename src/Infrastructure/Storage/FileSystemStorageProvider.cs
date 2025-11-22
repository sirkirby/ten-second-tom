using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// File system-based implementation of IMemoryStorageProvider.
/// Stores memory entries as markdown files with YAML frontmatter in a directory structure.
/// </summary>
public sealed partial class FileSystemStorageProvider : IMemoryStorageProvider
{
    private readonly string _baseDirectory;
    private readonly ILogger<FileSystemStorageProvider> _logger;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly IDeserializer _yamlDeserializer;

    /// <summary>
    /// Directories to exclude from memory entry enumeration.
    /// These directories contain non-memory files (e.g., templates, configuration).
    /// </summary>
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        DirectoryNames.Templates
    };

    private static readonly Regex DailyGeneratedFileRegex = new(
        "^(?<start>\\d{2}-\\d{2}-\\d{4})_(?<number>\\d+)_generated$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WeeklyGeneratedFileRegex = new(
        "^(?<start>\\d{2}-\\d{2}-\\d{4})_(?<end>\\d{2}-\\d{2}-\\d{4})_(?<number>\\d+)_generated$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemStorageProvider"/> class.
    /// </summary>
    /// <param name="baseDirectory">The base directory for storing memory files.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public FileSystemStorageProvider(string baseDirectory, ILogger<FileSystemStorageProvider> logger)
    {
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc/>
    public async Task<Result<MemoryEntry>> SaveAsync(MemoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            // Use the FilePath property from the entry itself (feature-owned logic)
            string relativePath = entry.FilePath;
            string filePath = Path.Combine(_baseDirectory, relativePath);
            string directory = Path.GetDirectoryName(filePath)!;

            // Create directory if it doesn't exist
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }

            // Generate markdown content with YAML frontmatter
            string content = GenerateMarkdownContent(entry);

            // Write to file
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Saved entry {EntryId} to {FilePath}", entry.EntryId, filePath);

            return Result<MemoryEntry>.Success(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save entry {EntryId}", entry.EntryId);
            return Result<MemoryEntry>.Failure($"Failed to save entry: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MemoryEntry>>> GetEntriesAsync(
        string command,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTime normalizedStartDate = startDate.Date;
            DateTime normalizedEndDate = endDate.Date;
            var entries = new List<MemoryEntry>();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string commandDirectory in GetCommandDirectories(command))
            {
                if (!Directory.Exists(commandDirectory))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(commandDirectory, "*.md", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!processedFiles.Add(file))
                    {
                        continue;
                    }

                    MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                    
                    if (entry != null &&
                        entry.Command.Equals(command, StringComparison.OrdinalIgnoreCase) &&
                        entry.Timestamp.Date >= normalizedStartDate &&
                        entry.Timestamp.Date <= normalizedEndDate)
                    {
                        entries.Add(entry);
                    }
                }
            }

            entries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            _logger.LogDebug("Retrieved {Count} entries for command {Command} between {StartDate} and {EndDate}",
                entries.Count, command, normalizedStartDate, normalizedEndDate);

            return Result<IReadOnlyList<MemoryEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries for command {Command}", command);
            return Result<IReadOnlyList<MemoryEntry>>.Failure($"Failed to retrieve entries: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MemoryEntry>>> GetGeneratedEntriesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTime normalizedStartDate = startDate.Date;
            DateTime normalizedEndDate = endDate.Date;
            var entries = new List<MemoryEntry>();
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string directory in GetCommandDirectories(CommandNames.Generate))
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                IEnumerable<string> files = Directory.EnumerateFiles(directory, "*_generated.md", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!processedFiles.Add(file))
                    {
                        continue;
                    }

                    MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);

                    if (entry == null)
                    {
                        continue;
                    }

                    if (entry.Command.Equals(CommandNames.ThisWeek, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug(
                            "Skipping weekly entry {EntryId} while aggregating generated entries",
                            entry.EntryId);
                        continue;
                    }

                    if (entry.Timestamp.Date >= normalizedStartDate && entry.Timestamp.Date <= normalizedEndDate)
                    {
                        entries.Add(entry);
                    }
                }
            }

            entries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            _logger.LogDebug(
                "Retrieved {Count} generated entries between {StartDate} and {EndDate}",
                entries.Count,
                normalizedStartDate,
                normalizedEndDate);

            return Result<IReadOnlyList<MemoryEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get generated entries between {StartDate} and {EndDate}", startDate, endDate);
            return Result<IReadOnlyList<MemoryEntry>>.Failure($"Failed to retrieve generated entries: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<int>> CountEntriesAsync(string command, DateTime targetDate, CancellationToken cancellationToken)
    {
        try
        {
            int count = 0;

            foreach (string commandDirectory in GetCommandDirectories(command))
            {
                if (!Directory.Exists(commandDirectory))
                {
                    continue;
                }

                count += CountEntriesByConvention(commandDirectory, command, targetDate);
            }

            _logger.LogDebug("Counted {Count} entries for command {Command} on {Date}", count, command, targetDate);

            return Task.FromResult(Result<int>.Success(count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count entries for command {Command}", command);
            return Task.FromResult(Result<int>.Failure($"Failed to count entries: {ex.Message}"));
        }
    }

    private static int CountEntriesByConvention(string commandDirectory, string command, DateTime targetDate)
        => command switch
        {
            CommandNames.Today => CountTodayEntries(commandDirectory, targetDate),
            CommandNames.ThisWeek => CountThisWeekEntries(commandDirectory, targetDate),
            _ => CountEntriesByScan(commandDirectory, targetDate)
        };

    private static int CountTodayEntries(string commandDirectory, DateTime targetDate)
    {
        if (!Directory.Exists(commandDirectory))
        {
            return 0;
        }

        string pattern = $"{targetDate:MM-dd-yyyy}_*_generated.md";
        return Directory.EnumerateFiles(commandDirectory, pattern, SearchOption.TopDirectoryOnly).Count();
    }

    private static int CountThisWeekEntries(string commandDirectory, DateTime targetDate)
    {
        if (!Directory.Exists(commandDirectory))
        {
            return 0;
        }

        var (start, end) = GetWeeklyRange(targetDate);
        string prefix = $"{start:MM-dd-yyyy}_{end:MM-dd-yyyy}_";

        return Directory.EnumerateFiles(commandDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Count(file => Path.GetFileName(file).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountEntriesByScan(string commandDirectory, DateTime targetDate)
    {
        if (!Directory.Exists(commandDirectory))
        {
            return 0;
        }

        string dateToken = targetDate.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
        return Directory.EnumerateFiles(commandDirectory, "*.md", SearchOption.AllDirectories)
            .Count(file => Path.GetFileName(file).Contains(dateToken, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string> GetCommandDirectories(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            yield break;
        }

        var normalized = command.ToLowerInvariant();

        var relativeDirectories = normalized switch
        {
            CommandNames.Today => new[] { DirectoryNames.Note, DirectoryNames.Today },
            CommandNames.Note => new[] { DirectoryNames.Note },
            CommandNames.ThisWeek => new[] { DirectoryNames.Note, DirectoryNames.ThisWeek },
            CommandNames.Generate => new[] { DirectoryNames.Note, DirectoryNames.Recording, DirectoryNames.Today },
            _ => new[] { normalized }
        };

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relative in relativeDirectories)
        {
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var fullPath = Path.Combine(_baseDirectory, relative);
            if (emitted.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<MemoryEntry>>> SearchEntriesAsync(
        string query,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = new List<MemoryEntry>();
            
            if (!Directory.Exists(_baseDirectory))
            {
                return Result<IReadOnlyList<MemoryEntry>>.Success(entries);
            }

            string[] files = Directory.GetFiles(_baseDirectory, "*.md", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Skip files in excluded directories
                if (IsExcludedPath(file))
                {
                    continue;
                }

                string content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                
                if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                    
                    if (entry != null)
                    {
                        bool withinDateRange = true;
                        
                        if (startDate.HasValue && entry.Timestamp.Date < startDate.Value.Date)
                        {
                            withinDateRange = false;
                        }
                        
                        if (endDate.HasValue && entry.Timestamp.Date > endDate.Value.Date)
                        {
                            withinDateRange = false;
                        }

                        if (withinDateRange)
                        {
                            entries.Add(entry);
                        }
                    }
                }
            }

            entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp)); // Newest first

            _logger.LogDebug("Found {Count} entries matching query '{Query}'", entries.Count, query);

            return Result<IReadOnlyList<MemoryEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search entries for query '{Query}'", query);
            return Result<IReadOnlyList<MemoryEntry>>.Failure($"Failed to search entries: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<int>> DeleteEntriesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        try
        {
            DateTime normalizedStartDate = startDate.Date;
            DateTime normalizedEndDate = endDate.Date;
            int deletedCount = 0;

            if (!Directory.Exists(_baseDirectory))
            {
                return Result<int>.Success(deletedCount);
            }

            string[] files = Directory.GetFiles(_baseDirectory, "*.md", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (IsExcludedPath(file))
                {
                    continue;
                }

                MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                
                if (entry != null &&
                    entry.Timestamp.Date >= normalizedStartDate &&
                    entry.Timestamp.Date <= normalizedEndDate)
                {
                    File.Delete(file);
                    deletedCount++;
                    _logger.LogDebug("Deleted entry {EntryId} from {FilePath}", entry.EntryId, file);
                }
            }

            _logger.LogInformation("Deleted {Count} entries between {StartDate} and {EndDate}",
                deletedCount, normalizedStartDate, normalizedEndDate);

            return Result<int>.Success(deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete entries");
            return Result<int>.Failure($"Failed to delete entries: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<int>> PurgeExpiredEntriesAsync(RetentionPolicy retentionPolicy, CancellationToken cancellationToken)
    {
        try
        {
            if (retentionPolicy == RetentionPolicy.Indefinite)
            {
                _logger.LogDebug("Retention policy is Indefinite, skipping purge");
                return Result<int>.Success(0);
            }

            DateTime cutoffDate = CalculateCutoffDate(retentionPolicy);
            
            _logger.LogInformation("Purging entries older than {CutoffDate} (Retention: {Policy})",
                cutoffDate, retentionPolicy);

            return await DeleteEntriesAsync(DateTime.MinValue, cutoffDate, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge expired entries");
            return Result<int>.Failure($"Failed to purge expired entries: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<MemoryEntry?>> GetEntryByIdAsync(string entryId, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(_baseDirectory))
            {
                return Result<MemoryEntry?>.Success(null);
            }

            string[] files = Directory.GetFiles(_baseDirectory, "*.md", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (IsExcludedPath(file))
                {
                    continue;
                }

                MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                
                if (entry != null && entry.EntryId == entryId)
                {
                    _logger.LogDebug("Found entry {EntryId} at {FilePath}", entryId, file);
                    return Result<MemoryEntry?>.Success(entry);
                }
            }

            _logger.LogDebug("Entry {EntryId} not found", entryId);
            return Result<MemoryEntry?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entry {EntryId}", entryId);
            return Result<MemoryEntry?>.Failure($"Failed to retrieve entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates markdown content with YAML frontmatter for a memory entry.
    /// </summary>
    private static string GenerateMarkdownContent(MemoryEntry entry)
    {
        var frontmatter = new Dictionary<string, object>
        {
            ["entry-id"] = entry.EntryId,
            ["command"] = entry.Command,
            ["timestamp"] = MarkdownFormatter.FormatTimestamp(entry.Timestamp),
            ["entry-number"] = entry.EntryNumber,
            ["llm-provider"] = entry.Metadata.LlmProvider,
            ["llm-model"] = entry.Metadata.LlmModel,
            ["tokens-used"] = entry.Metadata.TokensUsed,
            ["processing-duration"] = MarkdownFormatter.FormatDuration(entry.Metadata.ProcessingDuration)
        };

        var contentBody = new StringBuilder();
        contentBody.AppendLine("## User Input");
        contentBody.AppendLine();
        contentBody.AppendLine(entry.UserInput);
        contentBody.AppendLine();
        contentBody.AppendLine("## Summary");
        contentBody.AppendLine();
        contentBody.Append(entry.LlmResponse);

        return MarkdownFormatter.FormatWithYamlFrontMatter(frontmatter, contentBody.ToString());
    }

    /// <summary>
    /// Parses a markdown file and returns a MemoryEntry object.
    /// </summary>
    private async Task<MemoryEntry?> ParseMarkdownFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            MarkdownDocument document = Markdown.Parse(content, _markdownPipeline);

            // Extract YAML frontmatter
            YamlFrontMatterBlock? yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
            
            if (yamlBlock == null)
            {
                if (TryParseLegacyEntry(filePath, content, out MemoryEntry? legacyEntry))
                {
                    _logger.LogDebug("Parsed legacy entry without YAML frontmatter: {FilePath}", filePath);
                    return legacyEntry;
                }

                _logger.LogWarning("No YAML frontmatter found in file: {FilePath}", filePath);
                return null;
            }

            string yamlContent = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length)
                .Replace("---", string.Empty, StringComparison.Ordinal)
                .Trim();

            var frontmatter = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            string command = frontmatter.GetValueOrDefault("command")?.ToString() ?? CommandNames.Today;

            // Build metadata
            var metadata = new MemoryEntryMetadata
            {
                LlmProvider = frontmatter.GetValueOrDefault("llm-provider")?.ToString() ?? "Unknown",
                LlmModel = frontmatter.GetValueOrDefault("llm-model")?.ToString() ?? "Unknown",
                TokensUsed = Convert.ToInt32(frontmatter.GetValueOrDefault("tokens-used") ?? 0, CultureInfo.InvariantCulture),
                ProcessingDuration = TimeSpan.FromSeconds(Convert.ToDouble(frontmatter.GetValueOrDefault("processing-duration") ?? 0, CultureInfo.InvariantCulture))
            };

            // Handle different command types
            if (command == CommandNames.Generate)
            {
                // For generate entries, the entire content after YAML is the generated output
                string generatedContent = ExtractContentAfterYaml(content, yamlBlock);
                string relativePath = Path.GetRelativePath(_baseDirectory, filePath);

                return new MemoryEntry
                {
                    EntryId = frontmatter.GetValueOrDefault("entry-id")?.ToString() ?? string.Empty,
                    Command = command,
                    Timestamp = DateTimeOffset.Parse(frontmatter.GetValueOrDefault("timestamp")?.ToString() ?? DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                    EntryNumber = Convert.ToInt32(frontmatter.GetValueOrDefault("entry-number") ?? 0, CultureInfo.InvariantCulture),
                    UserInput = frontmatter.GetValueOrDefault("recording")?.ToString() ?? string.Empty, // Store recording name in UserInput for search
                    LlmResponse = generatedContent.Trim(),
                    Metadata = metadata,
                    FilePath = relativePath
                };
            }

            // Extract content sections for daily/weekly entries
            string userInput = ExtractSection(content, "## User Input", "## Summary");
            string llmResponse = ExtractSection(content, "## Summary", null);

            if (command == CommandNames.Today)
            {
                return new DailyEntry
                {
                    EntryId = frontmatter.GetValueOrDefault("entry-id")?.ToString() ?? string.Empty,
                    Command = command,
                    Timestamp = DateTimeOffset.Parse(frontmatter.GetValueOrDefault("timestamp")?.ToString() ?? DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                    EntryNumber = Convert.ToInt32(frontmatter.GetValueOrDefault("entry-number") ?? 1, CultureInfo.InvariantCulture),
                    UserInput = userInput,
                    LlmResponse = llmResponse,
                    Metadata = metadata
                };
            }
            else // thisweek
            {
                return new WeeklyEntry
                {
                    EntryId = frontmatter.GetValueOrDefault("entry-id")?.ToString() ?? string.Empty,
                    Command = command,
                    Timestamp = DateTimeOffset.Parse(frontmatter.GetValueOrDefault("timestamp")?.ToString() ?? DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                    EntryNumber = Convert.ToInt32(frontmatter.GetValueOrDefault("entry-number") ?? 1, CultureInfo.InvariantCulture),
                    UserInput = userInput,
                    LlmResponse = llmResponse,
                    Metadata = metadata
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse markdown file: {FilePath}", filePath);
            return null;
        }
    }

    private static MemoryEntryMetadata CreateUnknownMetadata()
        => new()
        {
            LlmProvider = "Unknown",
            LlmModel = "Unknown"
        };

    private bool TryParseLegacyEntry(string filePath, string content, out MemoryEntry? entry)
    {
        entry = null;

        string relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        Match weeklyMatch = WeeklyGeneratedFileRegex.Match(fileName);
        Match dailyMatch = DailyGeneratedFileRegex.Match(fileName);

        bool isRecording = PathContainsSegment(relativePath, DirectoryNames.Recording);

        if (weeklyMatch.Success)
        {
            if (!TryParseDateToken(weeklyMatch.Groups["start"].Value, out DateTime startDate) ||
                !TryParseDateToken(weeklyMatch.Groups["end"].Value, out DateTime endDate) ||
                !int.TryParse(weeklyMatch.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int entryNumber))
            {
                return false;
            }

            string userInput = ExtractSection(content, "## User Input", "## Summary");
            string llmResponse = ExtractSection(content, "## Summary", null);

            entry = new WeeklyEntry
            {
                EntryId = $"{CommandNames.ThisWeek}-{startDate:yyyy-MM-dd}-{entryNumber}",
                Command = CommandNames.ThisWeek,
                Timestamp = new DateTimeOffset(DateTime.SpecifyKind(endDate, DateTimeKind.Utc)),
                EntryNumber = entryNumber,
                UserInput = userInput,
                LlmResponse = llmResponse,
                Metadata = CreateUnknownMetadata()
            };

            return true;
        }

        if (dailyMatch.Success)
        {
            if (!TryParseDateToken(dailyMatch.Groups["start"].Value, out DateTime primaryDate) ||
                !int.TryParse(dailyMatch.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int entryNumber))
            {
                return false;
            }

            DateTimeOffset timestamp = new(DateTime.SpecifyKind(primaryDate, DateTimeKind.Utc));
            string userInput = ExtractSection(content, "## User Input", "## Summary");
            string llmResponse = ExtractSection(content, "## Summary", null);

            if (isRecording)
            {
                entry = new MemoryEntry
                {
                    EntryId = $"{CommandNames.Generate}-{primaryDate:yyyy-MM-dd}-{entryNumber}",
                    Command = CommandNames.Generate,
                    Timestamp = timestamp,
                    EntryNumber = entryNumber,
                    UserInput = string.Empty,
                    LlmResponse = string.IsNullOrWhiteSpace(llmResponse) ? content.Trim() : llmResponse,
                    Metadata = CreateUnknownMetadata(),
                    FilePath = relativePath
                };

                return true;
            }

            entry = new DailyEntry
            {
                EntryId = $"{CommandNames.Today}-{primaryDate:yyyy-MM-dd}-{entryNumber}",
                Command = CommandNames.Today,
                Timestamp = timestamp,
                EntryNumber = entryNumber,
                UserInput = userInput,
                LlmResponse = llmResponse,
                Metadata = CreateUnknownMetadata()
            };

            return true;
        }

        return false;
    }

    private static bool TryParseDateToken(string token, out DateTime date)
    {
        if (DateTime.TryParseExact(token, "MM-dd-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            date = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        date = default;
        return false;
    }

    private static bool PathContainsSegment(string relativePath, string segment)
    {
        string[] segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => s.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts all content that comes after the YAML frontmatter block.
    /// </summary>
    private static string ExtractContentAfterYaml(string content, YamlFrontMatterBlock yamlBlock)
    {
        // Find the end of the YAML block (after the closing ---)
        int yamlEnd = yamlBlock.Span.End;
        
        // Skip to the next line after the closing ---
        int contentStart = content.IndexOf('\n', yamlEnd);
        if (contentStart == -1)
        {
            return string.Empty;
        }
        
        return content.Substring(contentStart + 1);
    }

    /// <summary>
    /// Extracts a section from markdown content between two headers.
    /// </summary>
    private static string ExtractSection(string content, string startHeader, string? endHeader)
    {
        int startIndex = content.IndexOf(startHeader, StringComparison.Ordinal);
        
        if (startIndex == -1)
        {
            return string.Empty;
        }

        startIndex = content.IndexOf('\n', startIndex) + 1;

        int endIndex = endHeader != null
            ? content.IndexOf(endHeader, startIndex, StringComparison.Ordinal)
            : content.Length;

        if (endIndex == -1)
        {
            endIndex = content.Length;
        }

        return content.Substring(startIndex, endIndex - startIndex).Trim();
    }

    private static (DateTime Start, DateTime End) GetWeeklyRange(DateTime referenceDate)
    {
        var normalized = referenceDate.Date;
        var daysSinceMonday = ((int)normalized.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = normalized.AddDays(-daysSinceMonday);
        var end = start.AddDays(6);
        return (start, end);
    }

    /// <summary>
    /// Calculates the cutoff date for a given retention policy.
    /// </summary>
    private static DateTime CalculateCutoffDate(RetentionPolicy policy)
    {
        DateTime now = DateTime.UtcNow;

        return policy switch
        {
            RetentionPolicy.Days30 => now.AddDays(-30),
            RetentionPolicy.Days90 => now.AddDays(-90),
            RetentionPolicy.OneYear => now.AddYears(-1),
            RetentionPolicy.TwoYears => now.AddYears(-2),
            _ => DateTime.MinValue
        };
    }

    /// <summary>
    /// Determines whether a file path is within an excluded directory.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file is in an excluded directory; otherwise, false.</returns>
    private bool IsExcludedPath(string filePath)
    {
        // Get the relative path from the base directory
        string relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        
        // Split the path into segments
        string[] pathSegments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        // Check if any segment matches an excluded directory
        return pathSegments.Any(segment => ExcludedDirectories.Contains(segment));
    }
}

