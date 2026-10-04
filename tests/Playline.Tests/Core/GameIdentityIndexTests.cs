using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.Tests.Core;

public sealed class GameIdentityIndexTests
{
    [Fact]
    public void TryAdd_RejectsEquivalentIdPathOrUri()
    {
        var path = Path.Combine(Path.GetTempPath(), "Playline", "Game.exe");
        var index = new GameIdentityIndex();

        Assert.True(index.TryAdd(new Game
        {
            Id = "first",
            Name = "First",
            ExecutablePath = path,
            LaunchUri = "steam://rungameid/10"
        }));
        Assert.False(index.TryAdd(new Game { Id = "FIRST", Name = "Same ID" }));
        Assert.False(index.TryAdd(new Game
        {
            Id = "second",
            Name = "Same path",
            ExecutablePath = path.ToUpperInvariant()
        }));
        Assert.False(index.TryAdd(new Game
        {
            Id = "third",
            Name = "Same URI",
            LaunchUri = "STEAM://RUNGAMEID/10"
        }));
    }
}
