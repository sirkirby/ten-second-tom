using FluentValidation;
using TenSecondTom.Features.Setup.Commands;

namespace TenSecondTom.Features.Setup.Validation;

/// <summary>
/// Validator for SetupCommand
/// Ensures command parameters are valid before execution
/// </summary>
public sealed class SetupCommandValidator : AbstractValidator<SetupCommand>
{
    public SetupCommandValidator()
    {
        // NonInteractive mode requires ExistingConfiguration
        RuleFor(x => x.ExistingConfiguration)
            .NotNull()
            .When(x => x.NonInteractive)
            .WithMessage("ExistingConfiguration must be provided when NonInteractive is true");

        // ExistingConfiguration must be valid if provided
        RuleFor(x => x.ExistingConfiguration)
            .Must(config => config == null || config.IsValid())
            .When(x => x.ExistingConfiguration != null)
            .WithMessage("ExistingConfiguration must be valid if provided");
    }
}
