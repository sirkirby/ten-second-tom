namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Constants for template system identifiers and configuration values.
/// Centralizes template-related constants to ensure consistency across the application.
/// </summary>
public static class TemplateConstants
{
    /// <summary>
    /// Identifier for the default daily summary template.
    /// </summary>
    [Obsolete("Use template ID from front matter instead")]
    public const string DailySummaryTemplateId = "daily-summary";

    /// <summary>
    /// Identifier for the default weekly review template.
    /// </summary>
    [Obsolete("Use template ID from front matter instead")]
    public const string WeeklyReviewTemplateId = "weekly-review";

    /// <summary>
    /// Template identifier for bundled business meeting template.
    /// This is the template FILENAME ("business-meeting" from "business-meeting.md"),
    /// not the template TYPE (TemplateType.BusinessMeeting enum value).
    /// Template selection and output filenames use the filename, not the type.
    /// </summary>
    [Obsolete("Use template ID from front matter instead")]
    public const string BusinessMeetingTemplateId = "business-meeting";

    /// <summary>
    /// Identifier for the journal and reflection template.
    /// Helps users unpack thoughts, identify emotions, and gain insights.
    /// </summary>
    public const string JournalTemplateId = "journal";

    /// <summary>
    /// Identifier for the organize and plan template.
    /// Helps users extract action items, organize reminders, and identify priorities.
    /// </summary>
    public const string OrganizeTemplateId = "organize";

    /// <summary>
    /// Maximum file size for template files (1MB).
    /// </summary>
    public const int MaxFileSizeBytes = 1_048_576;

    /// <summary>
    /// Maximum content length for template content (1MB).
    /// </summary>
    public const int MaxContentLength = MaxFileSizeBytes;

    /// <summary>
    /// Warning threshold for very long lines in templates.
    /// </summary>
    public const int MaxLineLength = 500;

    /// <summary>
    /// Maximum length for template filenames.
    /// </summary>
    public const int MaxFilenameLength = 100;

    /// <summary>
    /// Maximum length for template titles in metadata.
    /// </summary>
    public const int MaxTitleLength = 200;

    /// <summary>
    /// Maximum length for template descriptions in metadata.
    /// </summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>
    /// Maximum length for template author field in metadata.
    /// </summary>
    public const int MaxAuthorLength = 100;

    /// <summary>
    /// Maximum number of tags allowed in template metadata.
    /// </summary>
    public const int MaxTagsCount = 20;

    /// <summary>
    /// Maximum length for individual tag strings.
    /// </summary>
    public const int MaxTagLength = 50;

    /// <summary>
    /// Determines if a template ID corresponds to a default (built-in) template.
    /// </summary>
    /// <param name="templateId">The template ID to check.</param>
    /// <returns>True if the template is a default template; otherwise false.</returns>
    public static bool IsDefaultTemplate(string templateId)
    {
        return templateId.Equals(DailySummaryTemplateId, StringComparison.OrdinalIgnoreCase) ||
               templateId.Equals(WeeklyReviewTemplateId, StringComparison.OrdinalIgnoreCase) ||
               templateId.Equals(BusinessMeetingTemplateId, StringComparison.OrdinalIgnoreCase) ||
               templateId.Equals(JournalTemplateId, StringComparison.OrdinalIgnoreCase) ||
               templateId.Equals(OrganizeTemplateId, StringComparison.OrdinalIgnoreCase);
    }
}
