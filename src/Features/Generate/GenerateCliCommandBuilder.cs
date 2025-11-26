using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// Builds the /generate command with subcommands:
/// <list type="bullet">
///   <item><c>generate note</c> - Select a note interactively, then select template and generate</item>
///   <item><c>generate recording</c> - Select a recording interactively, then select template and generate</item>
/// </list>
/// </summary>
public sealed class GenerateCliCommandBuilder : ICommandBuilder
{
    public int Priority => 45;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var generateCommand = new Command("generate", "Generate output from notes or recordings using prompt templates");

        // Add subcommands
        generateCommand.Subcommands.Add(BuildNoteSubcommand(serviceProvider, jsonOutputOption));
        generateCommand.Subcommands.Add(BuildRecordingSubcommand(serviceProvider, jsonOutputOption));

        return generateCommand;
    }

    private static Command BuildNoteSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var noteCommand = new Command("note", "Generate output from a note using a prompt template");

        var templateOption = new Option<string?>("--template")
        {
            Description = "Template ID to use. If not provided, interactive selection is used."
        };

        noteCommand.Options.Add(templateOption);
        noteCommand.Options.Add(jsonOutputOption);

        noteCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? templateId = parseResult.GetValue(templateOption);

            return await ExecuteGenerateAsync(
                serviceProvider,
                jsonOutput,
                "Note",
                templateId).ConfigureAwait(false);
        });

        return noteCommand;
    }

    private static Command BuildRecordingSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var recordingCommand = new Command("recording", "Generate output from a recording using a prompt template");

        var templateOption = new Option<string?>("--template")
        {
            Description = "Template ID to use. If not provided, interactive selection is used."
        };

        recordingCommand.Options.Add(templateOption);
        recordingCommand.Options.Add(jsonOutputOption);

        recordingCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? templateId = parseResult.GetValue(templateOption);

            return await ExecuteGenerateAsync(
                serviceProvider,
                jsonOutput,
                "Recording",
                templateId).ConfigureAwait(false);
        });

        return recordingCommand;
    }

    private static async Task<int> ExecuteGenerateAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        string inputType,
        string? templateId)
    {
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var templateLoader = serviceProvider.GetRequiredService<IPromptTemplateLoader>();
        var console = serviceProvider.GetRequiredService<IAnsiConsole>();

        // Show warning if using mock authentication
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            console.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            console.WriteLine();
        }

        // Authenticate first
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Generate,
            jsonOutput,
            CancellationToken.None);

        if (!authResult.IsSuccess)
        {
            return 1;
        }

        try
        {
            // Step 1: Select input based on type
            string inputFilePath;
            string inputBaseName;

            if (inputType == "Note")
            {
                var notesResult = await mediator.Send(new ListNotes.Query());
                if (!notesResult.IsSuccess)
                {
                    return HandleError(jsonOutput, $"Failed to list notes: {notesResult.Error}");
                }

                if (notesResult.Value.Count == 0)
                {
                    return HandleError(jsonOutput, "No notes found. Create notes using 'tom note' first.");
                }

                if (jsonOutput)
                {
                    // In JSON mode, use the most recent note
                    var latest = notesResult.Value[0];
                    inputFilePath = latest.FilePath;
                    inputBaseName = latest.FileName;
                }
                else
                {
                    // Interactive note selection
                    var selectedNote = console.Prompt(
                        new SelectionPrompt<NoteListItem>()
                            .Title("[cyan]Select a note:[/]")
                            .PageSize(15)
                            .AddChoices(notesResult.Value)
                            .UseConverter(n => $"{n.FileName} ({n.LastModified:g})"));

                    inputFilePath = selectedNote.FilePath;
                    inputBaseName = selectedNote.FileName;
                }
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
                    return HandleError(jsonOutput, "No recordings found. Create recordings using 'tom record' first.");
                }

                if (jsonOutput)
                {
                    // In JSON mode, use the most recent recording
                    var latest = recordingsResult.Value[0];
                    inputFilePath = latest.TranscriptFilePath;
                    inputBaseName = latest.RecordingBaseName;
                }
                else
                {
                    // Interactive recording selection
                    var selectedRecording = console.Prompt(
                        new SelectionPrompt<RecordingListItem>()
                            .Title("[cyan]Select a recording:[/]")
                            .PageSize(15)
                            .AddChoices(recordingsResult.Value)
                            .UseConverter(r => $"{r.FormattedDate} ({r.WordCount} words)"));

                    inputFilePath = selectedRecording.TranscriptFilePath;
                    inputBaseName = selectedRecording.RecordingBaseName;
                }
            }

            // Step 2: Select template
            var allTemplatesResult = await templateLoader.LoadAllTemplatesAsync();
            if (!allTemplatesResult.IsSuccess)
            {
                return HandleError(jsonOutput, $"Failed to load templates: {allTemplatesResult.Error}");
            }

            var allTemplates = allTemplatesResult.Value;
            if (allTemplates.Count == 0)
            {
                return HandleError(jsonOutput, "No templates found.");
            }

            PromptTemplate selectedTemplate;

            if (!string.IsNullOrWhiteSpace(templateId))
            {
                selectedTemplate = allTemplates.FirstOrDefault(t =>
                    t.TemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase))!;

                if (selectedTemplate == null)
                {
                    var availableTemplates = string.Join(", ", allTemplates.Select(t => $"'{t.TemplateId}'"));
                    return HandleError(jsonOutput, $"Template '{templateId}' not found. Available: {availableTemplates}");
                }

                if (!jsonOutput)
                {
                    console.MarkupLine($"[dim]Using template: {(selectedTemplate.Metadata?.Title ?? "Untitled").EscapeMarkup()}[/]");
                }
            }
            else
            {
                if (jsonOutput)
                {
                    // Default to first template in JSON mode
                    selectedTemplate = allTemplates[0];
                }
                else
                {
                    // Interactive template selection
                    selectedTemplate = console.Prompt(
                        new SelectionPrompt<PromptTemplate>()
                            .Title("[cyan]Select a template:[/]")
                            .PageSize(10)
                            .AddChoices(allTemplates)
                            .UseConverter(t => $"{t.Metadata?.Title ?? "Untitled"} ({t.TemplateId})"));
                }
            }

            // Step 3: Get max input tokens
            var llmOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<LlmOptions>>().Value;
            int maxInputTokens = llmOptions.GetMaxInputTokens()
                ?? (llmOptions.Provider == LlmProvider.Anthropic
                    ? LlmConstants.DefaultMaxInputTokensAnthropic
                    : LlmConstants.DefaultMaxInputTokensOpenAI);

            // Step 4: Generate output
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

            Models.GeneratedOutput? output;

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
                            console.MarkupLine($"[red]✗[/] Generation failed: {generateResult.Error?.EscapeMarkup()}");
                            return null;
                        }
                        return generateResult.Value;
                    });

                if (output == null) return 1;

                console.MarkupLine("[green]✓[/] Generation complete!");
                console.WriteLine();
            }

            // Step 5: Display output
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

            return 0;
        }
        catch (Exception ex)
        {
            return HandleError(jsonOutput, $"Unexpected error: {ex.Message}");
        }
    }

    private static void DisplayGeneratedOutput(Models.GeneratedOutput output, IAnsiConsole console)
    {
        const int MaxDisplayContentLength = 1000;

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
