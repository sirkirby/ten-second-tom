using FluentValidation;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Services;

/// <summary>
/// Validator for ConfigurationSettings
/// Ensures complete configuration is valid
/// </summary>
public sealed class ConfigurationSettingsValidator : AbstractValidator<ConfigurationSettings>
{
    public ConfigurationSettingsValidator()
    {
        // SSH Configuration
        RuleFor(x => x.Ssh.KeyPath)
            .NotEmpty()
            .WithMessage("SSH key path is required");

        RuleFor(x => x.Ssh.KeyPath)
            .Must(path => string.IsNullOrWhiteSpace(path) || File.Exists(ExpandPath(path)))
            .When(x => !string.IsNullOrWhiteSpace(x.Ssh.KeyPath))
            .WithMessage("SSH key file does not exist");

        // LLM Configuration
        RuleFor(x => x.Llm.Provider)
            .IsInEnum()
            .WithMessage("LLM provider must be a valid provider");

        RuleFor(x => x.Llm.ApiKey)
            .NotEmpty()
            .WithMessage("API key is required");

        RuleFor(x => x.Llm.ApiKey)
            .Matches(@"^sk-[a-zA-Z0-9]{48,}$")
            .When(x => x.Llm.Provider == LlmProvider.OpenAI)
            .WithMessage("Invalid OpenAI API key format");

        RuleFor(x => x.Llm.ApiKey)
            .Matches(@"^sk-ant-[a-zA-Z0-9\-]{32,}$")
            .When(x => x.Llm.Provider == LlmProvider.Anthropic)
            .WithMessage("Invalid Anthropic API key format");

        // Storage Configuration
        RuleFor(x => x.RootDirectory)
            .NotEmpty()
            .WithMessage("Memory directory is required");

        RuleFor(x => x.RootDirectory)
            .Must(path => string.IsNullOrWhiteSpace(path) || IsValidDirectoryPath(path))
            .When(x => !string.IsNullOrWhiteSpace(x.RootDirectory))
            .WithMessage("Memory directory path is not valid");

        // Optional Configuration
        RuleFor(x => x.Optional.RetentionDays)
            .GreaterThan(0)
            .WithMessage("Retention days must be greater than 0");

        RuleFor(x => x.Optional.LogLevel)
            .IsInEnum()
            .WithMessage("Log level must be a valid log level");
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var expandedPath = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        return Path.GetFullPath(expandedPath);
    }

    private static bool IsValidDirectoryPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            return !string.IsNullOrWhiteSpace(fullPath);
        }
        catch
        {
            return false;
        }
    }
}
