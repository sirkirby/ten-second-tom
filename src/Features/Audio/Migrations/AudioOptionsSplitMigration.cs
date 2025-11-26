using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Migrations;

/// <summary>
/// Migrates STT configuration from AudioOptions to new TranscribeOptions section.
/// This migration runs during application bootstrap to split the configuration.
/// </summary>
/// <remarks>
/// Migration logic:
/// 1. Check if Audio section has legacy STT fields (SttProvider, Providers, KeepFiles)
/// 2. If yes, ensure Transcribe section exists with those values
/// 3. Remove the legacy fields from Audio section
///
/// The migration is idempotent - safe to run multiple times.
/// </remarks>
public sealed class AudioOptionsSplitMigration : IFeatureMigration
{
    private static readonly string[] LegacySttFields = ["SttProvider", "Providers", "KeepFiles"];

    /// <inheritdoc/>
    public string FeatureName => "Audio/Transcribe Split";

    /// <inheritdoc/>
    public int Priority => 5; // Run early - before other feature migrations

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<AudioOptionsSplitMigration>>();
        var sectionStore = services.GetRequiredService<IConfigurationSectionStore>();

        try
        {
            // Read the full config
            var configResult = await sectionStore.ReadFullConfigAsync(cancellationToken).ConfigureAwait(false);
            if (!configResult.IsSuccess)
            {
                logger.LogDebug("No configuration file found. Skipping migration (fresh install).");
                return false;
            }

            using var document = configResult.Value;

            // Check if Audio section has legacy STT fields
            if (!HasLegacySttFields(document))
            {
                logger.LogDebug("No legacy STT fields in Audio section. Skipping migration.");
                return false;
            }

            // Extract STT config from Audio section
            if (!TryGetAudioSttConfig(document, out var sttProvider, out var providers, out var keepFiles))
            {
                logger.LogDebug("Could not extract STT configuration. Skipping migration.");
                return false;
            }

            // Check if Transcribe section already exists
            var transcribeExists = SectionExists(document, "Transcribe");

            // If Transcribe doesn't exist, create it with the extracted values
            if (!transcribeExists)
            {
                var transcribeOptions = new TranscribeOptions
                {
                    SttProvider = sttProvider,
                    Providers = providers,
                    KeepFiles = keepFiles
                };

                var writeResult = await sectionStore.WriteSectionAsync(
                    TranscribeOptions.SectionPath,
                    transcribeOptions,
                    cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess)
                {
                    logger.LogWarning("Failed to write Transcribe section: {Error}", writeResult.Error);
                    return false;
                }

                logger.LogInformation(
                    "Created Transcribe section from Audio STT config. Provider: {Provider}",
                    sttProvider);
            }

            // Now clean up the legacy fields from Audio section
            var cleanupResult = await CleanupAudioSectionAsync(sectionStore, cancellationToken).ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
            {
                logger.LogWarning("Failed to cleanup Audio section: {Error}", cleanupResult.Error);
                return false;
            }

            logger.LogInformation("Removed legacy STT fields from Audio section.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audio/Transcribe split migration failed.");
            return false;
        }
    }

    /// <summary>
    /// Checks if Audio section has any legacy STT fields.
    /// </summary>
    private static bool HasLegacySttFields(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return false;
        }

        if (!tenSecondTom.TryGetProperty("Audio", out var audio))
        {
            return false;
        }

        foreach (var field in LegacySttFields)
        {
            if (audio.TryGetProperty(field, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a section exists in the TenSecondTom configuration.
    /// </summary>
    private static bool SectionExists(JsonDocument document, string sectionName)
    {
        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return false;
        }

        return tenSecondTom.TryGetProperty(sectionName, out _);
    }

    /// <summary>
    /// Removes legacy STT fields from the Audio section.
    /// </summary>
    private static async Task<Result> CleanupAudioSectionAsync(
        IConfigurationSectionStore sectionStore,
        CancellationToken cancellationToken)
    {
        // Read current config as mutable JsonNode
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

        var audio = tenSecondTom["Audio"]?.AsObject();
        if (audio is null)
        {
            return Result.Success(); // Nothing to clean up
        }

        // Remove legacy fields
        foreach (var field in LegacySttFields)
        {
            audio.Remove(field);
        }

        // Write the cleaned config back
        // We need to write just the Audio section, so extract it
        var audioJson = audio.ToJsonString();
        var cleanedAudio = JsonSerializer.Deserialize<AudioOptions>(audioJson);

        if (cleanedAudio is null)
        {
            return Result.Failure("Failed to deserialize cleaned Audio section");
        }

        var writeResult = await sectionStore.WriteSectionAsync(
            AudioOptions.SectionPath,
            cleanedAudio,
            cancellationToken).ConfigureAwait(false);

        return writeResult.IsSuccess ? Result.Success() : Result.Failure(writeResult.Error ?? "Failed to write Audio section");
    }

    /// <summary>
    /// Attempts to extract STT configuration from the Audio section.
    /// </summary>
    private static bool TryGetAudioSttConfig(
        JsonDocument document,
        out string sttProvider,
        out Dictionary<string, Dictionary<string, string>> providers,
        out bool keepFiles)
    {
        sttProvider = string.Empty;
        providers = new Dictionary<string, Dictionary<string, string>>();
        keepFiles = true;

        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return false;
        }

        if (!tenSecondTom.TryGetProperty("Audio", out var audio))
        {
            return false;
        }

        // Check if SttProvider exists (indicates old config structure)
        if (!audio.TryGetProperty("SttProvider", out var sttProviderElement))
        {
            return false;
        }

        sttProvider = sttProviderElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sttProvider))
        {
            return false;
        }

        // Extract Providers dictionary
        if (audio.TryGetProperty("Providers", out var providersElement))
        {
#pragma warning disable IDISP004 // JsonElement.ObjectEnumerator is a struct
            var providerEntries = providersElement.EnumerateObject().ToList();
#pragma warning restore IDISP004
            foreach (var provider in providerEntries)
            {
                var providerConfig = new Dictionary<string, string>();
#pragma warning disable IDISP004
                var settingEntries = provider.Value.EnumerateObject().ToList();
#pragma warning restore IDISP004
                foreach (var setting in settingEntries)
                {
                    var value = setting.Value.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        providerConfig[setting.Name] = value;
                    }
                }

                if (providerConfig.Count > 0)
                {
                    providers[provider.Name] = providerConfig;
                }
            }
        }

        // Extract KeepFiles (default to true if not present)
        if (audio.TryGetProperty("KeepFiles", out var keepFilesElement))
        {
            keepFiles = keepFilesElement.GetBoolean();
        }

        return true;
    }
}
