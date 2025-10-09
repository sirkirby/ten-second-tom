using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Queries;

namespace TenSecondTom.Infrastructure.Auth.SshProviders;

/// <summary>
/// Detects SSH keys from 1Password SSH agent
/// Connects to 1Password agent socket on macOS
/// </summary>
public sealed class OnePasswordSshAgentDetector : ISshKeyDetector
{
    private readonly ILogger<OnePasswordSshAgentDetector> _logger;

    public OnePasswordSshAgentDetector(ILogger<OnePasswordSshAgentDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SshKeySource Source => SshKeySource.OnePasswordAgent;

    public async Task<IReadOnlyList<SshKeyInfo>> DetectKeysAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var keys = new List<SshKeyInfo>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            await Task.Run(() =>
            {
                var socketPath = GetOnePasswordSocketPath();
                if (string.IsNullOrEmpty(socketPath) || !File.Exists(socketPath))
                {
                    _logger.LogDebug("1Password SSH agent socket not found");
                    return;
                }

                _logger.LogDebug("Connecting to 1Password SSH agent at {SocketPath}", socketPath);

                try
                {
                    // Use SSH_AUTH_SOCK temporarily pointing to 1Password
                    var originalAuthSock = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
                    Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", socketPath);

                    try
                    {
                        var process = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "ssh-add",
                                Arguments = "-L",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };

                        process.Start();
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(timeout);

                        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                        {
                            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                if (line.StartsWith("ssh-ed25519"))
                                {
                                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 2)
                                    {
                                        var comment = parts.Length > 2 ? parts[2] : "1password-key";
                                        keys.Add(new SshKeyInfo
                                        {
                                            DisplayName = $"[1Password] {comment}",
                                            Source = SshKeySource.OnePasswordAgent,
                                            PublicKey = line,
                                            AgentName = "1Password",
                                            IsEd25519 = true,
                                            DetectedAt = DateTime.UtcNow,
                                            ValidationResult = ValidationResult.Valid
                                        });
                                    }
                                }
                            }
                        }

                        _logger.LogDebug("Detected {Count} ED25519 keys from 1Password SSH agent", keys.Count);
                    }
                    finally
                    {
                        // Restore original SSH_AUTH_SOCK
                        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", originalAuthSock);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query 1Password SSH agent");
                }
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("1Password SSH agent detection timed out after {Timeout}", timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting keys from 1Password SSH agent");
        }

        return keys;
    }

    private static string? GetOnePasswordSocketPath()
    {
        if (!OperatingSystem.IsMacOS())
            return null;

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var socketPath = Path.Combine(
            homeDir,
            "Library/Group Containers/2BUA8C4S2C.com.1password/t/agent.sock"
        );

        return socketPath;
    }
}
