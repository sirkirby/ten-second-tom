using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Auth;
using TenSecondTom.Features.Generate;
using TenSecondTom.Features.Search;
using TenSecondTom.Features.Setup;
using TenSecondTom.Features.Shell;
using TenSecondTom.Features.Templates;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.DependencyInjection;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Cli;

/// <summary>
/// Unit tests for CommandRegistry to ensure proper command registration.
/// These tests verify that all commands are registered and accessible.
/// </summary>
public sealed class CommandRegistryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public CommandRegistryTests()
    {
        var services = new ServiceCollection();

        // Create empty configuration for tests
        var configuration = new ConfigurationBuilder().Build();

        // Infrastructure (cross-cutting concerns)
        services.AddInfrastructureServices();

        // Feature slices (vertical slice architecture)
        // Note: /today and /thisweek commands removed - use /note command with date ranges
        services.AddSearchFeature();
        services.AddAuthFeature();
        services.AddTemplatesFeature();
        services.AddSetupFeature(configuration);
        services.AddShellFeature();
        services.AddGenerateFeature();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void BuildRootCommand_ShouldRegisterAllExpectedCommands()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);

        // Assert
        var subcommandNames = rootCommand.Subcommands.Select(c => c.Name).ToList();

        subcommandNames.Should().Contain("note", "note command should be registered");
        subcommandNames.Should().Contain("search", "search command should be registered");
        subcommandNames.Should().Contain("record", "record command should be registered");
        subcommandNames.Should().Contain("transcribe", "transcribe command should be registered");
        subcommandNames.Should().Contain("generate", "generate command should be registered");
        subcommandNames.Should().Contain("login", "login command should be registered");
        subcommandNames.Should().Contain("logout", "logout command should be registered");
        subcommandNames.Should().Contain("config", "config command should be registered");
        subcommandNames.Should().Contain("shell", "shell command should be registered");
        subcommandNames.Should().Contain("help", "help command should be registered");
        subcommandNames.Should().Contain("version", "version command should be registered");
        subcommandNames.Should().Contain("audio", "audio parent command should be registered");
        subcommandNames.Should().Contain("auth", "auth parent command should be registered");
        subcommandNames.Should().Contain("storage", "storage parent command should be registered");
        subcommandNames.Should().Contain("llm", "llm parent command should be registered");

        // Removed commands: today, thisweek, setup (replaced by /config all)
    }

    [Fact]
    public void BuildRootCommand_ShouldHaveExpectedCommandCount()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);

        // Assert - 15 commands: note, search, record, transcribe, generate,
        // login, logout, config, llm, shell, help, version, audio, auth, storage
        // Removed: today, thisweek, setup (setup replaced by /config all)
        rootCommand.Subcommands.Should().HaveCount(15,
            "root command should have 15 subcommands");
    }

    [Fact]
    public void BuildRootCommand_ConfigAllCommand_ShouldRunSetupWizard()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var configCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "config");
        var allCommand = configCommand?.Subcommands.FirstOrDefault(c => c.Name == "all");

        // Assert
        allCommand.Should().NotBeNull("config all command should be registered");
        allCommand!.Description.Should().Contain("setup wizard", "should describe the setup wizard functionality");
    }

    [Fact]
    public void BuildRootCommand_ConfigCommand_ShouldBeRegistered()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var configCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "config");

        // Assert
        configCommand.Should().NotBeNull("config command should be registered");
        configCommand!.Description.Should().Contain("configuration", "should describe config command");
        
        // Verify config subcommands
        var configSubcommands = configCommand.Subcommands.Select(c => c.Name).ToList();
        configSubcommands.Should().Contain("show", "config should have show subcommand");
        configSubcommands.Should().Contain("set", "config should have set subcommand");
        configSubcommands.Should().Contain("validate", "config should have validate subcommand");
    }

    [Fact]
    public void BuildRootCommand_HelpCommand_ShouldBeRegistered()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var helpCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "help");

        // Assert
        helpCommand.Should().NotBeNull("help command should be registered");
        helpCommand!.Description.Should().Contain("available commands", "should describe help command");
    }

    [Fact]
    public void BuildRootCommand_AllCommands_ShouldHaveDescriptions()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);

        // Assert
        foreach (var command in rootCommand.Subcommands)
        {
            command.Description.Should().NotBeNullOrWhiteSpace(
                $"command '{command.Name}' should have a description");
            command.Description.Length.Should().BeGreaterThan(10,
                $"command '{command.Name}' description should be meaningful");
        }
    }

    [Fact]
    public void BuildRootCommand_SearchCommand_ShouldAcceptMultiWordQueries()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var searchCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "search");

        // Assert
        searchCommand.Should().NotBeNull("search command should be registered");
        
        // Verify the query argument accepts multiple words
        var queryArgument = searchCommand!.Arguments.FirstOrDefault();
        queryArgument.Should().NotBeNull("search command should have a query argument");
        queryArgument!.Name.Should().Be("query", "argument should be named 'query'");
        queryArgument.Arity.MinimumNumberOfValues.Should().Be(0, 
            "query argument uses ZeroOrMore to allow options to be parsed first");
        queryArgument.Arity.MaximumNumberOfValues.Should().BeGreaterThan(1, 
            "query argument should accept multiple words without quotes");
    }

    [Fact]
    public void BuildRootCommand_SearchCommand_ShouldHaveQueryArgument()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var searchCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "search");

        // Assert
        searchCommand.Should().NotBeNull("search command should be registered");
        searchCommand!.Arguments.Should().HaveCount(1, "search command should have exactly one argument");
        
        var queryArgument = searchCommand.Arguments.First();
        queryArgument.Name.Should().Be("query", "argument should be named 'query'");
        queryArgument.Description.Should().Contain("search", 
            "query argument should describe search functionality");
    }

    [Fact]
    public void BuildRootCommand_SearchCommand_ShouldHaveDateFilterOptions()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var searchCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "search");

        // Assert
        searchCommand.Should().NotBeNull("search command should be registered");
        
        var optionNames = searchCommand!.Options.Select(o => o.Name).ToList();
        optionNames.Should().Contain("--from-date", "search should support --from-date filter");
        optionNames.Should().Contain("--to-date", "search should support --to-date filter");
    }

    [Fact]
    public void BuildRootCommand_SearchCommand_ShouldSupportJsonOutput()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var searchCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "search");

        // Assert
        searchCommand.Should().NotBeNull("search command should be registered");
        
        var jsonOption = searchCommand!.Options.FirstOrDefault(o => o.Name == "--output-json");
        jsonOption.Should().NotBeNull("search should support --output-json option");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
