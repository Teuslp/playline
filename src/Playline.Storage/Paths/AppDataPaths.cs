namespace Playline.Storage.Paths;

public sealed class AppDataPaths
{
    public AppDataPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
        GamesFile = Path.Combine(RootDirectory, "games.json");
        SettingsFile = Path.Combine(RootDirectory, "settings.json");
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        IconsDirectory = Path.Combine(CacheDirectory, "icons");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
    }

    public string RootDirectory { get; }

    public string GamesFile { get; }

    public string SettingsFile { get; }

    public string CacheDirectory { get; }

    public string IconsDirectory { get; }

    public string LogsDirectory { get; }

    public static AppDataPaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return new AppDataPaths(Path.Combine(localAppData, "Playline"));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(IconsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
