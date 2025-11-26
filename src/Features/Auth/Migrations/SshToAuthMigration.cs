using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth.Migrations;

/// <summary>
/// Migrates SSH configuration from legacy "Ssh" section to "Auth" section.
/// This migration runs during application bootstrap to clean up old config structure.
/// </summary>
/// <remarks>
/// Migration logic:
/// 1. Check if legacy "Ssh" section exists
/// 2. If Auth section doesn't exist, create it from Ssh values
/// 3. Remove the legacy Ssh section
///
/// The migration is idempotent - safe to run multiple times.
/// </remarks>
public sealed class SshToAuthMigration : IFeatureMigration
{
    /// <inheritdoc/>
    public string FeatureName => "Ssh to Auth Migration";

    /// <inheritdoc/>
    public int Priority => 4; // Run very early, before other migrations

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<SshToAuthMigration>>();
        var sectionStore = services.GetRequiredService<IConfigurationSectionStore>();

        try
        {
            // Read the full config
            var configResult = await sectionStore.ReadFullConfigAsync(cancellationToken).ConfigureAwait(false);
            if (!configResult.IsSuccess)
            {
                logger.LogDebug("No configuration file found. Skipping migration.");
                return false;
            }

            using var document = configResult.Value;

            // Check if legacy Ssh section exists
            if (!HasLegacySshSection(document))
            {
                logger.LogDebug("No legacy Ssh section found. Skipping migration.");
                return false;
            }

            // Check if Auth section already exists
            var authExists = SectionExists(document, "Auth");

            // If Auth doesn't exist, create it from Ssh values
            if (!authExists)
            {
                if (TryGetSshConfig(document, out var keyPath, out var keySource, out var agentSocketPath, out var keyDisplayName))
                {
                    var authOptions = new AuthOptions
                    {
                        KeyPath = keyPath,
                        KeySource = keySource,
                        AgentSocketPath = agentSocketPath,
                        KeyDisplayName = keyDisplayName
                    };

                    var writeResult = await sectionStore.WriteSectionAsync(
                        AuthOptions.SectionPath,
                        authOptions,
                        cancellationToken).ConfigureAwait(false);

                    if (!writeResult.IsSuccess)
                    {
                        logger.LogWarning("Failed to write Auth section: {Error}", writeResult.Error);
                        return false;
                    }

                    logger.LogInformation("Created Auth section from legacy Ssh config.");
                }
            }

            // Remove the legacy Ssh section
            var cleanupResult = await RemoveLegacySshSectionAsync(sectionStore, cancellationToken).ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
            {
                logger.LogWarning("Failed to remove legacy Ssh section: {Error}", cleanupResult.Error);
                return false;
            }

            logger.LogInformation("Removed legacy Ssh section from configuration.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ssh to Auth migration failed.");
            return false;
        }
    }

    private static bool HasLegacySshSection(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return false;
        }

        return tenSecondTom.TryGetProperty("Ssh", out _);
    }

    private static bool SectionExists(JsonDocument document, string sectionName)
    {
        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return false;
        }

        return tenSecondTom.TryGetProperty(sectionName, out _);
    }

    private static bool TryGetSshConfig(
        JsonDocument document,
        out string? keyPath,
        out Shared.Models.SshKeySource? keySource,
        out string? agentSocketPath,
        out string? keyDisplayName)
    {
        keyPath = null;
        keySource = null;
        agentSocketPath = null;
        keyDisplayName = null;

        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return false;
        }

        if (!tenSecondTom.TryGetProperty("Ssh", out var ssh))
        {
            return false;
        }

        if (ssh.TryGetProperty("KeyPath", out var keyPathElement) && keyPathElement.ValueKind == JsonValueKind.String)
        {
            keyPath = keyPathElement.GetString();
        }

        if (ssh.TryGetProperty("KeySource", out var keySourceElement) && keySourceElement.ValueKind == JsonValueKind.String)
        {
            var keySourceStr = keySourceElement.GetString();
            if (!string.IsNullOrEmpty(keySourceStr) && Enum.TryParse<Shared.Models.SshKeySource>(keySourceStr, out var parsedSource))
            {
                keySource = parsedSource;
            }
        }

        if (ssh.TryGetProperty("AgentSocketPath", out var agentElement) && agentElement.ValueKind == JsonValueKind.String)
        {
            agentSocketPath = agentElement.GetString();
        }

        if (ssh.TryGetProperty("KeyDisplayName", out var displayNameElement) && displayNameElement.ValueKind == JsonValueKind.String)
        {
            keyDisplayName = displayNameElement.GetString();
        }

        return keySource.HasValue || !string.IsNullOrEmpty(keyPath);
    }

    private static async Task<Result> RemoveLegacySshSectionAsync(
        IConfigurationSectionStore sectionStore,
        CancellationToken cancellationToken)
    {
        var configResult = await sectionStore.ReadFullConfigAsync(cancellationToken).ConfigureAwait(false);
        if (!configResult.IsSuccess)
        {
            return Result.Failure("Could not read configuration");
        }

        using var document = configResult.Value;

        // Parse into mutable JsonNode
        var jsonNode = JsonNode.Parse(document.RootElement.GetRawText());
        if (jsonNode is not JsonObject root)
        {
            return Result.Failure("Configuration root is not an object");
        }

        var tenSecondTom = root["TenSecondTom"]?.AsObject();
        if (tenSecondTom is null)
        {
            return Result.Failure("TenSecondTom section not found");
        }

        // Remove the Ssh section
        if (!tenSecondTom.Remove("Ssh"))
        {
            return Result.Success(); // Already removed
        }

        // Write the entire config back
        var writeResult = await sectionStore.WriteFullConfigAsync(root, cancellationToken).ConfigureAwait(false);
        return writeResult.IsSuccess ? Result.Success() : Result.Failure(writeResult.Error ?? "Failed to write config");
    }
}
