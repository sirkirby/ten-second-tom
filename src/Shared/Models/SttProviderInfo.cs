namespace TenSecondTom.Shared.Models;

/// <summary>
/// Information about an STT provider for display and configuration purposes.
/// Cross-feature DTO used by Audio feature queries and Setup feature wizards.
/// </summary>
/// <param name="ProviderId">The unique identifier for the provider (e.g., "whisper-cpp", "openai").</param>
/// <param name="DisplayName">The user-friendly display name for the provider.</param>
/// <param name="Description">A brief description of the provider.</param>
/// <param name="RequiresApiKey">Whether this provider requires an API key.</param>
/// <param name="IsCloud">Whether this is a cloud-based provider.</param>
public sealed record SttProviderInfo(
    string ProviderId,
    string DisplayName,
    string Description,
    bool RequiresApiKey,
    bool IsCloud);
