using System.Text.Json;
using Playline.Core.Models;
using Playline.Storage.Games;
using Playline.Storage.Paths;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Storage;

public sealed class JsonGameRepositoryTests
{
    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_CreatesEmptyLibrary()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var repository = new JsonGameRepository(paths, new RecordingLogger());

        var games = await repository.LoadAsync();

        Assert.Empty(games);
        Assert.True(File.Exists(paths.GamesFile));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(paths.GamesFile));
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("games").ValueKind);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsEmpty_RepairsFileAndReturnsEmptyLibrary()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.GamesFile, string.Empty);
        var repository = new JsonGameRepository(paths, new RecordingLogger());

        var games = await repository.LoadAsync();

        Assert.Empty(games);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(paths.GamesFile));
        Assert.Empty(document.RootElement.GetProperty("games").EnumerateArray());
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsInvalid_ReturnsEmptyLibraryAndLogsError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.GamesFile, "{ invalid json");
        var logger = new RecordingLogger();
        var repository = new JsonGameRepository(paths, logger);

        var games = await repository.LoadAsync();

        Assert.Empty(games);
        Assert.Single(logger.Entries);
        Assert.IsType<JsonException>(logger.Entries[0].Exception);
        using var repairedDocument = JsonDocument.Parse(await File.ReadAllTextAsync(paths.GamesFile));
        Assert.Empty(repairedDocument.RootElement.GetProperty("games").EnumerateArray());
        Assert.Single(Directory.GetFiles(paths.RootDirectory, "games.json.corrupt-*.bak"));
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesGameData()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var repository = new JsonGameRepository(paths, new RecordingLogger());
        var game = new Game
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Counter-Strike 2",
            Arguments = "-novid",
            WorkingDirectory = @"C:\Games\Counter-Strike 2",
            LaunchUri = "steam://rungameid/730",
            Source = GameSource.Steam,
            DateAdded = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)
        };

        await repository.SaveAsync([game]);
        var loadedGames = await repository.LoadAsync();

        Assert.Equal(game, Assert.Single(loadedGames));
        var json = await File.ReadAllTextAsync(paths.GamesFile);
        Assert.Contains("\"source\": \"Steam\"", json, StringComparison.Ordinal);
    }
}
