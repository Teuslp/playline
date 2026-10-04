using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery.Contracts;

namespace Playline.Discovery.Steam;

public sealed class SteamGameScanner(ISteamInstallationLocator installationLocator) : IGameScanner
{
    public string Name => "Steam";

    public async Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var steamDirectory in installationLocator.LocateInstallations())
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddLibrary(libraries, steamDirectory);

            var libraryFile = Path.Combine(steamDirectory, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
            {
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(libraryFile, cancellationToken).ConfigureAwait(false);
                var root = ValveKeyValuesParser.Parse(content);
                var libraryFolders = root.GetChild("libraryfolders");
                if (libraryFolders is null)
                {
                    continue;
                }

                foreach (var (key, path) in libraryFolders.Values)
                {
                    if (int.TryParse(key, out _))
                    {
                        AddLibrary(libraries, path);
                    }
                }

                foreach (var (key, libraryNode) in libraryFolders.Children)
                {
                    if (int.TryParse(key, out _))
                    {
                        AddLibrary(libraries, libraryNode.GetValue("path"));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException
                                                 or UnauthorizedAccessException
                                                 or InvalidDataException
                                                 or ArgumentException
                                                 or NotSupportedException)
            {
            }
        }

        var games = new List<Game>();
        var identityIndex = new GameIdentityIndex();
        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamAppsDirectory = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamAppsDirectory))
            {
                continue;
            }

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(steamAppsDirectory, "appmanifest_*.acf").ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var manifest in manifests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var game = await ParseManifestAsync(library, manifest, cancellationToken).ConfigureAwait(false);
                if (game is not null && identityIndex.TryAdd(game))
                {
                    games.Add(game);
                }
            }
        }

        return games;
    }

    private static async Task<Game?> ParseManifestAsync(
        string library,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var root = ValveKeyValuesParser.Parse(content);
            var appState = root.GetChild("AppState");
            var appId = appState?.GetValue("appid");
            var name = appState?.GetValue("name");
            var installDirectoryName = appState?.GetValue("installdir");
            var stateFlags = appState?.GetValue("StateFlags");

            if (string.IsNullOrWhiteSpace(appId)
                || !ulong.TryParse(appId, out _)
                || string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(installDirectoryName))
            {
                return null;
            }

            if (int.TryParse(stateFlags, out var parsedStateFlags)
                && (parsedStateFlags & 4) == 0)
            {
                return null;
            }

            var installDirectory = Path.Combine(library, "steamapps", "common", installDirectoryName);
            if (!Directory.Exists(installDirectory))
            {
                return null;
            }

            return new Game
            {
                Id = GameIdentity.CreateLauncherId("steam", appId),
                Name = name,
                LaunchUri = $"steam://rungameid/{appId}",
                WorkingDirectory = installDirectory,
                Source = GameSource.Steam
            };
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or InvalidDataException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            return null;
        }
    }

    private static void AddLibrary(ISet<string> libraries, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            libraries.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)));
        }
        catch (Exception exception) when (exception is ArgumentException
                                             or NotSupportedException
                                             or PathTooLongException)
        {
        }
    }
}
