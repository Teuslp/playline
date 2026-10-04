using Playline.Core.Contracts;
using Playline.Core.Models;

namespace Playline.Discovery.Steam;

public sealed class SteamArtworkLocator(ISteamInstallationLocator installationLocator) : IGameArtworkLocator
{
    public string? Locate(Game game)
    {
        if (game.Source != GameSource.Steam || !TryGetAppId(game, out var appId))
        {
            return null;
        }

        foreach (var steamDirectory in installationLocator.LocateInstallations())
        {
            var artwork = LocateInInstallation(steamDirectory, appId);
            if (artwork is not null)
            {
                return artwork;
            }
        }

        return null;
    }

    private static string? LocateInInstallation(string steamDirectory, string appId)
    {
        try
        {
            var cacheDirectory = Path.Combine(steamDirectory, "appcache", "librarycache", appId);
            if (!Directory.Exists(cacheDirectory))
            {
                return null;
            }

            var squareIcon = Directory
                .EnumerateFiles(cacheDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => IsContentHash(Path.GetFileNameWithoutExtension(path)));
            if (squareIcon is not null)
            {
                return squareIcon;
            }

            foreach (var fileName in new[] { "library_600x900.jpg", "header.jpg", "logo.png" })
            {
                var candidate = Path.Combine(cacheDirectory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Directory
                .EnumerateFiles(cacheDirectory, "library_capsule.jpg", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryGetAppId(Game game, out string appId)
    {
        const string idPrefix = "steam-";
        appId = game.Id.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase)
            ? game.Id[idPrefix.Length..]
            : string.Empty;

        if (appId.Length > 0 && appId.All(char.IsAsciiDigit))
        {
            return true;
        }

        const string uriPrefix = "steam://rungameid/";
        var launchUri = game.LaunchUri?.Trim();
        appId = launchUri is not null && launchUri.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase)
            ? launchUri[uriPrefix.Length..]
            : string.Empty;
        return appId.Length > 0 && appId.All(char.IsAsciiDigit);
    }

    private static bool IsContentHash(string value) =>
        value.Length == 40 && value.All(Uri.IsHexDigit);
}
