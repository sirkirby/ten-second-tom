using FluentValidation;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Shared.TextEditing.Validation;

/// <summary>
/// Validator for EditorConfiguration to ensure sane limits and prevent misconfigurations.
/// </summary>
public sealed class EditorConfigurationValidator : AbstractValidator<EditorConfiguration>
{
    /// <summary>
    /// Maximum allowed content length (1MB = 1,000,000 characters)
    /// </summary>
    private const int MaxAllowedContentLength = 1_000_000;

    /// <summary>
    /// Maximum allowed line count (100,000 lines)
    /// </summary>
    private const int MaxAllowedLineCount = 100_000;

    /// <summary>
    /// Minimum required content length (must allow at least 100 characters)
    /// </summary>
    private const int MinContentLength = 100;

    /// <summary>
    /// Minimum required line count (must allow at least 10 lines)
    /// </summary>
    private const int MinLineCount = 10;

    public EditorConfigurationValidator()
    {
        // Validate MaxContentLength
        RuleFor(x => x.MaxContentLength)
            .GreaterThan(0)
            .WithMessage("MaxContentLength must be greater than 0")
            .GreaterThanOrEqualTo(MinContentLength)
            .WithMessage($"MaxContentLength must be at least {MinContentLength} characters to be useful")
            .LessThanOrEqualTo(MaxAllowedContentLength)
            .WithMessage($"MaxContentLength cannot exceed {MaxAllowedContentLength} characters (1MB limit)");

        // Validate MaxLineCount
        RuleFor(x => x.MaxLineCount)
            .GreaterThan(0)
            .WithMessage("MaxLineCount must be greater than 0")
            .GreaterThanOrEqualTo(MinLineCount)
            .WithMessage($"MaxLineCount must be at least {MinLineCount} lines to be useful")
            .LessThanOrEqualTo(MaxAllowedLineCount)
            .WithMessage($"MaxLineCount cannot exceed {MaxAllowedLineCount} lines");

        // Validate PreviewLineLimit (0 = show all, so 0 is valid)
        RuleFor(x => x.PreviewLineLimit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("PreviewLineLimit must be 0 (show all) or positive")
            .LessThanOrEqualTo(1000)
            .WithMessage("PreviewLineLimit seems excessively high (max 1000 lines)");

        // Validate Title length if provided
        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title != null)
            .WithMessage("Title cannot exceed 200 characters");
    }
}

