using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.ThisWeek;

/// <summary>
/// Provides the /thisweek command via ICommandBuilder discovery.
/// </summary>
public sealed class ThisWeekCliCommandBuilder : ICommandBuilder
{
    public int Priority => 25;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var thisWeekCommand = new Command("thisweek", "Generate a weekly review from recent daily entries");

        var fromDateOption = new Option<DateTimeOffset?>("--from-date") { Description = "Start date for custom range (yyyy-MM-dd). Must be used with --to-date." };
        var toDateOption = new Option<DateTimeOffset?>("--to-date") { Description = "End date for custom range (yyyy-MM-dd). Must be used with --from-date." };
        var providerOption = new Option<string?>("--provider") { Description = "LLM provider to use (OpenAI or Anthropic). Defaults to configured provider." };

        thisWeekCommand.Options.Add(fromDateOption);
        thisWeekCommand.Options.Add(toDateOption);
        thisWeekCommand.Options.Add(providerOption);
        thisWeekCommand.Options.Add(jsonOutputOption);

        thisWeekCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            DateTimeOffset? fromDate = parseResult.GetValue(fromDateOption);
            DateTimeOffset? toDate = parseResult.GetValue(toDateOption);
            string? provider = parseResult.GetValue(providerOption);

            var handler = serviceProvider.GetRequiredService<CreateWeeklyReview.Handler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await ThisWeekCommandHandler.ExecuteAsync(
                serviceProvider,
                handler,
                authService,
                fromDate,
                toDate,
                provider,
                jsonOutput).ConfigureAwait(false);
        });

        return thisWeekCommand;
    }
}
