using System.Text.Json;
using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Storage.Json;
using Playline.Storage.Paths;

namespace Playline.Storage.Settings;

public sealed class JsonSettingsRepository(
    AppDataPaths paths,
    ICriticalErrorLogger logger) : ISettingsRepository
{
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            paths.EnsureCreated();

            if (!File.Exists(paths.SettingsFile))
            {
                var defaultSettings = new AppSettings();
                await SaveAsync(defaultSettings, cancellationToken).ConfigureAwait(false);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(paths.SettingsFile, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                var defaultSettings = new AppSettings();
                await SaveAsync(defaultSettings, cancellationToken).ConfigureAwait(false);
                return defaultSettings;
            }

            var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)
                ?? new AppSettings();
            var normalizedSettings = loadedSettings.Normalize();
            if (loadedSettings != normalizedSettings)
            {
                await SaveAsync(normalizedSettings, cancellationToken).ConfigureAwait(false);
            }

            return normalizedSettings;
        }
        catch (JsonException exception)
        {
            await logger.LogAsync(
                    "The application settings could not be loaded. Default settings will be used.",
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);

            var defaultSettings = new AppSettings();
            await CorruptJsonRecovery.TryReplaceAsync(
                    paths.SettingsFile,
                    token => SaveAsync(defaultSettings, token),
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);

            return defaultSettings;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await logger.LogAsync(
                    "The application settings could not be loaded. Default settings will be used.",
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);

            return new AppSettings();
        }
    }

    public Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return JsonFileWriter.WriteAsync(paths.SettingsFile, settings.Normalize(), cancellationToken);
    }
}
