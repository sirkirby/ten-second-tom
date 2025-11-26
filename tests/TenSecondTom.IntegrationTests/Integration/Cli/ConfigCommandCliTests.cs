using System.Diagnostics;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace TenSecondTom.IntegrationTests.Integration.Cli;

/// <summary>
/// CLI smoke tests for config and core commands.
/// Verifies basic command structure and help output without testing full scenarios.
///
/// These tests run the CLI via 'dotnet tom.dll' which works in all environments
/// (local dev, CI) without requiring a self-contained executable.
/// </summary>
public sealed class ConfigCommandCliTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _projectRoot;
    private readonly string _dllPath;

    public ConfigCommandCliTests(ITestOutputHelper output)
    {
        _output = output;

        // Find project root (traverse up from test assembly location)
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "TenSecondTom.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        _projectRoot = currentDir ?? throw new InvalidOperationException("Could not find project root");

        // Determine DLL path based on build configuration
        #if DEBUG
        _dllPath = Path.Combine(_projectRoot, "src", "bin", "Debug", "net10.0", "tom.dll");
        #else
        _dllPath = Path.Combine(_projectRoot, "src", "bin", "Release", "net10.0", "tom.dll");
        #endif

        // Verify DLL exists
        if (!File.Exists(_dllPath))
        {
            throw new FileNotFoundException(
                $"CLI assembly not found at {_dllPath}. Run 'dotnet build' first.",
                _dllPath);
        }
    }

    private Process CreateCliProcess(string arguments)
    {
        // Run via 'dotnet {dll}' - works everywhere without native executable
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_dllPath}\" {arguments}",
                WorkingDirectory = _projectRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
    }

    private async Task<(string Output, string Error, int ExitCode)> RunCliCommandAsync(
        string arguments,
        int timeoutMs = 15000)
    {
        using var process = CreateCliProcess(arguments);

        process.Start();

        // Close stdin immediately to prevent CLI from waiting for input
        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeoutTask = Task.Delay(timeoutMs);
        var processTask = Task.Run(() => process.WaitForExit());

        var completedTask = await Task.WhenAny(processTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
        {
            process.Kill();
            throw new TimeoutException($"CLI command '{arguments}' timed out after {timeoutMs}ms");
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        _output.WriteLine($"Command: dotnet tom.dll {arguments}");
        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"Output: {output}");
        if (!string.IsNullOrEmpty(error))
        {
            _output.WriteLine($"Error: {error}");
        }

        return (output, error, process.ExitCode);
    }

    [Fact]
    public async Task ConfigAllCommand_Help_DisplaysUsageInformation()
    {
        // Act
        var (output, _, exitCode) = await RunCliCommandAsync("config all --help");

        // Assert
        exitCode.Should().Be(0, "help should always succeed");
        output.Should().Contain("all", "help should mention the all subcommand");
        output.Should().NotBeNullOrWhiteSpace("help output should not be empty");
    }

    [Fact]
    public async Task ConfigCommand_Help_DisplaysUsageInformation()
    {
        // Act
        var (output, _, exitCode) = await RunCliCommandAsync("config --help");

        // Assert
        exitCode.Should().Be(0, "help should always succeed");
        output.Should().Contain("config", "help should mention the config command");
        output.Should().NotBeNullOrWhiteSpace("help output should not be empty");
    }

    [Fact]
    public async Task ConfigCommand_ShowSubcommand_IsRecognized()
    {
        // Act
        var (output, _, exitCode) = await RunCliCommandAsync("config show --help");

        // Assert
        exitCode.Should().Be(0, "help with valid subcommand should succeed");
        output.Should().Contain("show", "help should document the show subcommand");
    }

    [Fact]
    public async Task ConfigCommand_InvalidSubcommand_ProducesError()
    {
        // Act - use a clearly invalid subcommand (not a flag starting with --)
        // Use longer timeout as CI runners can be slow on first invocation
        var (output, error, exitCode) = await RunCliCommandAsync("config invalidsubcommand", timeoutMs: 30000);

        // Assert
        exitCode.Should().NotBe(0, "invalid subcommand should produce non-zero exit code");
        var combinedOutput = (output + error).ToLowerInvariant();

        // System.CommandLine may use different error messages across versions
        combinedOutput.Should().MatchRegex("(invalid|unrecognized|unknown|required command)",
            "output should indicate an error with the command");
    }

    [Fact]
    public async Task RootCommand_ListsAvailableCommands()
    {
        // Act
        var (output, _, exitCode) = await RunCliCommandAsync("--help");

        // Assert
        exitCode.Should().Be(0, "help should always succeed");
        output.Should().Contain("config", "root help should list config command");
        output.Should().Contain("record", "root help should list record command");
        output.Should().Contain("generate", "root help should list generate command");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
