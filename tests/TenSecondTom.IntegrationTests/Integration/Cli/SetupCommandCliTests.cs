using System.Diagnostics;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace TenSecondTom.IntegrationTests.Integration.Cli;

/// <summary>
/// CLI smoke tests for setup and config commands.
/// Verifies basic command structure and help output without testing full scenarios.
/// </summary>
public sealed class SetupCommandCliTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _projectRoot;
    private readonly string _executablePath;

    public SetupCommandCliTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Find project root (traverse up from test assembly location)
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "TenSecondTom.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        _projectRoot = currentDir ?? throw new InvalidOperationException("Could not find project root");
        
        // Determine executable path based on build configuration
        #if DEBUG
        _executablePath = Path.Combine(_projectRoot, "src", "bin", "Debug", "net9.0", "TenSecondTom");
        #else
        _executablePath = Path.Combine(_projectRoot, "src", "bin", "Release", "net9.0", "TenSecondTom");
        #endif

        // Verify executable exists, if not try to build it
        if (!File.Exists(_executablePath) && !File.Exists(_executablePath + ".exe"))
        {
            _output.WriteLine($"Executable not found at {_executablePath}, attempting to build...");
            BuildProject();
        }
    }

    private void BuildProject()
    {
        using var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build src/TenSecondTom.csproj -c Debug",
                WorkingDirectory = _projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        buildProcess.Start();
        buildProcess.WaitForExit(30000); // 30 second timeout

        if (buildProcess.ExitCode != 0)
        {
            var error = buildProcess.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to build project: {error}");
        }
    }

    private Process CreateCliProcess(string arguments)
    {
        var executablePath = _executablePath;
        
        // On Windows, add .exe extension if needed
        if (OperatingSystem.IsWindows() && !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            executablePath += ".exe";
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"CLI executable not found at {executablePath}");
        }

        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = _projectRoot,
                RedirectStandardInput = true,  // Prevent blocking on stdin
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
    }

    private async Task<(string Output, string Error, int ExitCode)> RunCliCommandAsync(
        string arguments,
        int timeoutMs = 5000)
    {
        using var process = CreateCliProcess(arguments);
        
        process.Start();
        
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

        _output.WriteLine($"Command: TenSecondTom {arguments}");
        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"Output: {output}");
        if (!string.IsNullOrEmpty(error))
        {
            _output.WriteLine($"Error: {error}");
        }

        return (output, error, process.ExitCode);
    }

    [Fact]
    public async Task SetupCommand_Help_DisplaysUsageInformation()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("setup --help");

        // Assert
        exitCode.Should().Be(0, "help should always succeed");
        output.Should().Contain("setup", "help should mention the setup command");
        output.Should().NotBeNullOrWhiteSpace("help output should not be empty");
    }

    [Fact]
    public async Task SetupCommand_InvalidFlag_ProducesError()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("setup --invalid-flag");

        // Assert
        exitCode.Should().NotBe(0, "invalid flag should produce non-zero exit code");
        var combinedOutput = (output + error).ToLowerInvariant();

        // System.CommandLine may use different error messages across versions
        // Check for common error indicators
        combinedOutput.Should().MatchRegex("(invalid|unrecognized|unknown)",
            "output should indicate an error with the flag");
    }

    [Fact]
    public async Task SetupCommand_ForceFlag_IsRecognized()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("setup --force --help");

        // Assert
        exitCode.Should().Be(0, "help with valid flag should succeed");
        output.Should().Contain("force", "help should document the force flag");
    }

    [Fact]
    public async Task SetupCommand_NonInteractiveFlag_IsRecognized()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("setup --non-interactive --help");

        // Assert
        exitCode.Should().Be(0, "help with valid flag should succeed");
        output.Should().Contain("non-interactive", "help should document the non-interactive flag");
    }

    [Fact]
    public async Task ConfigCommand_Help_DisplaysUsageInformation()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("config --help");

        // Assert
        exitCode.Should().Be(0, "help should always succeed");
        output.Should().Contain("config", "help should mention the config command");
        output.Should().NotBeNullOrWhiteSpace("help output should not be empty");
    }

    [Fact]
    public async Task ConfigCommand_Show_IsRecognized()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("config --show --help");

        // Assert
        exitCode.Should().Be(0, "help with valid flag should succeed");
        output.Should().Contain("show", "help should document the show flag");
    }

    [Fact]
    public async Task ConfigCommand_InvalidSubcommand_ProducesError()
    {
        // Act - increased timeout for CI environments
        var (output, error, exitCode) = await RunCliCommandAsync("config --invalid-subcommand", timeoutMs: 10000);

        // Assert
        exitCode.Should().NotBe(0, "invalid subcommand should produce non-zero exit code");
        var combinedOutput = (output + error).ToLowerInvariant();

        // System.CommandLine may use different error messages across versions
        // Check for common error indicators
        combinedOutput.Should().MatchRegex("(invalid|unrecognized|unknown|required command)",
            "output should indicate an error with the command");
    }

    [Fact]
    public async Task RootCommand_ListsAvailableCommands()
    {
        // Act
        var (output, error, exitCode) = await RunCliCommandAsync("--help");

        // Assert
        exitCode.Should().Be(0, "help should always succeed");
        output.Should().Contain("setup", "root help should list setup command");
        output.Should().Contain("config", "root help should list config command");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
