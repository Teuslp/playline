using Playline.Core.Models;
using Playline.Discovery;
using Playline.Discovery.Contracts;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Discovery;

public sealed class GameDiscoveryServiceTests
{
    [Fact]
    public async Task ScanAsync_WhenOneScannerFails_ContinuesAndLogsOnce()
    {
        var game = new Game
        {
            Id = "steam-730",
            Name = "Counter-Strike 2",
            LaunchUri = "steam://rungameid/730",
            Source = GameSource.Steam
        };
        var logger = new RecordingLogger();
        var service = new GameDiscoveryService(
            [new ThrowingScanner(), new FixedScanner([game, game])],
            logger);

        var games = await service.ScanAsync();

        Assert.Equal(game, Assert.Single(games));
        Assert.Single(logger.Entries);
        Assert.Contains("Broken", logger.Entries[0].Message, StringComparison.Ordinal);
    }

    private sealed class ThrowingScanner : IGameScanner
    {
        public string Name => "Broken";

        public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
        {
            throw new IOException("Simulated scanner failure.");
        }
    }

    private sealed class FixedScanner(IReadOnlyList<Game> games) : IGameScanner
    {
        public string Name => "Fixed";

        public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(games);
        }
    }
}
