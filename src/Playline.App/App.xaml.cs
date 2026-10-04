using System.Windows;
using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery;
using Playline.Discovery.Contracts;
using Playline.Discovery.Epic;
using Playline.Discovery.Steam;
using Playline.Storage.Games;
using Playline.Storage.Logging;
using Playline.Storage.Paths;
using Playline.Storage.Settings;
using Playline.Windows.Games;
using Playline.Windows.Icons;
using Playline.Windows.Launching;
using Playline.Windows.Shortcuts;
using Playline.Windows.Startup;

namespace Playline.App;

public partial class App : Application
{
    private ICriticalErrorLogger? _logger;
    private TrayIconService? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = AppDataPaths.CreateDefault();
            var usesIsolatedDataDirectory = false;
            var isolatedDataDirectory = Environment.GetEnvironmentVariable("PLAYLINE_DATA_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(isolatedDataDirectory))
            {
                paths = new AppDataPaths(isolatedDataDirectory);
                usesIsolatedDataDirectory = true;
            }
            paths.EnsureCreated();

            _logger = new CriticalFileLogger(paths.LogsDirectory);
            var settingsRepository = new JsonSettingsRepository(paths, _logger);
            var settings = await settingsRepository.LoadAsync();
            var processPath = Environment.ProcessPath
                ?? throw new InvalidOperationException("The Playline executable path is unavailable.");
            IStartupRegistrationService startupService = new StartupRegistrationService(processPath);
            if (usesIsolatedDataDirectory
                && !string.Equals(
                    Environment.GetEnvironmentVariable("PLAYLINE_ENABLE_STARTUP_REGISTRY"),
                    "1",
                    StringComparison.Ordinal))
            {
                startupService = new SessionStartupRegistrationService(settings.StartWithWindows);
            }
            try
            {
                startupService.SetEnabled(settings.StartWithWindows);
            }
            catch (Exception exception)
            {
                await _logger.LogAsync("The Windows startup preference could not be synchronized.", exception);
            }

            var gameRepository = new JsonGameRepository(paths, _logger);
            var gameLibrary = new GameLibraryService(gameRepository);
            await gameLibrary.InitializeAsync();

            var iconCache = new IconCacheService(paths.IconsDirectory);
            var steamInstallationLocator = new SteamInstallationLocator();
            var gameArtwork = new GameArtworkService(
                iconCache,
                [new SteamArtworkLocator(steamInstallationLocator)]);
            var manualGameService = new ManualGameService(
                new ShellLinkShortcutResolver(),
                iconCache);
            var gameLauncher = new GameLauncherService(_logger);
            IGameScanner[] scanners =
            [
                new SteamGameScanner(steamInstallationLocator),
                new EpicGameScanner(new EpicManifestLocator())
            ];
            var discoveryService = new GameDiscoveryService(scanners, _logger);

            IReadOnlyList<Game>? developmentGames = null;
#if DEBUG
            developmentGames = DevelopmentGameData.FromArguments(e.Args);
#endif

            var window = new MainWindow(
                gameLibrary,
                manualGameService,
                gameLauncher,
                gameArtwork,
                discoveryService,
                settingsRepository,
                startupService,
                _logger,
                settings,
                developmentGames);

            MainWindow = window;
            window.Show();
            _trayIcon = new TrayIconService(
                Dispatcher,
                window.ShowFromTray,
                window.OpenDiscoveryFromTray,
                window.OpenSettingsFromTray,
                window.ExitApplication);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception exception)
        {
            if (_logger is not null)
            {
                await _logger.LogAsync("Playline startup failed.", exception);
            }

            MessageBox.Show(
                "Não foi possível inicializar o Playline.",
                "Playline",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }

    private sealed class SessionStartupRegistrationService(bool enabled) : IStartupRegistrationService
    {
        private bool _enabled = enabled;

        public bool IsEnabled() => _enabled;

        public void SetEnabled(bool enabledValue) => _enabled = enabledValue;
    }
}
