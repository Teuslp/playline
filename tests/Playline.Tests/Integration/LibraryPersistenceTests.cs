using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Storage.Games;
using Playline.Storage.Paths;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Integration;

public sealed class LibraryPersistenceTests
{
    [Fact]
    public async Task TwoInitializations_ReloadPersistedLibrary()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var logger = new RecordingLogger();

        var firstRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await firstRun.InitializeAsync();
        var game = new Game
        {
            Name = "Persisted game",
            ExecutablePath = @"C:\Games\PersistedGame.exe"
        };
        await firstRun.AddGameAsync(game);

        var secondRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await secondRun.InitializeAsync();

        Assert.Equal(game with { SortOrder = 0 }, Assert.Single(secondRun.GetGames()));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task EditAndRemove_PersistAcrossIndependentServiceInstances()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var logger = new RecordingLogger();
        var firstRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await firstRun.InitializeAsync();
        var game = new Game { Id = "manual-edit", Name = "Before" };
        await firstRun.AddGameAsync(game);
        await firstRun.UpdateGameAsync(game with { Name = "After", Arguments = "--edited" });

        var secondRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await secondRun.InitializeAsync();
        var reloaded = Assert.Single(secondRun.GetGames());
        Assert.Equal("After", reloaded.Name);
        Assert.Equal("--edited", reloaded.Arguments);
        await secondRun.RemoveGameAsync(reloaded.Id);

        var thirdRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await thirdRun.InitializeAsync();
        Assert.Empty(thirdRun.GetGames());
    }

    [Fact]
    public async Task FavoriteAndOrder_PersistAcrossIndependentServiceInstances()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var logger = new RecordingLogger();
        var firstRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await firstRun.InitializeAsync();
        await firstRun.AddGameAsync(new Game { Id = "one", Name = "One" });
        await firstRun.AddGameAsync(new Game { Id = "two", Name = "Two" });
        await firstRun.AddGameAsync(new Game { Id = "three", Name = "Three" });
        await firstRun.ToggleFavoriteAsync("two");
        await firstRun.MoveGameAsync("three", "one", insertAfter: false);

        var secondRun = new GameLibraryService(new JsonGameRepository(paths, logger));
        await secondRun.InitializeAsync();

        Assert.Equal(new[] { "three", "one", "two" }, secondRun.GetGames().Select(game => game.Id));
        Assert.True(secondRun.GetGames().Single(game => game.Id == "two").IsFavorite);
        Assert.Equal(new[] { 0, 1, 2 }, secondRun.GetGames().Select(game => game.SortOrder));
    }
}
