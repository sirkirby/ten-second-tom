using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using System.Linq;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;

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
    /// <param name="templateId">Optional template ID for non-interactive execution.</param>
    /// <param name="noteName">Optional note filename (without extension).</param>
    /// <param name="recordingName">Optional recording filename (without extension).</param>
    /// <param name="listTemplates">Whether to list available templates and exit.</param>
    /// <returns>Exit code (0 for success, non-zero for errors).</returns>
    public static async Task<int> ExecuteAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        string? templateId = null,
        string? noteName = null,
        string? recordingName = null,
        bool listTemplates = false)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<GenerateOutput.Handler>>(); // Using Handler logger as GenerateCommand is static
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var templateLoader = serviceProvider.GetRequiredService<IPromptTemplateLoader>();
        var console = serviceProvider.GetRequiredService<IAnsiConsole>();

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            console.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            console.WriteLine();
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
            // Step 1: List Templates if requested
            if (listTemplates)
            {
                var listTemplatesResult = await templateLoader.LoadAllTemplatesAsync();
                if (!listTemplatesResult.IsSuccess)
                {
                    return HandleError(jsonOutput, $"Failed to load templates: {listTemplatesResult.Error}");
                }

                var templates = listTemplatesResult.Value;

                if (jsonOutput)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(templates, JsonOptions);
                    console.WriteLine(json);
                }
                else
                {
                    var table = new Table().AddColumn("ID").AddColumn("Title").AddColumn("Description");
                    foreach (var t in templates)
                    {
                        table.AddRow(
                            t.TemplateId.EscapeMarkup(),
                            (t.Metadata?.Title ?? "Untitled").EscapeMarkup(),
                            (t.Metadata?.Description ?? "").EscapeMarkup());
                    }
                    console.Write(table);
                }
                return 0;
            }

            // Step 2: Determine Input (Note or Recording)
            string inputFilePath;
            string inputType; // "Note" or "Recording"
            string inputBaseName;

            if (!string.IsNullOrWhiteSpace(noteName))
            {
                // Direct Note selection
                inputType = "Note";
                inputBaseName = noteName;
                var notesResult = await mediator.Send(new ListNotes.Query());
                if (!notesResult.IsSuccess)
                {
                    return HandleError(jsonOutput, $"Failed to list notes: {notesResult.Error}");
                }
                var note = notesResult.Value.FirstOrDefault(n => n.FileName.Equals(noteName, StringComparison.OrdinalIgnoreCase));
                if (note == null)
                {
                    return HandleError(jsonOutput, $"Note '{noteName}' not found.");
                }
                inputFilePath = note.FilePath;
            }
            else if (!string.IsNullOrWhiteSpace(recordingName))
            {
                // Direct Recording selection
                inputType = "Recording";
                inputBaseName = recordingName;
                var recordingsResult = await mediator.Send(new ListRecordings.Query());
                if (!recordingsResult.IsSuccess)
                {
                    return HandleError(jsonOutput, $"Failed to list recordings: {recordingsResult.Error}");
                }
                var recording = recordingsResult.Value.FirstOrDefault(r => r.RecordingBaseName.Equals(recordingName, StringComparison.OrdinalIgnoreCase));
                if (recording == null)
                {
                    return HandleError(jsonOutput, $"Recording '{recordingName}' not found.");
                }
                inputFilePath = recording.TranscriptFilePath;
            }
            else
            {
                // No direct input specified
                if (!string.IsNullOrWhiteSpace(templateId))
                {
                    // Non-interactive default: Latest Recording
                    inputType = "Recording";
                    var recordingsResult = await mediator.Send(new ListRecordings.Query());
                    if (!recordingsResult.IsSuccess)
                    {
                        return HandleError(jsonOutput, $"Failed to list recordings: {recordingsResult.Error}");
                    }
                    if (recordingsResult.Value.Count == 0)
                    {
                        return HandleError(jsonOutput, "No recordings found.");
                    }
                    var latest = recordingsResult.Value[0];
                    inputFilePath = latest.TranscriptFilePath;
                    inputBaseName = latest.RecordingBaseName;

                    if (!jsonOutput)
                    {
                        console.MarkupLine($"[dim]Auto-selected most recent recording: {inputBaseName.EscapeMarkup()}[/]");
                    }
                }
                else
                {
                    // Interactive Mode
                    if (jsonOutput)
                    {
                        // In JSON mode, we can't prompt, so default to latest recording
                        var recordingsResult = await mediator.Send(new ListRecordings.Query());
                        if (!recordingsResult.IsSuccess || recordingsResult.Value.Count == 0)
                        {
                            return HandleError(jsonOutput, "No recordings found.");
                        }
                        var latest = recordingsResult.Value[0];
                        inputFilePath = latest.TranscriptFilePath;
                        inputBaseName = latest.RecordingBaseName;
                        inputType = "Recording";
                    }
                    else
                    {
                        var selection = console.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Select input type:")
                                .AddChoices("Recording", "Note"));

                        inputType = selection;

                        if (selection == "Note")
                        {
                            var notesResult = await mediator.Send(new ListNotes.Query());
                            if (!notesResult.IsSuccess)
                            {
                                return HandleError(jsonOutput, $"Failed to list notes: {notesResult.Error}");
                            }
                            if (notesResult.Value.Count == 0)
                            {
                                return HandleError(jsonOutput, "No notes found.");
                            }

                            var selectedNote = console.Prompt(
                                new SelectionPrompt<NoteListItem>()
                                    .Title("Select a note:")
                                    .PageSize(10)
                                    .AddChoices(notesResult.Value)
                                    .UseConverter(n => $"{n.FileName} ({n.LastModified:g})"));

                            inputFilePath = selectedNote.FilePath;
                            inputBaseName = selectedNote.FileName;
                        }
                        else // Recording
                        {
                            var recordingsResult = await mediator.Send(new ListRecordings.Query());
                            if (!recordingsResult.IsSuccess)
                            {
                                return HandleError(jsonOutput, $"Failed to list recordings: {recordingsResult.Error}");
                            }
                            if (recordingsResult.Value.Count == 0)
                            {
                                return HandleError(jsonOutput, "No recordings found.");
                            }

                            var selectedRecording = console.Prompt(
                                new SelectionPrompt<RecordingListItem>()
                                    .Title("Select a recording:")
                                    .PageSize(10)
                                    .AddChoices(recordingsResult.Value)
                                    .UseConverter(r => $"{r.FormattedDate} ({r.WordCount} words)"));

                            inputFilePath = selectedRecording.TranscriptFilePath;
                            inputBaseName = selectedRecording.RecordingBaseName;
                        }
                    }
                }
            }

            // Step 3: Select Template
            PromptTemplate selectedTemplate;
            var allTemplatesResult = await templateLoader.LoadAllTemplatesAsync();

            if (!allTemplatesResult.IsSuccess)
            {
                return HandleError(jsonOutput, $"Failed to load templates: {allTemplatesResult.Error}");
            }

            var allTemplates = allTemplatesResult.Value;

            if (!string.IsNullOrWhiteSpace(templateId))
            {
                selectedTemplate = allTemplates.FirstOrDefault(t => t.TemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase))!;
                if (selectedTemplate == null)
                {
                    var availableTemplates = string.Join(", ", allTemplates.Select(t => $"'{t.TemplateId}'"));
                    return HandleError(jsonOutput, $"Template '{templateId}' not found. Available templates: {availableTemplates}");
                }

                if (!jsonOutput)
                {
                    console.MarkupLine($"[dim]Using template: {(selectedTemplate.Metadata?.Title ?? "Untitled").EscapeMarkup()} ({selectedTemplate.TemplateId.EscapeMarkup()})[/]");
                }
            }
            else
            {
                if (jsonOutput)
                {
                    // Default to first template if not specified in JSON mode (or error?)
                    // Let's error to be safe, or default. CLI usually defaults.
                    if (allTemplates.Count == 0) return HandleError(jsonOutput, "No templates found.");
                    selectedTemplate = allTemplates[0];
                }
                else
                {
                    // Interactive Template Selection
                    selectedTemplate = console.Prompt(
                        new SelectionPrompt<PromptTemplate>()
                            .Title("Select a template:")
                            .PageSize(10)
                            .AddChoices(allTemplates)
                            .UseConverter(t => $"{t.Metadata?.Title ?? "Untitled"} ({t.TemplateId})"));
                }
            }

            // Step 4: Get max input tokens
            var llmOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<LlmOptions>>().Value;
            int maxInputTokens = llmOptions.GetMaxInputTokens()
                ?? (llmOptions.Provider == LlmProvider.Anthropic
                    ? LlmConstants.DefaultMaxInputTokensAnthropic
                    : LlmConstants.DefaultMaxInputTokensOpenAI);

            // Step 5: Generate Output
            var generateHandler = serviceProvider.GetRequiredService<GenerateOutput.Handler>();
            var generateCommand = new GenerateOutput.Command
            {
                TranscriptFilePath = inputFilePath,
                InputName = inputBaseName,
                InputType = inputType,
                TemplateId = selectedTemplate.TemplateId,
                MaxInputTokens = maxInputTokens,
                CancellationToken = CancellationToken.None
            };

            Models.GeneratedOutput? output = null;

            if (jsonOutput)
            {
                var generateResult = await generateHandler.Handle(generateCommand, CancellationToken.None);
                if (!generateResult.IsSuccess)
                {
                    return HandleError(jsonOutput, $"Generation failed: {generateResult.Error}");
                }
                output = generateResult.Value!;
            }
            else
            {
                output = await console.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Generating {selectedTemplate.Metadata?.Title ?? "Untitled"}...", async ctx =>
                    {
                        var generateResult = await generateHandler.Handle(generateCommand, CancellationToken.None);
                        if (!generateResult.IsSuccess)
                        {
                            console.MarkupLine($"[red]✗[/] Generation failed: {generateResult.Error.EscapeMarkup()}");
                            return null;
                        }
                        return generateResult.Value;
                    });

                if (output == null) return 1;

                console.MarkupLine("[green]✓[/] Generation complete!");
                console.WriteLine();
            }

            // Step 6: Display Output
            if (jsonOutput)
            {
                var jsonResult = new
                {
                    success = true,
                    input_name = output.InputName,
                    input_type = output.InputType,
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
                DisplayGeneratedOutput(output, console);
            }

            logger.LogInformation(
                "Generated output for {InputBaseName} using template {TemplateId} ({InputTokens} input tokens, {OutputTokens} output tokens)",
                inputBaseName,
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

    private static void DisplayGeneratedOutput(Models.GeneratedOutput output, IAnsiConsole console)
    {
        var metadataTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Property[/]")
            .AddColumn("[bold]Value[/]");

        metadataTable.AddRow("Input", output.InputName.EscapeMarkup());
        metadataTable.AddRow("Type", output.InputType.EscapeMarkup());
        metadataTable.AddRow("Template", $"{output.TemplateTitle.EscapeMarkup()} ({output.TemplateId.EscapeMarkup()})");
        metadataTable.AddRow("Provider", $"{output.ProviderName.EscapeMarkup()} ({output.ModelName.EscapeMarkup()})");
        metadataTable.AddRow("Tokens", $"{output.InputTokens:N0} input + {output.OutputTokens:N0} output = {output.TotalTokens:N0} total");

        if (!string.IsNullOrWhiteSpace(output.OutputFilePath))
        {
            metadataTable.AddRow("Saved to", output.OutputFilePath.EscapeMarkup());
        }

        console.Write(metadataTable);
        console.WriteLine();

        console.MarkupLine("[bold cyan]Generated Output:[/]");
        console.WriteLine();

        var displayContent = output.Content.Length > MaxDisplayContentLength
            ? output.Content[..MaxDisplayContentLength] + $"\n\n[... truncated {output.Content.Length - MaxDisplayContentLength} characters for display ...]"
            : output.Content;

        var panel = new Panel(displayContent.EscapeMarkup())
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);

        console.Write(panel);
        console.WriteLine();

        if (output.Content.Length > MaxDisplayContentLength)
        {
            console.MarkupLine($"[dim]Full content saved to: {output.OutputFilePath?.EscapeMarkup() ?? "file"}[/]");
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
