using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.Tests.Core;

public sealed class GameIdentityTests
{
    [Fact]
    public void CreateManualId_NormalizesPathCaseAndSegments()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "Playline", "Games");
        var firstPath = Path.Combine(basePath, "Folder", "..", "Game.exe");
        var secondPath = Path.Combine(basePath.ToUpperInvariant(), "GAME.EXE");

        var firstId = GameIdentity.CreateManualId(firstPath);
        var secondId = GameIdentity.CreateManualId(secondPath);

        Assert.Equal(firstId, secondId);
        Assert.StartsWith("manual-", firstId, StringComparison.Ordinal);
    }

    [Fact]
    public void AreEquivalent_RecognizesSameExecutableWithDifferentIds()
    {
        var path = Path.Combine(Path.GetTempPath(), "Playline", "Game.exe");
        var first = new Game { Id = "first", Name = "First", ExecutablePath = path };
        var second = new Game { Id = "second", Name = "Second", ExecutablePath = path.ToUpperInvariant() };

        Assert.True(GameIdentity.AreEquivalent(first, second));
    }

    [Fact]
    public void CreateLauncherId_ProducesStableReadableId()
    {
        Assert.Equal("steam-730", GameIdentity.CreateLauncherId("Steam", "730"));
        Assert.Equal("epic-artifact-id", GameIdentity.CreateLauncherId("Epic", "Artifact-ID"));
    }
}

