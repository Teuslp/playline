using Playline.Core.Models;

namespace Playline.Core.Services;

public sealed class GameIdentityIndex
{
    private readonly HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _executablePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _launchUris = new(StringComparer.OrdinalIgnoreCase);

    public GameIdentityIndex()
    {
    }

    public GameIdentityIndex(IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        foreach (var game in games)
        {
            _ = TryAdd(game);
        }
    }

    public bool Contains(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return GetId(game) is { } id && _ids.Contains(id)
            || GetExecutablePath(game) is { } path && _executablePaths.Contains(path)
            || GetLaunchUri(game) is { } uri && _launchUris.Contains(uri);
    }

    public bool TryAdd(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (Contains(game))
        {
            return false;
        }

        if (GetId(game) is { } id)
        {
            _ids.Add(id);
        }

        if (GetExecutablePath(game) is { } path)
        {
            _executablePaths.Add(path);
        }

        if (GetLaunchUri(game) is { } uri)
        {
            _launchUris.Add(uri);
        }

        return true;
    }

    private static string? GetId(Game game) =>
        string.IsNullOrWhiteSpace(game.Id) ? null : game.Id;

    private static string? GetExecutablePath(Game game) =>
        GameIdentity.NormalizePath(game.ExecutablePath);

    private static string? GetLaunchUri(Game game) =>
        string.IsNullOrWhiteSpace(game.LaunchUri) ? null : game.LaunchUri;
}
