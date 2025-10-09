using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.DependencyInjection;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Cli;

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
        services.AddTenSecondTomServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void BuildRootCommand_ShouldRegisterAllExpectedCommands()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);

        // Assert
        var subcommandNames = rootCommand.Subcommands.Select(c => c.Name).ToList();
        
        subcommandNames.Should().Contain("today", "today command should be registered");
        subcommandNames.Should().Contain("thisweek", "thisweek command should be registered");
        subcommandNames.Should().Contain("search", "search command should be registered");
        subcommandNames.Should().Contain("login", "login command should be registered");
        subcommandNames.Should().Contain("logout", "logout command should be registered");
        subcommandNames.Should().Contain("setup", "setup command should be registered");
        subcommandNames.Should().Contain("config", "config command should be registered");
        subcommandNames.Should().Contain("shell", "shell command should be registered");
        subcommandNames.Should().Contain("help", "help command should be registered");
        subcommandNames.Should().Contain("version", "version command should be registered");
    }

    [Fact]
    public void BuildRootCommand_ShouldHaveExactlyTenCommands()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);

        // Assert
        rootCommand.Subcommands.Should().HaveCount(10, 
            "root command should have exactly 10 subcommands: today, thisweek, search, login, logout, setup, config, shell, help, version");
    }

    [Fact]
    public void BuildRootCommand_SetupCommand_ShouldBeRegistered()
    {
        // Arrange & Act
        var rootCommand = CommandRegistry.BuildRootCommand(_serviceProvider);
        var setupCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "setup");

        // Assert
        setupCommand.Should().NotBeNull("setup command should be registered");
        setupCommand!.Description.Should().Contain("setup wizard", "should describe setup command");
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

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
