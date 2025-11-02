using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Auth.SshProviders;

/// <summary>
/// Detects SSH keys from Secretive SSH agent
/// Connects to Secretive agent socket on macOS
/// </summary>
public sealed class SecretiveSshAgentDetector : ISshKeyDetector
{
    private readonly ILogger<SecretiveSshAgentDetector> _logger;

    public SecretiveSshAgentDetector(ILogger<SecretiveSshAgentDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SshKeySource Source => SshKeySource.SecretiveAgent;

    public async Task<IReadOnlyList<SshKeyInfo>> DetectKeysAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var keys = new List<SshKeyInfo>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            await Task.Run(() =>
            {
                var socketPath = GetSecretiveSocketPath();
                if (string.IsNullOrEmpty(socketPath) || !File.Exists(socketPath))
                {
                    _logger.LogDebug("Secretive SSH agent socket not found");
                    return;
                }

                _logger.LogDebug("Connecting to Secretive SSH agent at {SocketPath}", socketPath);

                try
                {
                    // Use SSH_AUTH_SOCK temporarily pointing to Secretive
                    var originalAuthSock = Environment.GetEnvironmentVariable(SshConstants.AgentPaths.SystemAgentEnvVar);
                    Environment.SetEnvironmentVariable(SshConstants.AgentPaths.SystemAgentEnvVar, socketPath);

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
                                            SshConstants.KeyTypes.Ed25519 => "secretive-ed25519",
                                            SshConstants.KeyTypes.Rsa => "secretive-rsa",
                                            SshConstants.KeyTypes.EcdsaNistP256 => "secretive-ecdsa",
                                            SshConstants.KeyTypes.EcdsaNistP384 => "secretive-ecdsa",
                                            SshConstants.KeyTypes.EcdsaNistP521 => "secretive-ecdsa",
                                            SshConstants.KeyTypes.Dsa => "secretive-dsa",
                                            _ => "secretive-key"
                                        };
                                        var comment = parts.Length > 2 ? parts[2] : defaultComment;

                                        keys.Add(new SshKeyInfo
                                        {
                                            DisplayName = $"[Secretive] {comment} ({keyType})",
                                            Source = SshKeySource.SecretiveAgent,
                                            PublicKey = line,
                                            AgentName = "Secretive",
                                            IsEd25519 = isEd25519,
                                            DetectedAt = DateTime.UtcNow,
                                            ValidationResult = ValidationResult.Valid
                                        });
                                    }
                                }
                            }
                        }

                        _logger.LogDebug("Detected {Count} SSH keys from Secretive SSH agent", keys.Count);
                    }
                    finally
                    {
                        // Restore original SSH_AUTH_SOCK
                        Environment.SetEnvironmentVariable(SshConstants.AgentPaths.SystemAgentEnvVar, originalAuthSock);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query Secretive SSH agent");
                }
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Secretive SSH agent detection timed out after {Timeout}", timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting keys from Secretive SSH agent");
        }

        return keys;
    }

    private static string? GetSecretiveSocketPath()
    {
        if (!OperatingSystem.IsMacOS())
            return null;

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var socketPath = Path.Combine(
            homeDir,
            SshConstants.AgentPaths.Secretive.MacContainers,
            SshConstants.AgentPaths.Secretive.MacContainerId,
            SshConstants.AgentPaths.Secretive.MacDataDirectory,
            SshConstants.AgentPaths.Secretive.MacSocketFile);

        return socketPath;
    }
}
