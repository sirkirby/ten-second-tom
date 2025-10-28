namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a daily memory entry.
/// </summary>
/// <remarks>
/// A daily entry is a thin wrapper around MemoryEntry that provides type distinction.
/// The prompt template defines the output structure, and the LlmResponse field contains
/// the complete output from the LLM. No additional parsing or structure is imposed.
/// </remarks>
public record DailyEntry : MemoryEntry
{
    // No additional properties - this is a type marker for daily entries
}
