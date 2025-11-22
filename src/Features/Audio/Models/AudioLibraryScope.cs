namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Identifies the origin of an audio file that can be transcribed.
/// Recording and note entries live under the storage root; external files
/// represent arbitrary paths provided at runtime.
/// </summary>
public enum AudioLibraryScope
{
    /// <summary>
    /// Audio that already lives in the recording/ directory.
    /// </summary>
    Recording,

    /// <summary>
    /// Audio captured from the note/ directory (voice notes).
    /// </summary>
    Note,

    /// <summary>
    /// Audio captured from the today/ directory (daily voice captures).
    /// </summary>
    Today,

    /// <summary>
    /// Audio supplied via an arbitrary filesystem path.
    /// </summary>
    External
}
