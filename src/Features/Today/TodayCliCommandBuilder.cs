using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Today;

/// <summary>
/// Provides the /today command via ICommandBuilder discovery.
/// </summary>
public sealed class TodayCliCommandBuilder : ICommandBuilder
{
    public int Priority => 20;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var todayCommand = new Command("today", "Capture today's reflection with 3-5 prompts");

        var notesArgument = new Argument<string?>("notes")
        {
            Description = "Notes for today. If omitted, opens interactive editor.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var noEditOption = new Option<bool>("--no-edit") { Description = "Skip interactive editor and use notes from command line argument." };
        var useDefaultTemplateOption = new Option<bool>("--use-default-template") { Description = "Automatically use default template (no prompt)." };
        var templateOption = new Option<string?>("--template") { Description = "Use specific template by name (without .md extension)." };
        var providerOption = new Option<string?>("--provider") { Description = "LLM provider to use (OpenAI or Anthropic). Defaults to configured provider." };
        var voiceOption = new Option<bool>("--voice") { Description = "Capture notes using voice recording instead of text input." };
        var sttOption = new Option<string?>("--stt") { Description = "STT engine selection: auto (default), local, or openai. Only used with --voice." };

        todayCommand.Arguments.Add(notesArgument);
        todayCommand.Options.Add(noEditOption);
        todayCommand.Options.Add(useDefaultTemplateOption);
        todayCommand.Options.Add(templateOption);
        todayCommand.Options.Add(providerOption);
        todayCommand.Options.Add(voiceOption);
        todayCommand.Options.Add(sttOption);
        todayCommand.Options.Add(jsonOutputOption);

        todayCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? provider = parseResult.GetValue(providerOption);
            string? notes = parseResult.GetValue(notesArgument);
            bool noEdit = parseResult.GetValue(noEditOption);
            bool useDefaultTemplate = parseResult.GetValue(useDefaultTemplateOption);
            string? templateName = parseResult.GetValue(templateOption);
            bool useVoice = parseResult.GetValue(voiceOption);
            string? stt = parseResult.GetValue(sttOption);

            await TodayCommandHandler.ExecuteAsync(
                serviceProvider,
                notes,
                noEdit,
                useDefaultTemplate,
                templateName,
                provider,
                useVoice,
                stt,
                jsonOutput).ConfigureAwait(false);
        });

        return todayCommand;
    }
}
