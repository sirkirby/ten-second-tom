using Microsoft.Extensions.Logging;
using Renci.SshNet;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Auth.Constants;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Auth.SshProviders;

/// <summary>
/// Detects SSH keys from the system SSH agent
/// Connects via SSH_AUTH_SOCK (Unix) or named pipe (Windows)
/// </summary>
public sealed class SystemSshAgentDetector : ISshKeyDetector
{
    private readonly ILogger<SystemSshAgentDetector> _logger;

    public SystemSshAgentDetector(ILogger<SystemSshAgentDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SshKeySource Source => SshKeySource.SystemAgent;

    public async Task<IReadOnlyList<SshKeyInfo>> DetectKeysAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var keys = new List<SshKeyInfo>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            await Task.Run(() =>
            {
                var agentSocketPath = GetAgentSocketPath();
                if (string.IsNullOrEmpty(agentSocketPath))
                {
                    _logger.LogDebug("System SSH agent socket not found");
                    return;
                }

                _logger.LogDebug("Connecting to system SSH agent at {SocketPath}", agentSocketPath);

                try
                {
                    // Note: SSH.NET doesn't have direct agent support, so we'll need to use ssh-add or similar
                    // For now, we'll use a process-based approach as a fallback
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
                            // Accept all SSH key types: ssh-rsa, ssh-ed25519, ecdsa-sha2-*, ssh-dss
                            if (line.StartsWith(SshConstants.KeyPrefixes.Ssh) || line.StartsWith(SshConstants.KeyPrefixes.Ecdsa))
                            {
                                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    var keyType = parts[0];
                                    var isEd25519 = keyType == SshConstants.KeyTypes.Ed25519;
                                    var defaultComment = keyType switch
                                    {
                                        SshConstants.KeyTypes.Ed25519 => "id_ed25519",
                                        SshConstants.KeyTypes.Rsa => "id_rsa",
                                        SshConstants.KeyTypes.EcdsaNistP256 => "id_ecdsa",
                                        SshConstants.KeyTypes.EcdsaNistP384 => "id_ecdsa",
                                        SshConstants.KeyTypes.EcdsaNistP521 => "id_ecdsa",
                                        SshConstants.KeyTypes.Dsa => "id_dsa",
                                        _ => "id_unknown"
                                    };
                                    var comment = parts.Length > 2 ? parts[2] : defaultComment;

                                    keys.Add(new SshKeyInfo
                                    {
                                        DisplayName = $"[System Agent] {comment} ({keyType})",
                                        Source = SshKeySource.SystemAgent,
                                        PublicKey = line,
                                        AgentName = "ssh-agent",
                                        IsEd25519 = isEd25519,
                                        DetectedAt = DateTime.UtcNow,
                                        ValidationResult = ValidationResult.Valid
                                    });
                                }
                            }
                        }
                    }

                    _logger.LogDebug("Detected {Count} SSH keys from system SSH agent", keys.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query system SSH agent");
                }
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("System SSH agent detection timed out after {Timeout}", timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting keys from system SSH agent");
        }

        return keys;
    }

    private static string? GetAgentSocketPath()
    {
        // Unix: SSH_AUTH_SOCK environment variable
        var unixSocket = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        if (!string.IsNullOrEmpty(unixSocket) && File.Exists(unixSocket))
            return unixSocket;

        // Windows: Named pipe
        if (OperatingSystem.IsWindows())
        {
            var windowsPipe = @"\\.\pipe\openssh-ssh-agent";
            // Named pipes don't use File.Exists, assume it exists on Windows
            return windowsPipe;
        }

        return null;
    }
}
