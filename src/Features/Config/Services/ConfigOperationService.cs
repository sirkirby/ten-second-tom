using MediatR;
using TenSecondTom.Shared.Abstractions.Configuration;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Config.Services;

/// <summary>
/// Bridges CLI/infrastructure callers to the Config feature via MediatR.
/// </summary>
public sealed class ConfigOperationService(IMediator mediator) : IConfigOperationService
{
    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    public Task<Result<ConfigDisplay>> ExecuteAsync(
        ConfigAction action,
        string? settingName,
        string? settingValue,
        bool showSecrets,
        CancellationToken cancellationToken)
    {
        var command = new ShowConfig.Command
        {
            Action = action,
            SettingName = settingName,
            SettingValue = settingValue,
            ShowSecrets = showSecrets
        };

        return _mediator.Send(command, cancellationToken);
    }
}

