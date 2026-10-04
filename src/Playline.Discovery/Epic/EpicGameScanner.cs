using System.Text.Json;
using System.Text.Json.Serialization;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery.Contracts;

namespace Playline.Discovery.Epic;

public sealed class EpicGameScanner(IEpicManifestLocator manifestLocator) : IGameScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "Epic Games";

    public async Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<Game>();
        var identityIndex = new GameIdentityIndex();

        foreach (var manifestDirectory in manifestLocator.LocateManifestDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(manifestDirectory, "*.item").ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var manifestPath in manifests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var game = await ParseManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);
                if (game is not null && identityIndex.TryAdd(game))
                {
                    games.Add(game);
                }
            }
        }

        return games;
    }

    private static async Task<Game?> ParseManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            var manifest = await JsonSerializer
                .DeserializeAsync<EpicManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (manifest is null
                || manifest.IsIncompleteInstall
                || string.IsNullOrWhiteSpace(manifest.DisplayName)
                || string.IsNullOrWhiteSpace(manifest.InstallLocation)
                || !Directory.Exists(manifest.InstallLocation))
            {
                return null;
            }

            var stableIdentifier = FirstNotEmpty(manifest.CatalogItemId, manifest.AppName);
            if (stableIdentifier is null)
            {
                return null;
            }

            var executablePath = ResolveExecutablePath(manifest);
            var launchUri = CreateLaunchUri(manifest);
            if (executablePath is null && launchUri is null)
            {
                return null;
            }

            return new Game
            {
                Id = GameIdentity.CreateLauncherId("epic", stableIdentifier),
                Name = manifest.DisplayName.Trim(),
                ExecutablePath = executablePath,
                Arguments = NullIfWhiteSpace(manifest.LaunchCommand),
                WorkingDirectory = executablePath is null
                    ? Path.GetFullPath(manifest.InstallLocation)
                    : Path.GetDirectoryName(executablePath),
                LaunchUri = launchUri,
                Source = GameSource.Epic
            };
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or JsonException
                                             or NotSupportedException
                                             or ArgumentException)
        {
            return null;
        }
    }

    private static string? ResolveExecutablePath(EpicManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.LaunchExecutable))
        {
            return null;
        }

        var executable = Path.IsPathRooted(manifest.LaunchExecutable)
            ? manifest.LaunchExecutable
            : Path.Combine(manifest.InstallLocation!, manifest.LaunchExecutable);
        var fullPath = Path.GetFullPath(executable);

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static string? CreateLaunchUri(EpicManifest manifest)
    {
        var catalogNamespace = FirstNotEmpty(manifest.CatalogNamespace, manifest.MainGameCatalogNamespace);
        var artifactId = FirstNotEmpty(manifest.AppName, manifest.ArtifactId);

        if (catalogNamespace is null
            || string.IsNullOrWhiteSpace(manifest.CatalogItemId)
            || artifactId is null)
        {
            return null;
        }

        return "com.epicgames.launcher://apps/"
            + Uri.EscapeDataString(catalogNamespace)
            + "%3A"
            + Uri.EscapeDataString(manifest.CatalogItemId)
            + "%3A"
            + Uri.EscapeDataString(artifactId)
            + "?action=launch&silent=true";
    }

    private static string? FirstNotEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record EpicManifest
    {
        public string? DisplayName { get; init; }

        public string? InstallLocation { get; init; }

        public string? LaunchExecutable { get; init; }

        public string? LaunchCommand { get; init; }

        public string? AppName { get; init; }

        public string? ArtifactId { get; init; }

        public string? CatalogItemId { get; init; }

        public string? CatalogNamespace { get; init; }

        public string? MainGameCatalogNamespace { get; init; }

        [JsonPropertyName("bIsIncompleteInstall")]
        public bool IsIncompleteInstall { get; init; }
    }
}
