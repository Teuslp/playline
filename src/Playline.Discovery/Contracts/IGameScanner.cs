using Playline.Core.Models;

namespace Playline.Discovery.Contracts;

public interface IGameScanner
{
    string Name { get; }

    Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default);
}

