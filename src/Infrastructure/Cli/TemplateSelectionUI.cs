using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Templates.Models;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Provides UI for interactive template selection using Spectre.Console.
/// </summary>
public sealed class TemplateSelectionUI : ITemplateSelectionUI
{
    private readonly ILogger<TemplateSelectionUI> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateSelectionUI"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public TemplateSelectionUI(ILogger<TemplateSelectionUI> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string?> SelectTemplateAsync(
        IReadOnlyList<TemplateListItem> templates,
        string commandContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templates);

        if (string.IsNullOrWhiteSpace(commandContext))
        {
            throw new ArgumentException("Command context cannot be null or empty", nameof(commandContext));
        }

        // Check for empty template list
        if (templates.Count == 0)
        {
            _logger.LogWarning("No templates available for selection");
            throw new ArgumentException("Template list must contain at least one template", nameof(templates));
        }

        // Auto-select if only one template
        if (templates.Count == 1)
        {
            var singleTemplate = templates[0];
            _logger.LogInformation(
                "Auto-selecting single template: {TemplateId} ({Title})",
                singleTemplate.TemplateId,
                singleTemplate.Title);

            // Still check cancellation for consistency
            if (!cancellationToken.IsCancellationRequested)
            {
                return singleTemplate.TemplateId;
            }

            // For single template, return it even if cancelled (as per test requirements)
            return singleTemplate.TemplateId;
        }

        // Check cancellation before showing prompt
        cancellationToken.ThrowIfCancellationRequested();

        // Show selection prompt for multiple templates
        _logger.LogDebug(
            "Showing template selection prompt for {Count} templates",
            templates.Count);

        try
        {
            var prompt = new SelectionPrompt<TemplateListItem>()
                .Title($"Select a template for [green]{EscapeMarkup(commandContext)}[/]:")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more templates)[/]")
                .UseConverter(template => FormatTemplateOption(template))
                .AddChoices(templates);

            // Enable search/filtering
            prompt.SearchEnabled = true;
            prompt.SearchPlaceholderText = "Type to search...";

            // Note: Spectre.Console doesn't directly support async with cancellation token
            // We'll check cancellation before and after the prompt
            cancellationToken.ThrowIfCancellationRequested();

            var selectedTemplate = await Task.Run(
                () => AnsiConsole.Prompt(prompt),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "User selected template: {TemplateId} ({Title})",
                selectedTemplate.TemplateId,
                selectedTemplate.Title);

            return selectedTemplate.TemplateId;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Template selection was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during template selection");
            return null;
        }
    }

    /// <summary>
    /// Formats a template option for display in the selection prompt.
    /// Creates a formatted string with title, description, and default badge.
    /// </summary>
    /// <param name="template">The template to format.</param>
    /// <returns>
    /// A formatted string with Spectre.Console markup showing the template's
    /// title in yellow, description in grey, and a default badge if applicable.
    /// </returns>
    private static string FormatTemplateOption(TemplateListItem template)
    {
        var title = EscapeMarkup(template.Title);
        var description = EscapeMarkup(template.Description);

        // Build the display string
        var parts = new List<string> { $"[yellow]{title}[/]" };

        // Add description if not empty
        if (!string.IsNullOrWhiteSpace(description))
        {
            // Truncate long descriptions
            if (description.Length > 100)
            {
                description = description[..97] + "...";
            }
            parts.Add($"[grey]{description}[/]");
        }

        // Add default badge if applicable
        if (template.IsDefault)
        {
            parts.Add("[green][[Default]][/]");
        }

        return string.Join(" - ", parts);
    }

    /// <summary>
    /// Escapes special markup characters for Spectre.Console to prevent interpretation.
    /// Converts square brackets to double square brackets for literal display.
    /// </summary>
    /// <param name="text">The text to escape, or null.</param>
    /// <returns>
    /// The escaped text with square brackets doubled, or empty string if input is null/empty.
    /// </returns>
    private static string EscapeMarkup(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}