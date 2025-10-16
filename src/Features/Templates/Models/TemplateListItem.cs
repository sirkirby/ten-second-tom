using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Templates.Models;

/// <summary>
/// Lightweight model for template selection UI.
/// Represents a template in the selection list with display-friendly information.
/// </summary>
public sealed record TemplateListItem
{
    /// <summary>
    /// Gets the unique identifier for selection.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the display name in selection list.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the description shown in selection list.
    /// Empty string if no description available.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets where template came from (embedded or filesystem).
    /// </summary>
    public required TemplateSource Source { get; init; }

    /// <summary>
    /// Gets whether this is a default template.
    /// </summary>
    public required bool IsDefault { get; init; }

    /// <summary>
    /// Gets the template type (daily or weekly).
    /// </summary>
    public required TemplateType TemplateType { get; init; }
}
