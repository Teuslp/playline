using Playline.Core.Contracts;
using Playline.Core.Models;

namespace Playline.Core.Services;

public sealed class GameLibraryService(IGameRepository repository)
{
    private IReadOnlyList<Game> _games = Array.Empty<Game>();
    private bool _isInitialized;

    public IReadOnlyList<Game> GetGames() => _games;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var loadedGames = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);

        var uniqueGames = new List<Game>();
        var identityIndex = new GameIdentityIndex();
        foreach (var game in loadedGames
                     .Select((game, index) => new { Game = game, Index = index })
                     .OrderBy(item => item.Game.SortOrder)
                     .ThenBy(item => item.Index)
                     .Select(item => item.Game))
        {
            if (identityIndex.TryAdd(game))
            {
                uniqueGames.Add(game);
            }
        }

        var normalizedGames = NormalizeOrder(uniqueGames);
        if (!loadedGames.SequenceEqual(normalizedGames))
        {
            await repository.SaveAsync(normalizedGames, cancellationToken).ConfigureAwait(false);
        }

        _games = normalizedGames;
        _isInitialized = true;
    }

    public async Task<bool> AddGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(game);

        if (new GameIdentityIndex(_games).Contains(game))
        {
            return false;
        }

        var gameToAdd = game with { SortOrder = _games.Count };
        var updatedGames = _games.Append(gameToAdd).ToArray();
        await repository.SaveAsync(updatedGames, cancellationToken).ConfigureAwait(false);
        _games = updatedGames;

        return true;
    }

    public async Task<bool> UpdateGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(game);

        var index = -1;
        for (var currentIndex = 0; currentIndex < _games.Count; currentIndex++)
        {
            if (string.Equals(_games[currentIndex].Id, game.Id, StringComparison.OrdinalIgnoreCase))
            {
                index = currentIndex;
                break;
            }
        }

        if (index < 0
            || new GameIdentityIndex(_games.Where((_, currentIndex) => currentIndex != index))
                .Contains(game))
        {
            return false;
        }

        var existingGame = _games[index];
        var updatedGame = game with
        {
            Id = existingGame.Id,
            DateAdded = existingGame.DateAdded,
            SortOrder = existingGame.SortOrder
        };
        var updatedGames = _games.ToArray();
        updatedGames[index] = updatedGame;

        await repository.SaveAsync(updatedGames, cancellationToken).ConfigureAwait(false);
        _games = updatedGames;

        return true;
    }

    public async Task<bool> ToggleFavoriteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var index = FindIndex(id);
        if (index < 0)
        {
            return false;
        }

        var updatedGames = _games.ToArray();
        updatedGames[index] = updatedGames[index] with
        {
            IsFavorite = !updatedGames[index].IsFavorite
        };

        await repository.SaveAsync(updatedGames, cancellationToken).ConfigureAwait(false);
        _games = updatedGames;

        return true;
    }

    public async Task<bool> UpdateIconPathsAsync(
        IReadOnlyDictionary<string, string> iconPaths,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(iconPaths);

        var updatedGames = _games.ToArray();
        var changed = false;
        for (var index = 0; index < updatedGames.Length; index++)
        {
            var game = updatedGames[index];
            if (!iconPaths.TryGetValue(game.Id, out var iconPath)
                || string.IsNullOrWhiteSpace(iconPath)
                || string.Equals(game.IconPath, iconPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            updatedGames[index] = game with { IconPath = iconPath };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        await repository.SaveAsync(updatedGames, cancellationToken).ConfigureAwait(false);
        _games = updatedGames;
        return true;
    }

    public async Task<bool> MoveGameAsync(
        string gameId,
        string targetGameId,
        bool insertAfter,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetGameId);

        if (string.Equals(gameId, targetGameId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var games = _games.ToList();
        var sourceIndex = FindIndex(gameId);
        var targetIndex = FindIndex(targetGameId);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        var game = games[sourceIndex];
        games.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        if (insertAfter)
        {
            targetIndex++;
        }

        games.Insert(Math.Clamp(targetIndex, 0, games.Count), game);
        var updatedGames = NormalizeOrder(games);
        if (_games.SequenceEqual(updatedGames))
        {
            return false;
        }

        await repository.SaveAsync(updatedGames, cancellationToken).ConfigureAwait(false);
        _games = updatedGames;

        return true;
    }

    public async Task<bool> RemoveGameAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var updatedGames = NormalizeOrder(_games
            .Where(game => !string.Equals(game.Id, id, StringComparison.OrdinalIgnoreCase)));
        if (updatedGames.Length == _games.Count)
        {
            return false;
        }

        await repository.SaveAsync(updatedGames, cancellationToken).ConfigureAwait(false);
        _games = updatedGames;

        return true;
    }

    private int FindIndex(string id)
    {
        for (var index = 0; index < _games.Count; index++)
        {
            if (string.Equals(_games[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static Game[] NormalizeOrder(IEnumerable<Game> games)
    {
        return games
            .Select((game, index) => game.SortOrder == index
                ? game
                : game with { SortOrder = index })
            .ToArray();
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The game library has not been initialized.");
        }
    }
}
