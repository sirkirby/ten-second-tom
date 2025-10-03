namespace TenSecondTom.IntegrationTests.TestHelpers;

/// <summary>
/// Helper for managing temporary test directories and memory file storage.
/// </summary>
public sealed class TemporaryTestDirectory : IDisposable
{
    private readonly string _basePath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryTestDirectory"/> class.
    /// </summary>
    public TemporaryTestDirectory()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "ten-second-tom-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Gets the base path for the temporary directory.
    /// </summary>
    public string BasePath => _basePath;

    /// <summary>
    /// Gets the memory directory path (.memory).
    /// </summary>
    public string MemoryPath => Path.Combine(_basePath, ".memory");

    /// <summary>
    /// Gets the today directory path (.memory/today).
    /// </summary>
    public string TodayPath => Path.Combine(MemoryPath, "today");

    /// <summary>
    /// Gets the weekly directory path (.memory/weekly).
    /// </summary>
    public string WeeklyPath => Path.Combine(MemoryPath, "weekly");

    /// <summary>
    /// Creates a daily entry file for testing.
    /// </summary>
    /// <param name="date">The date for the entry.</param>
    /// <param name="content">The content of the entry.</param>
    /// <returns>The path to the created file.</returns>
    public string CreateDailyEntry(DateTime date, string content)
    {
        Directory.CreateDirectory(TodayPath);
        string fileName = $"{date:MM-dd-yyyy}_1.md";
        string filePath = Path.Combine(TodayPath, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Gets all daily entry files in the today directory.
    /// </summary>
    /// <returns>An array of file paths.</returns>
    public string[] GetDailyEntries()
    {
        if (!Directory.Exists(TodayPath))
        {
            return [];
        }
        return Directory.GetFiles(TodayPath, "*.md", SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// Gets all weekly review files in the weekly directory.
    /// </summary>
    /// <returns>An array of file paths.</returns>
    public string[] GetWeeklyReviews()
    {
        if (!Directory.Exists(WeeklyPath))
        {
            return [];
        }
        return Directory.GetFiles(WeeklyPath, "*.md", SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// Cleans up the temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_basePath))
            {
                Directory.Delete(_basePath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
