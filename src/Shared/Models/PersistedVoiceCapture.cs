namespace TenSecondTom.Shared.Models;

/// <summary>
/// Describes the stored audio and transcript paths produced from a persisted voice capture.
/// </summary>
public sealed record PersistedVoiceCapture(string AudioFilePath, string TranscriptFilePath);
