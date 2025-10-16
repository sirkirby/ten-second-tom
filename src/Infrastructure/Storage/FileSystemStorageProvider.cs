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
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;

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

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
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
            string filePath = GetFilePathForEntry(entry);
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
            string commandDirectory = Path.Combine(_baseDirectory, command);

            if (!Directory.Exists(commandDirectory))
            {
                return Result<IReadOnlyList<MemoryEntry>>.Success(Array.Empty<MemoryEntry>());
            }

            var entries = new List<MemoryEntry>();
            string[] files = Directory.GetFiles(commandDirectory, "*.md", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                
                if (entry != null && entry.Timestamp.Date >= startDate && entry.Timestamp.Date <= endDate)
                {
                    entries.Add(entry);
                }
            }

            entries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            _logger.LogDebug("Retrieved {Count} entries for command {Command} between {StartDate} and {EndDate}",
                entries.Count, command, startDate, endDate);

            return Result<IReadOnlyList<MemoryEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries for command {Command}", command);
            return Result<IReadOnlyList<MemoryEntry>>.Failure($"Failed to retrieve entries: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<int>> CountEntriesAsync(string command, DateTime targetDate, CancellationToken cancellationToken)
    {
        try
        {
            string commandDirectory = Path.Combine(_baseDirectory, command);

            if (!Directory.Exists(commandDirectory))
            {
                return Result<int>.Success(0);
            }

            string[] files = Directory.GetFiles(commandDirectory, "*.md", SearchOption.AllDirectories);
            int count = 0;

            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                
                if (entry != null && entry.Timestamp.Date == targetDate.Date)
                {
                    count++;
                }
            }

            _logger.LogDebug("Counted {Count} entries for command {Command} on {Date}", count, command, targetDate);

            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count entries for command {Command}", command);
            return Result<int>.Failure($"Failed to count entries: {ex.Message}");
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

                MemoryEntry? entry = await ParseMarkdownFileAsync(file, cancellationToken).ConfigureAwait(false);
                
                if (entry != null && entry.Timestamp.Date >= startDate && entry.Timestamp.Date <= endDate)
                {
                    File.Delete(file);
                    deletedCount++;
                    _logger.LogDebug("Deleted entry {EntryId} from {FilePath}", entry.EntryId, file);
                }
            }

            _logger.LogInformation("Deleted {Count} entries between {StartDate} and {EndDate}",
                deletedCount, startDate, endDate);

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
    /// Gets the file path for a memory entry based on its command and date.
    /// </summary>
    private string GetFilePathForEntry(MemoryEntry entry)
    {
        string directory = Path.Combine(_baseDirectory, entry.Command);
        string fileName;

    if (entry.Command == CommandNames.Today)
        {
            // Daily entries: MM-DD-YYYY_N.md
            fileName = $"{entry.Timestamp.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture)}_{entry.EntryNumber}.md";
        }
        else // thisweek
        {
            // Weekly entries: YYYY-WW_N.md (WW = ISO week number)
            int weekNumber = GetIso8601WeekNumber(entry.Timestamp.DateTime);
            fileName = $"{entry.Timestamp.Year}-{weekNumber:D2}_{entry.EntryNumber}.md";
        }

        return Path.Combine(directory, fileName);
    }

    /// <summary>
    /// Generates markdown content with YAML frontmatter for a memory entry.
    /// </summary>
    private string GenerateMarkdownContent(MemoryEntry entry)
    {
        var sb = new StringBuilder();

        // YAML frontmatter
        sb.AppendLine("---");
        
        var frontmatter = new Dictionary<string, object>
        {
            ["entry-id"] = entry.EntryId,
            ["command"] = entry.Command,
            ["timestamp"] = entry.Timestamp.ToString("o", CultureInfo.InvariantCulture),
            ["entry-number"] = entry.EntryNumber,
            ["llm-provider"] = entry.Metadata.LlmProvider,
            ["llm-model"] = entry.Metadata.LlmModel,
            ["tokens-used"] = entry.Metadata.TokensUsed,
            ["processing-duration"] = entry.Metadata.ProcessingDuration.TotalSeconds
        };

        string yaml = _yamlSerializer.Serialize(frontmatter);
        sb.Append(yaml);
        sb.AppendLine("---");
        sb.AppendLine();

        // User input section
        sb.AppendLine("## User Input");
        sb.AppendLine();
        sb.AppendLine(entry.UserInput);
        sb.AppendLine();

        // LLM response section
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(entry.LlmResponse);

        return sb.ToString();
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
                _logger.LogWarning("No YAML frontmatter found in file: {FilePath}", filePath);
                return null;
            }

            string yamlContent = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length)
                .Replace("---", string.Empty, StringComparison.Ordinal)
                .Trim();

            var frontmatter = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // Extract content sections
            string userInput = ExtractSection(content, "## User Input", "## Summary");
            string llmResponse = ExtractSection(content, "## Summary", null);

            // Build MemoryEntry
            var metadata = new MemoryEntryMetadata
            {
                LlmProvider = frontmatter.GetValueOrDefault("llm-provider")?.ToString() ?? "Unknown",
                LlmModel = frontmatter.GetValueOrDefault("llm-model")?.ToString() ?? "Unknown",
                TokensUsed = Convert.ToInt32(frontmatter.GetValueOrDefault("tokens-used") ?? 0, CultureInfo.InvariantCulture),
                ProcessingDuration = TimeSpan.FromSeconds(Convert.ToDouble(frontmatter.GetValueOrDefault("processing-duration") ?? 0, CultureInfo.InvariantCulture))
            };

            string command = frontmatter.GetValueOrDefault("command")?.ToString() ?? CommandNames.Today;

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
                    Metadata = metadata,
                    Summary = new DailySummary
                    {
                        KeyEvents = [],
                        Themes = [],
                        TodoItems = [],
                        ImportantPeople = [],
                        NotableTasks = []
                    }
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
                    Metadata = metadata,
                    Summary = new WeeklySummary
                    {
                        TopAccomplishments = [],
                        TopChallenges = [],
                        DateRange = new DateRange
                        {
                            StartDate = DateTimeOffset.UtcNow.AddDays(-7),
                            EndDate = DateTimeOffset.UtcNow
                        }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse markdown file: {FilePath}", filePath);
            return null;
        }
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
    /// Gets the ISO 8601 week number for a given date.
    /// </summary>
    private static int GetIso8601WeekNumber(DateTime date)
    {
        DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }

        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }
}
