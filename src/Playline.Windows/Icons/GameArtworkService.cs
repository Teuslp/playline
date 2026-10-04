using System.IO;
using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.Windows.Icons;

public sealed class GameArtworkService(
    IconCacheService iconCache,
    IEnumerable<IGameArtworkLocator> artworkLocators)
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".ico", ".jpeg", ".jpg", ".png", ".tif", ".tiff"
    };

    private readonly IGameArtworkLocator[] _artworkLocators = artworkLocators.ToArray();

    public async Task<string?> GetOrCreateAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        var hasExistingArtwork = IsUsableImage(game.IconPath);
        if (hasExistingArtwork && !iconCache.IsManagedCachePath(game.IconPath))
        {
            return Path.GetFullPath(game.IconPath!);
        }

        foreach (var locator in _artworkLocators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var locatedArtwork = locator.Locate(game);
            if (IsUsableImage(locatedArtwork))
            {
                return Path.GetFullPath(locatedArtwork!);
            }
        }

        if (!string.IsNullOrWhiteSpace(game.ExecutablePath) && File.Exists(game.ExecutablePath))
        {
            var extractedIcon = await iconCache
                .GetOrCreateAsync(
                    $"{game.Id}|{GameIdentity.NormalizePath(game.ExecutablePath)}",
                    game.ExecutablePath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (IsUsableImage(extractedIcon))
            {
                return extractedIcon;
            }
        }

        if (hasExistingArtwork)
        {
            return Path.GetFullPath(game.IconPath!);
        }

        return null;
    }

    private static bool IsUsableImage(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && SupportedImageExtensions.Contains(Path.GetExtension(path))
        && File.Exists(path);
}
