using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Auth.SshProviders;

/// <summary>
/// Detects SSH keys from the file system (~/.ssh directory)
/// Scans for *.pub files and validates SSH key formats
/// </summary>
public sealed class FileSystemSshKeyDetector : ISshKeyDetector
{
    private readonly ILogger<FileSystemSshKeyDetector> _logger;

    public FileSystemSshKeyDetector(ILogger<FileSystemSshKeyDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SshKeySource Source => SshKeySource.FileSystem;

    public async Task<IReadOnlyList<SshKeyInfo>> DetectKeysAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var keys = new List<SshKeyInfo>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            await Task.Run(() =>
            {
                var sshDir = GetSshDirectory();
                if (!Directory.Exists(sshDir))
                {
                    _logger.LogDebug("SSH directory not found at {SshDir}", sshDir);
                    return;
                }

                _logger.LogDebug("Scanning SSH directory at {SshDir}", sshDir);

                try
                {
                    var pubKeyFiles = Directory.GetFiles(sshDir, "*.pub", SearchOption.TopDirectoryOnly);

                    foreach (var pubKeyFile in pubKeyFiles)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        try
                        {
                            var content = File.ReadAllText(pubKeyFile);

                            // Accept all SSH key types: ssh-rsa, ssh-ed25519, ecdsa-sha2-*, ssh-dss
                            if (content.StartsWith(SshConstants.KeyPrefixes.Ssh) || content.StartsWith(SshConstants.KeyPrefixes.Ecdsa))
                            {
                                var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    var keyType = parts[0];
                                    var isEd25519 = keyType == SshConstants.KeyTypes.Ed25519;
                                    var fileName = Path.GetFileNameWithoutExtension(pubKeyFile);
                                    var defaultComment = keyType switch
                                    {
                                        SshConstants.KeyTypes.Ed25519 => fileName,
                                        SshConstants.KeyTypes.Rsa => fileName,
                                        SshConstants.KeyTypes.EcdsaNistP256 => fileName,
                                        SshConstants.KeyTypes.EcdsaNistP384 => fileName,
                                        SshConstants.KeyTypes.EcdsaNistP521 => fileName,
                                        SshConstants.KeyTypes.Dsa => fileName,
                                        _ => fileName
                                    };
                                    var comment = parts.Length > 2 ? parts[2] : defaultComment;

                                    keys.Add(new SshKeyInfo
                                    {
                                        DisplayName = $"[File] ~/.ssh/{Path.GetFileName(pubKeyFile)} ({keyType})",
                                        Source = SshKeySource.FileSystem,
                                        PublicKey = content.TrimEnd(),
                                        FilePath = pubKeyFile,
                                        IsEd25519 = isEd25519,
                                        DetectedAt = DateTime.UtcNow,
                                        ValidationResult = ValidationResult.Valid
                                    });

                                    _logger.LogDebug("Found {KeyType} key: {FileName}", keyType, Path.GetFileName(pubKeyFile));
                                }
                            }
                            else
                            {
                                _logger.LogDebug("Skipping unrecognized key format: {FileName}", Path.GetFileName(pubKeyFile));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to read SSH key file: {FilePath}", pubKeyFile);
                        }
                    }

                    _logger.LogDebug("Detected {Count} SSH keys from file system", keys.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to scan SSH directory");
                }
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("File system SSH key detection timed out after {Timeout}", timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting keys from file system");
        }

        return keys;
    }

    private static string GetSshDirectory()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ".ssh");
    }
}
