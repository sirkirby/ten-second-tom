namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Represents the user's current progress through the setup wizard
/// Tracks wizard state, enables back navigation, supports incremental saving
/// </summary>
public sealed record SetupProgress
{
    /// <summary>
    /// Gets the current step number (1-based)
    /// </summary>
    public required int CurrentStep { get; init; }

    /// <summary>
    /// Gets the total number of steps in the wizard
    /// </summary>
    public required int TotalSteps { get; init; }

    /// <summary>
    /// Gets the selected SSH key configuration
    /// </summary>
    public SshKeyInfo? SelectedSshKey { get; init; }

    /// <summary>
    /// Gets the selected LLM provider
    /// </summary>
    public LlmProvider? SelectedProvider { get; init; }

    /// <summary>
    /// Gets the API key for the selected provider
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the memory storage directory path
    /// </summary>
    public string? MemoryDirectory { get; init; }

    /// <summary>
    /// Gets the logging level
    /// </summary>
    public Microsoft.Extensions.Logging.LogLevel? LogLevel { get; init; }

    /// <summary>
    /// Gets the number of days to retain memories
    /// </summary>
    public int? RetentionDays { get; init; }

    /// <summary>
    /// Gets the dictionary tracking which steps have been completed
    /// Key: step number (1-based), Value: completion status
    /// </summary>
    public Dictionary<int, bool> CompletedSteps { get; init; } = new();

    /// <summary>
    /// Gets the timestamp when setup was started
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when setup was completed
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Validates the setup progress state
    /// </summary>
    public bool IsValid()
    {
        if (CurrentStep < 1 || CurrentStep > TotalSteps)
            return false;

        if (TotalSteps <= 0)
            return false;

        if (SelectedProvider.HasValue && !Enum.IsDefined(SelectedProvider.Value))
            return false;

        if (!string.IsNullOrEmpty(MemoryDirectory))
        {
            try
            {
                _ = Path.GetFullPath(MemoryDirectory);
            }
            catch
            {
                return false;
            }
        }

        foreach (var step in CompletedSteps.Keys)
        {
            if (step < 1 || step > TotalSteps)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Creates an initial setup progress for a new wizard session
    /// </summary>
    public static SetupProgress CreateInitial(int totalSteps) => new()
    {
        CurrentStep = 1,
        TotalSteps = totalSteps,
        StartedAt = DateTime.UtcNow,
        CompletedSteps = new Dictionary<int, bool>()
    };

    /// <summary>
    /// Advances to the next step
    /// </summary>
    public SetupProgress MoveNext() => this with
    {
        CurrentStep = Math.Min(CurrentStep + 1, TotalSteps),
        CompletedSteps = new Dictionary<int, bool>(CompletedSteps)
        {
            [CurrentStep] = true
        }
    };

    /// <summary>
    /// Moves back to the previous step
    /// </summary>
    public SetupProgress MovePrevious() => this with
    {
        CurrentStep = Math.Max(CurrentStep - 1, 1)
    };

    /// <summary>
    /// Marks the setup as completed
    /// </summary>
    public SetupProgress Complete() => this with
    {
        CompletedAt = DateTime.UtcNow,
        CompletedSteps = new Dictionary<int, bool>(CompletedSteps)
        {
            [CurrentStep] = true
        }
    };
}
