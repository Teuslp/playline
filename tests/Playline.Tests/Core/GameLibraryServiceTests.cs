using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Core;

public sealed class GameLibraryServiceTests
{
    [Fact]
    public async Task AddGameAsync_WhenIdAlreadyExists_DoesNotPersistDuplicate()
    {
        var game = new Game { Name = "Existing game" };
        var repository = new InMemoryGameRepository([game]);
        var service = new GameLibraryService(repository);
        await service.InitializeAsync();

        var added = await service.AddGameAsync(game with { Name = "Duplicate" });

        Assert.False(added);
        Assert.Single(service.GetGames());
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AddAndRemoveGameAsync_PersistsEveryChange()
    {
        var repository = new InMemoryGameRepository();
        var service = new GameLibraryService(repository);
        var game = new Game { Name = "Manual game" };
        await service.InitializeAsync();

        var added = await service.AddGameAsync(game);
        var removed = await service.RemoveGameAsync(game.Id);

        Assert.True(added);
        Assert.True(removed);
        Assert.Empty(service.GetGames());
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task InitializeAsync_RemovesDuplicateIdsLoadedFromStorage()
    {
        var game = new Game { Name = "Original" };
        var repository = new InMemoryGameRepository([game, game with { Name = "Duplicate" }]);
        var service = new GameLibraryService(repository);

        await service.InitializeAsync();

        Assert.Equal(game with { SortOrder = 0 }, Assert.Single(service.GetGames()));
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AddGameAsync_WhenExecutablePathAlreadyExists_RejectsDifferentId()
    {
        var path = Path.Combine(Path.GetTempPath(), "Playline", "Duplicate.exe");
        var repository = new InMemoryGameRepository([
            new Game { Id = "first", Name = "First", ExecutablePath = path }
        ]);
        var service = new GameLibraryService(repository);
        await service.InitializeAsync();

        var added = await service.AddGameAsync(new Game
        {
            Id = "second",
            Name = "Second",
            ExecutablePath = path.ToUpperInvariant()
        });

        Assert.False(added);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateGameAsync_PersistsChangesAndPreservesIdentityMetadata()
    {
        var dateAdded = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var game = new Game { Id = "manual-game", Name = "Before", DateAdded = dateAdded };
        var repository = new InMemoryGameRepository([game]);
        var service = new GameLibraryService(repository);
        await service.InitializeAsync();

        var updated = await service.UpdateGameAsync(game with
        {
            Id = "manual-game",
            Name = "After",
            DateAdded = DateTimeOffset.UtcNow,
            Arguments = "--safe-mode"
        });

        var savedGame = Assert.Single(service.GetGames());
        Assert.True(updated);
        Assert.Equal("After", savedGame.Name);
        Assert.Equal("--safe-mode", savedGame.Arguments);
        Assert.Equal(dateAdded, savedGame.DateAdded);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_PersistsWithoutChangingOrder()
    {
        var games = new[]
        {
            new Game { Id = "one", Name = "One", SortOrder = 0 },
            new Game { Id = "two", Name = "Two", SortOrder = 1 }
        };
        var repository = new InMemoryGameRepository(games);
        var service = new GameLibraryService(repository);
        await service.InitializeAsync();

        var changed = await service.ToggleFavoriteAsync("two");

        Assert.True(changed);
        Assert.Equal(new[] { "one", "two" }, service.GetGames().Select(game => game.Id));
        Assert.True(service.GetGames()[1].IsFavorite);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateIconPathsAsync_UpdatesSeveralGamesWithOneSave()
    {
        var repository = new InMemoryGameRepository(
        [
            new Game { Id = "first", Name = "First", SortOrder = 0 },
            new Game { Id = "second", Name = "Second", SortOrder = 1 }
        ]);
        var service = new GameLibraryService(repository);
        await service.InitializeAsync();
        var savesBeforeUpdate = repository.SaveCount;

        var changed = await service.UpdateIconPathsAsync(new Dictionary<string, string>
        {
            ["first"] = @"C:\Cache\first.png",
            ["second"] = @"C:\Cache\second.jpg"
        });

        Assert.True(changed);
        Assert.Equal(savesBeforeUpdate + 1, repository.SaveCount);
        Assert.Equal(@"C:\Cache\first.png", service.GetGames()[0].IconPath);
        Assert.Equal(@"C:\Cache\second.jpg", service.GetGames()[1].IconPath);
    }

    [Fact]
    public async Task MoveGameAsync_ReordersAndNormalizesSortOrder()
    {
        var games = new[]
        {
            new Game { Id = "one", Name = "One", SortOrder = 0 },
            new Game { Id = "two", Name = "Two", SortOrder = 1 },
            new Game { Id = "three", Name = "Three", SortOrder = 2 }
        };
        var repository = new InMemoryGameRepository(games);
        var service = new GameLibraryService(repository);
        await service.InitializeAsync();

        var changed = await service.MoveGameAsync("three", "one", insertAfter: false);

        Assert.True(changed);
        Assert.Equal(new[] { "three", "one", "two" }, service.GetGames().Select(game => game.Id));
        Assert.Equal(new[] { 0, 1, 2 }, service.GetGames().Select(game => game.SortOrder));
        Assert.Equal(1, repository.SaveCount);
    }
}
