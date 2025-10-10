using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Queries;

namespace TenSecondTom.Infrastructure.Auth.SshProviders;

/// <summary>
/// Detects SSH keys from the file system (~/.ssh directory)
/// Scans for *.pub files and validates ED25519 format
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
                            
                            // Check if it's an ED25519 key
                            if (content.StartsWith("ssh-ed25519"))
                            {
                                var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                var fileName = Path.GetFileNameWithoutExtension(pubKeyFile);
                                var comment = parts.Length > 2 ? parts[2] : fileName;

                                keys.Add(new SshKeyInfo
                                {
                                    DisplayName = $"[File] ~/.ssh/{Path.GetFileName(pubKeyFile)}",
                                    Source = SshKeySource.FileSystem,
                                    PublicKey = content.TrimEnd(),
                                    FilePath = pubKeyFile,
                                    IsEd25519 = true,
                                    DetectedAt = DateTime.UtcNow,
                                    ValidationResult = ValidationResult.Valid
                                });

                                _logger.LogDebug("Found ED25519 key: {FileName}", Path.GetFileName(pubKeyFile));
                            }
                            else
                            {
                                _logger.LogDebug("Skipping non-ED25519 key: {FileName}", Path.GetFileName(pubKeyFile));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to read SSH key file: {FilePath}", pubKeyFile);
                        }
                    }

                    _logger.LogDebug("Detected {Count} ED25519 keys from file system", keys.Count);
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
