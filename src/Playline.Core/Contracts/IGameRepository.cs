using Playline.Core.Models;

namespace Playline.Core.Contracts;

public interface IGameRepository
{
    Task<IReadOnlyList<Game>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyCollection<Game> games,
        CancellationToken cancellationToken = default);
}

