using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery.Contracts;

namespace Playline.Discovery;

public sealed class GameDiscoveryService(
    IEnumerable<IGameScanner> scanners,
    ICriticalErrorLogger logger)
{
    private readonly IReadOnlyList<IGameScanner> _scanners = scanners.ToArray();

    public async Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var discoveredGames = new List<Game>();
        var identityIndex = new GameIdentityIndex();

        foreach (var scanner in _scanners)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var scannerGames = await scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
                foreach (var game in scannerGames)
                {
                    if (identityIndex.TryAdd(game))
                    {
                        discoveredGames.Add(game);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await logger.LogAsync(
                        $"The {scanner.Name} game scan failed.",
                        exception,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return discoveredGames;
    }
}
