using Playline.Core.Contracts;
using Playline.Core.Models;

namespace Playline.Tests.TestSupport;

internal sealed class InMemoryGameRepository(IEnumerable<Game>? initialGames = null) : IGameRepository
{
    private IReadOnlyList<Game> _games = initialGames?.ToArray() ?? Array.Empty<Game>();

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Game>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Game>>(_games.ToArray());
    }

    public Task SaveAsync(
        IReadOnlyCollection<Game> games,
        CancellationToken cancellationToken = default)
    {
        _games = games.ToArray();
        SaveCount++;
        return Task.CompletedTask;
    }
}

