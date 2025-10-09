using FluentValidation;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Validation;

/// <summary>
/// Validator for ConfigCommand
/// Ensures command parameters are valid before execution
/// </summary>
public sealed class ConfigCommandValidator : AbstractValidator<ConfigCommand>
{
    private static readonly string[] ValidSettingNames =
    [
        "llm-provider",
        "api-key",
        "memory-directory",
        "ssh-key-path",
        "log-level",
        "retention-days"
    ];

    public ConfigCommandValidator()
    {
        // SettingName is required for Set action
        RuleFor(x => x.SettingName)
            .NotEmpty()
            .When(x => x.Action == ConfigAction.Set)
            .WithMessage("SettingName is required for Set action");

        // SettingName must be valid if provided
        RuleFor(x => x.SettingName)
            .Must(name => string.IsNullOrWhiteSpace(name) || ValidSettingNames.Contains(name.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.SettingName))
            .WithMessage($"SettingName must be one of: {string.Join(", ", ValidSettingNames)}");

        // SettingValue is required for Set action
        RuleFor(x => x.SettingValue)
            .NotEmpty()
            .When(x => x.Action == ConfigAction.Set)
            .WithMessage("SettingValue is required for Set action");

        // ShowSecrets only valid for Show action
        RuleFor(x => x.ShowSecrets)
            .Equal(false)
            .When(x => x.Action != ConfigAction.Show)
            .WithMessage("ShowSecrets is only valid for Show action");
    }
}
