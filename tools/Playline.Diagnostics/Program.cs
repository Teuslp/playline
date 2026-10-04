using System.Diagnostics;
using System.Text.Json;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery.Epic;
using Playline.Discovery.Steam;
using Playline.Storage.Games;
using Playline.Storage.Logging;
using Playline.Storage.Paths;
using Playline.Storage.Settings;
using Playline.Windows.Launching;

var prepareRoot = args
    .FirstOrDefault(argument => argument.StartsWith("--prepare-root=", StringComparison.OrdinalIgnoreCase))?
    .Split('=', 2)[1];

if (!string.IsNullOrWhiteSpace(prepareRoot))
{
    var countArgument = args
        .FirstOrDefault(argument => argument.StartsWith("--games=", StringComparison.OrdinalIgnoreCase))?
        .Split('=', 2)[1];

    if (!int.TryParse(countArgument, out var count) || count < 0)
    {
        throw new ArgumentException("Use --games=N with a non-negative integer.");
    }

    var preparedPaths = new AppDataPaths(Path.GetFullPath(prepareRoot));
    var preparedLogger = new CriticalFileLogger(preparedPaths.LogsDirectory);
    await new JsonGameRepository(preparedPaths, preparedLogger).SaveAsync(CreateDiagnosticGames(preparedPaths.RootDirectory, count));
    await new JsonSettingsRepository(preparedPaths, preparedLogger).SaveAsync(new AppSettings());

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        root = preparedPaths.RootDirectory,
        games = count,
        preparedPaths.GamesFile,
        preparedPaths.SettingsFile
    }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var gameCounts = new[] { 0, 1, 10, 50, 100, 250 };
var libraryResults = new List<object>();

foreach (var gameCount in gameCounts)
{
    var root = Path.Combine(Path.GetTempPath(), "Playline-Diagnostics", Guid.NewGuid().ToString("N"));
    var paths = new AppDataPaths(root);
    var logger = new CriticalFileLogger(paths.LogsDirectory);
    var repository = new JsonGameRepository(paths, logger);
    var games = CreateDiagnosticGames(root, gameCount);

    await repository.SaveAsync(games);

    var samples = new List<double>();
    for (var run = 0; run < 10; run++)
    {
        var service = new GameLibraryService(repository);
        var stopwatch = Stopwatch.StartNew();
        await service.InitializeAsync();
        stopwatch.Stop();
        samples.Add(stopwatch.Elapsed.TotalMilliseconds);
    }

    libraryResults.Add(new
    {
        games = gameCount,
        meanMilliseconds = Math.Round(samples.Average(), 3),
        minimumMilliseconds = Math.Round(samples.Min(), 3),
        jsonBytes = new FileInfo(paths.GamesFile).Length
    });

    Directory.Delete(root, recursive: true);
}

var steamStopwatch = Stopwatch.StartNew();
var steamGames = await new SteamGameScanner(new SteamInstallationLocator()).ScanAsync();
steamStopwatch.Stop();

var epicStopwatch = Stopwatch.StartNew();
var epicGames = await new EpicGameScanner(new EpicManifestLocator()).ScanAsync();
epicStopwatch.Stop();

var launchRoot = Path.Combine(Path.GetTempPath(), "Playline-Diagnostics", Guid.NewGuid().ToString("N"));
var launchLogger = new CriticalFileLogger(Path.Combine(launchRoot, "logs"));
var launcher = new GameLauncherService(launchLogger);
var commandPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
var launchSamples = new List<double>();

for (var run = 0; run < 5; run++)
{
    var stopwatch = Stopwatch.StartNew();
    var result = await launcher.LaunchAsync(new Game
    {
        Name = "Diagnostics command",
        ExecutablePath = commandPath,
        Arguments = "/d /c exit 0",
        WorkingDirectory = Path.GetDirectoryName(commandPath),
        Source = GameSource.Manual
    });
    stopwatch.Stop();

    if (!result.Success)
    {
        throw new InvalidOperationException(result.ErrorMessage);
    }

    launchSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
}

if (Directory.Exists(launchRoot))
{
    Directory.Delete(launchRoot, recursive: true);
}

var report = new
{
    timestampUtc = DateTimeOffset.UtcNow,
    library = libraryResults,
    steam = new
    {
        milliseconds = Math.Round(steamStopwatch.Elapsed.TotalMilliseconds, 3),
        games = steamGames.Count
    },
    epic = new
    {
        milliseconds = Math.Round(epicStopwatch.Elapsed.TotalMilliseconds, 3),
        games = epicGames.Count
    },
    launch = new
    {
        meanMilliseconds = Math.Round(launchSamples.Average(), 3),
        minimumMilliseconds = Math.Round(launchSamples.Min(), 3)
    }
};

Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

static Game[] CreateDiagnosticGames(string root, int count) =>
    Enumerable.Range(0, count)
        .Select(index => new Game
        {
            Id = $"diagnostic-{index}",
            Name = $"Diagnostic Game {index + 1}",
            ExecutablePath = Path.Combine(root, $"Game {index + 1}.exe"),
            Source = GameSource.Manual,
            SortOrder = index
        })
        .ToArray();
