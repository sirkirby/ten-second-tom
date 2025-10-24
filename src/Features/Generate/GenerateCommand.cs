using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Generate.Commands;
using TenSecondTom.Features.Generate.Queries;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// CLI handler for the generate command.
/// Provides interactive recording and template selection with LLM-based output generation.
/// </summary>
public static class GenerateCommand
{
    private const int MaxDisplayContentLength = 1000; // Truncate displayed output for readability

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Executes the generate command with interactive or non-interactive recording and template selection.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution.</param>
    /// <param name="jsonOutput">Whether to output JSON format.</param>
    /// <param name="templateName">Optional template name for non-interactive execution. When provided, automatically selects most recent recording.</param>
    /// <returns>Exit code (0 for success, non-zero for errors).</returns>
    public static async Task<int> ExecuteAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        string? templateName = null)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Handlers.GenerateOutputCommandHandler>>();
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first (generate command requires LLM API access)
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Generate,
            jsonOutput,
            CancellationToken.None);

        if (!authResult.IsSuccess)
        {
            return 1; // Authentication failed
        }

        try
        {
            // T045: Validate templateName if provided
            if (templateName is not null && string.IsNullOrWhiteSpace(templateName))
            {
                return HandleError(jsonOutput, "Template name cannot be empty or whitespace.");
            }

            // Step 1: List available recordings
            var listRecordingsHandler = serviceProvider.GetRequiredService<Handlers.ListRecordingsQueryHandler>();
            var recordingsQuery = new ListRecordingsQuery { CancellationToken = CancellationToken.None };
            var recordingsResult = await listRecordingsHandler.Handle(recordingsQuery, CancellationToken.None);

            if (!recordingsResult.IsSuccess)
            {
                return HandleError(jsonOutput, $"Failed to list recordings: {recordingsResult.Error}");
            }

            if (recordingsResult.Value.Count == 0)
            {
                return HandleError(jsonOutput, "No recordings found. Use 'record' command to create a recording first.");
            }

            // Step 2: Recording selection (interactive or automatic)
            var recordings = recordingsResult.Value;

            Models.RecordingListItem selectedRecording;

            // T043: If --template provided, automatically select most recent recording (non-interactive)
            if (templateName is not null)
            {
                selectedRecording = recordings[0]; // Most recent (recordings are sorted newest first)

                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine($"[dim]Auto-selected most recent recording: {selectedRecording.RecordingBaseName.EscapeMarkup()}[/]");
                }
            }
            else if (jsonOutput)
            {
                // Non-interactive mode without --template: select most recent recording
                selectedRecording = recordings[0];
            }
            else
            {
                // Interactive: show selection prompt
                var selectionPrompt = new SelectionPrompt<Models.RecordingListItem>()
                    .Title("[cyan]Select a recording to process:[/]")
                    .PageSize(10)
                    .AddChoices(recordings)
                    .UseConverter(r => r.DisplayLabel);

                selectedRecording = AnsiConsole.Prompt(selectionPrompt);
                AnsiConsole.MarkupLine($"[dim]Selected: {selectedRecording.RecordingBaseName.EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();
            }

            // Step 3: List available templates
            var listTemplatesHandler = serviceProvider.GetRequiredService<Templates.Handlers.ListTemplatesQueryHandler>();
            var templatesQuery = new ListTemplatesQuery(FilterByType: null, IncludeInvalid: false);
            var templatesResult = await listTemplatesHandler.Handle(templatesQuery, CancellationToken.None);

            if (!templatesResult.IsSuccess)
            {
                return HandleError(jsonOutput, $"Failed to list templates: {templatesResult.Error}");
            }

            if (templatesResult.Value.Templates.Count == 0)
            {
                return HandleError(jsonOutput, "No prompt templates found. Please configure templates using 'setup' command.");
            }

            // Step 4: Template selection (interactive, automatic, or by name)
            var templates = templatesResult.Value.Templates;

            Templates.Models.TemplateListItem selectedTemplate;

            // T042: If --template provided, resolve template name case-insensitively
            if (templateName is not null)
            {
                // Match against TemplateId or Title (case-insensitive)
                selectedTemplate = templates.FirstOrDefault(t =>
                    string.Equals(t.TemplateId, templateName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.Title, templateName, StringComparison.OrdinalIgnoreCase))!;

                // T044: Error handling for template not found with list of available templates
                if (selectedTemplate is null)
                {
                    var availableTemplates = string.Join(", ", templates.Select(t => $"'{t.TemplateId}'"));
                    return HandleError(jsonOutput,
                        $"Template '{templateName}' not found. Available templates: {availableTemplates}");
                }

                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine($"[dim]Using template: {selectedTemplate.Title.EscapeMarkup()} ({selectedTemplate.TemplateId.EscapeMarkup()})[/]");
                    AnsiConsole.WriteLine();
                }
            }
            else if (jsonOutput)
            {
                // Non-interactive mode without --template: select first template (typically default template)
                selectedTemplate = templates[0];
            }
            else
            {
                // Interactive: show selection prompt
                var templatePrompt = new SelectionPrompt<Templates.Models.TemplateListItem>()
                    .Title("[cyan]Select a template:[/]")
                    .PageSize(10)
                    .AddChoices(templates)
                    .UseConverter(t => $"{t.Title} - {t.Description}");

                selectedTemplate = AnsiConsole.Prompt(templatePrompt);
                AnsiConsole.MarkupLine($"[dim]Selected: {selectedTemplate.Title.EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();
            }

            // Step 5: Load configuration for max input tokens
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var maxInputTokensConfig = configuration[ConfigurationKeys.LlmMaxInputTokens];
            int maxInputTokens = 50000; // Default fallback

            if (!string.IsNullOrWhiteSpace(maxInputTokensConfig) &&
                int.TryParse(maxInputTokensConfig, out int parsedTokens) &&
                parsedTokens > 0)
            {
                maxInputTokens = parsedTokens;
            }

            // Step 6: Generate output with LLM
            var generateHandler = serviceProvider.GetRequiredService<Handlers.GenerateOutputCommandHandler>();
            var generateCommand = new GenerateOutputCommand
            {
                TranscriptFilePath = selectedRecording.TranscriptFilePath,
                RecordingBaseName = selectedRecording.RecordingBaseName,
                TemplateId = selectedTemplate.TemplateId,
                MaxInputTokens = maxInputTokens,
                CancellationToken = CancellationToken.None
            };

            Models.GeneratedOutput? output = null;

            if (jsonOutput)
            {
                // Non-interactive: execute without status display
                var generateResult = await generateHandler.Handle(generateCommand, CancellationToken.None);

                if (!generateResult.IsSuccess)
                {
                    return HandleError(jsonOutput, $"Generation failed: {generateResult.Error}");
                }

                output = generateResult.Value;
            }
            else
            {
                // Interactive: show loading indicator
                output = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("[yellow]Generating output with LLM...[/]", async ctx =>
                    {
                        var generateResult = await generateHandler.Handle(generateCommand, CancellationToken.None);

                        if (!generateResult.IsSuccess)
                        {
                            AnsiConsole.MarkupLine($"[red]✗[/] Generation failed: {generateResult.Error.EscapeMarkup()}");
                            return null;
                        }

                        return generateResult.Value;
                    });

                if (output is null)
                {
                    return 1; // Error already displayed
                }

                AnsiConsole.MarkupLine("[green]✓[/] Generation complete!");
                AnsiConsole.WriteLine();
            }

            // Step 7: Display truncation warning if applicable
            if (output.WasTruncated && !jsonOutput)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠  Transcript truncated from {output.OriginalWordCount} to fit within token limit[/]");
                AnsiConsole.WriteLine();
            }

            // Step 8: Display generated output and save location
            if (jsonOutput)
            {
                // JSON output format
                var jsonResult = new
                {
                    success = true,
                    recording_base_name = output.RecordingBaseName,
                    template_id = output.TemplateId,
                    template_title = output.TemplateTitle,
                    output_file_path = output.OutputFilePath,
                    generated_at = output.GeneratedAt,
                    provider = output.ProviderName,
                    model = output.ModelName,
                    input_tokens = output.InputTokens,
                    output_tokens = output.OutputTokens,
                    total_tokens = output.TotalTokens,
                    was_truncated = output.WasTruncated,
                    original_word_count = output.OriginalWordCount,
                    content = output.Content
                };

                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonResult, JsonOptions));
            }
            else
            {
                // Human-readable output format
                DisplayGeneratedOutput(output);
            }

            logger.LogInformation(
                "Generated output for recording {RecordingBaseName} using template {TemplateId} ({InputTokens} input tokens, {OutputTokens} output tokens)",
                output.RecordingBaseName,
                output.TemplateId,
                output.InputTokens,
                output.OutputTokens);

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during generate command execution");
            return HandleError(jsonOutput, $"Unexpected error: {ex.Message}");
        }
    }

    private static void DisplayGeneratedOutput(Models.GeneratedOutput output)
    {
        // Display metadata
        var metadataTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Property[/]")
            .AddColumn("[bold]Value[/]");

        metadataTable.AddRow("Recording", output.RecordingBaseName.EscapeMarkup());
        metadataTable.AddRow("Template", $"{output.TemplateTitle.EscapeMarkup()} ({output.TemplateId.EscapeMarkup()})");
        metadataTable.AddRow("Provider", $"{output.ProviderName.EscapeMarkup()} ({output.ModelName.EscapeMarkup()})");
        metadataTable.AddRow("Tokens", $"{output.InputTokens:N0} input + {output.OutputTokens:N0} output = {output.TotalTokens:N0} total");

        if (!string.IsNullOrWhiteSpace(output.OutputFilePath))
        {
            metadataTable.AddRow("Saved to", output.OutputFilePath.EscapeMarkup());
        }

        AnsiConsole.Write(metadataTable);
        AnsiConsole.WriteLine();

        // Display content (truncated for terminal display)
        AnsiConsole.MarkupLine("[bold cyan]Generated Output:[/]");
        AnsiConsole.WriteLine();

        var displayContent = output.Content.Length > MaxDisplayContentLength
            ? output.Content[..MaxDisplayContentLength] + $"\n\n[... truncated {output.Content.Length - MaxDisplayContentLength} characters for display ...]"
            : output.Content;

        var panel = new Panel(displayContent.EscapeMarkup())
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (output.Content.Length > MaxDisplayContentLength)
        {
            AnsiConsole.MarkupLine($"[dim]Full content saved to: {output.OutputFilePath?.EscapeMarkup() ?? "file"}[/]");
        }
    }

    private static int HandleError(bool jsonOutput, string errorMessage)
    {
        if (jsonOutput)
        {
            var errorResult = new
            {
                success = false,
                error = errorMessage
            };

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorResult, JsonOptions));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {errorMessage.EscapeMarkup()}");
        }

        return 1;
    }
}
