using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Playline.App.Controls;
using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery;
using Playline.Windows.Games;
using Playline.Windows.Icons;
using Playline.Windows.Launching;
using Playline.Windows.Startup;
using Playline.Windows.Windowing;
using Forms = System.Windows.Forms;

namespace Playline.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double WheelScrollStep = 72;
    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromMilliseconds(550);
    private static readonly TimeSpan StatusDuration = TimeSpan.FromSeconds(3.5);

    private readonly GameLibraryService _gameLibrary;
    private readonly ManualGameService _manualGameService;
    private readonly GameLauncherService _gameLauncher;
    private readonly GameArtworkService _gameArtwork;
    private readonly GameDiscoveryService _gameDiscovery;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IStartupRegistrationService _startupService;
    private readonly ICriticalErrorLogger _logger;
    private IReadOnlyList<Game>? _developmentGames;
    private AppSettings _settings;
    private DispatcherTimer? _autoHideTimer;
    private DispatcherTimer? _statusTimer;
    private bool _dialogOpen;
    private bool _suppressAutoHide;
    private bool _positionNeedsSave;
    private Point? _barMoveStart;
    private bool _suppressMenuClick;

    public MainWindow(
        GameLibraryService gameLibrary,
        ManualGameService manualGameService,
        GameLauncherService gameLauncher,
        GameArtworkService gameArtwork,
        GameDiscoveryService gameDiscovery,
        ISettingsRepository settingsRepository,
        IStartupRegistrationService startupService,
        ICriticalErrorLogger logger,
        AppSettings settings,
        IReadOnlyList<Game>? developmentGames = null)
    {
        InitializeComponent();

        _gameLibrary = gameLibrary;
        _manualGameService = manualGameService;
        _gameLauncher = gameLauncher;
        _gameArtwork = gameArtwork;
        _gameDiscovery = gameDiscovery;
        _settingsRepository = settingsRepository;
        _startupService = startupService;
        _logger = logger;
        _settings = settings.Normalize();
        _developmentGames = developmentGames;

        ApplySettings(restoreSavedPosition: true);
        RefreshGames();

        Loaded += MainWindow_Loaded;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameDisplayMode DisplayMode => _settings.DisplayMode;

    public GameItemSize ItemSize => _settings.ItemSize;

    public BarOrientation BarOrientation => _settings.BarOrientation;

    public BarTheme BarTheme => _settings.BarTheme;

    public void ShowFromTray()
    {
        CancelAutoHide();
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }

    public void OpenDiscoveryFromTray()
    {
        ShowFromTray();
        DiscoverGames_Click(this, new RoutedEventArgs());
    }

    public void OpenSettingsFromTray()
    {
        ShowFromTray();
        SettingsMenuItem_Click(this, new RoutedEventArgs());
    }

    public void ExitApplication() => Application.Current.Shutdown();

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        PlaceAtDesktopLevel();
        if (_positionNeedsSave)
        {
            await PersistWindowPositionAsync();
        }

        try
        {
            await EnsureGameArtworkAsync();
        }
        catch (Exception exception)
        {
            await _logger.LogAsync("The game artwork could not be refreshed.", exception);
        }
    }

    private async void AddGame_Click(object sender, RoutedEventArgs e)
    {
        if (!BeginModalOperation())
        {
            return;
        }

        string? selectedFile = null;
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Procurar jogo no computador",
                Filter = "Executáveis e atalhos (*.exe;*.lnk)|*.exe;*.lnk|Executáveis (*.exe)|*.exe|Atalhos (*.lnk)|*.lnk",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                selectedFile = dialog.FileName;
            }
        }
        finally
        {
            EndModalOperation();
        }

        if (selectedFile is null)
        {
            return;
        }

        try
        {
            var game = await _manualGameService.CreateAsync(selectedFile);
            if (!await _gameLibrary.AddGameAsync(game))
            {
                ShowStatus("Esse jogo já está na biblioteca.");
                return;
            }

            _developmentGames = null;
            RefreshGames();
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The selected game could not be added.", exception);
        }
    }

    private async void DiscoverGames_Click(object sender, RoutedEventArgs e)
    {
        if (!BeginModalOperation())
        {
            return;
        }

        DiscoveryWindow dialog;
        bool accepted;
        try
        {
            dialog = new DiscoveryWindow(_gameDiscovery, _gameArtwork, _gameLibrary.GetGames())
            {
                Owner = this
            };
            accepted = dialog.ShowDialog() == true;
        }
        finally
        {
            EndModalOperation();
        }

        if (!accepted)
        {
            return;
        }

        try
        {
            foreach (var discoveredGame in dialog.SelectedGames)
            {
                var game = discoveredGame;
                if (string.IsNullOrWhiteSpace(game.IconPath) || !File.Exists(game.IconPath))
                {
                    var iconPath = await _gameArtwork.GetOrCreateAsync(game);
                    game = game with { IconPath = iconPath };
                }

                _ = await _gameLibrary.AddGameAsync(game);
            }

            _developmentGames = null;
            RefreshGames();
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The discovered games could not be added.", exception);
        }
    }

    private async void GameItem_OpenRequested(object? sender, GameActionEventArgs e)
    {
        var result = await _gameLauncher.LaunchAsync(e.Game);
        if (!result.Success)
        {
            ShowStatus(result.ErrorMessage ?? "Não foi possível abrir o jogo.", isError: true);
            return;
        }

        switch (AfterLaunchPolicy.Resolve(result.Success, _settings.AfterLaunchAction))
        {
            case AfterLaunchAction.Minimize:
                WindowState = WindowState.Minimized;
                break;
            case AfterLaunchAction.Hide:
                HideToTray();
                break;
            case AfterLaunchAction.Exit:
                ExitApplication();
                break;
        }
    }

    private async void GameItem_EditRequested(object? sender, GameActionEventArgs e)
    {
        if (!BeginModalOperation())
        {
            return;
        }

        EditGameWindow dialog;
        bool accepted;
        try
        {
            dialog = new EditGameWindow(e.Game)
            {
                Owner = this
            };
            accepted = dialog.ShowDialog() == true;
        }
        finally
        {
            EndModalOperation();
        }

        if (!accepted)
        {
            return;
        }

        try
        {
            var updatedGame = dialog.EditedGame;
            var pathChanged = !string.Equals(
                GameIdentity.NormalizePath(e.Game.ExecutablePath),
                GameIdentity.NormalizePath(updatedGame.ExecutablePath),
                StringComparison.OrdinalIgnoreCase);

            var customIconChanged = !string.Equals(
                GameIdentity.NormalizePath(e.Game.IconPath),
                GameIdentity.NormalizePath(updatedGame.IconPath),
                StringComparison.OrdinalIgnoreCase);
            if (pathChanged && !customIconChanged)
            {
                updatedGame = updatedGame with { IconPath = null };
            }

            if (string.IsNullOrWhiteSpace(updatedGame.IconPath) || !File.Exists(updatedGame.IconPath))
            {
                var iconPath = await _gameArtwork.GetOrCreateAsync(updatedGame);
                updatedGame = updatedGame with { IconPath = iconPath };
            }

            if (!await _gameLibrary.UpdateGameAsync(updatedGame))
            {
                ShowStatus("Outro jogo usa o mesmo caminho ou identificador.");
                return;
            }

            _developmentGames = null;
            RefreshGames();
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The game could not be edited.", exception);
        }
    }

    private async void GameItem_FavoriteRequested(object? sender, GameActionEventArgs e)
    {
        try
        {
            if (await _gameLibrary.ToggleFavoriteAsync(e.Game.Id))
            {
                _developmentGames = null;
                RefreshGames();
            }
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The favorite state could not be changed.", exception);
        }
    }

    private async void GameItem_ReorderRequested(object? sender, GameReorderEventArgs e)
    {
        try
        {
            if (await _gameLibrary.MoveGameAsync(e.GameId, e.TargetGameId, e.InsertAfter))
            {
                _developmentGames = null;
                RefreshGames();
            }
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The game order could not be changed.", exception);
        }
    }

    private void GameItem_MoveLeftRequested(object? sender, GameActionEventArgs e)
    {
        MoveGameByOffset(e.Game, -1);
    }

    private void GameItem_MoveRightRequested(object? sender, GameActionEventArgs e)
    {
        MoveGameByOffset(e.Game, 1);
    }

    private async void MoveGameByOffset(Game game, int offset)
    {
        var games = _gameLibrary.GetGames();
        var index = games
            .Select((candidate, candidateIndex) => new { candidate, candidateIndex })
            .FirstOrDefault(item => string.Equals(item.candidate.Id, game.Id, StringComparison.OrdinalIgnoreCase))
            ?.candidateIndex ?? -1;
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= games.Count)
        {
            return;
        }

        try
        {
            var moved = await _gameLibrary.MoveGameAsync(
                game.Id,
                games[targetIndex].Id,
                insertAfter: offset > 0);
            if (moved)
            {
                _developmentGames = null;
                RefreshGames();
            }
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The game order could not be changed.", exception);
        }
    }

    private async void GameItem_RemoveRequested(object? sender, GameActionEventArgs e)
    {
        if (!BeginModalOperation())
        {
            return;
        }

        MessageBoxResult answer;
        try
        {
            answer = MessageBox.Show(
                this,
                $"Remover '{e.Game.Name}' do Playline?\n\nO jogo não será desinstalado.",
                "Playline",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        }
        finally
        {
            EndModalOperation();
        }

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (await _gameLibrary.RemoveGameAsync(e.Game.Id))
            {
                _developmentGames = null;
                RefreshGames();
            }
        }
        catch (Exception exception)
        {
            await ReportOperationErrorAsync("The game could not be removed.", exception);
        }
    }

    private async void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!BeginModalOperation())
        {
            return;
        }

        SettingsWindow dialog;
        bool accepted;
        try
        {
            dialog = new SettingsWindow(_settings)
            {
                Owner = this
            };
            accepted = dialog.ShowDialog() == true;
        }
        finally
        {
            EndModalOperation();
        }

        if (!accepted)
        {
            return;
        }

        var previousSettings = _settings;
        var updatedSettings = dialog.Settings.Normalize();
        if (updatedSettings.RestoreWindowPosition
            && (updatedSettings.WindowX is null || updatedSettings.WindowY is null))
        {
            updatedSettings = updatedSettings with { WindowX = Left, WindowY = Top };
        }

        try
        {
            _startupService.SetEnabled(updatedSettings.StartWithWindows);
            await _settingsRepository.SaveAsync(updatedSettings);
            _settings = updatedSettings;
            ApplySettings(restoreSavedPosition: false);
            await PersistWindowPositionAsync();
            ShowStatus("Configurações salvas.");
        }
        catch (Exception exception)
        {
            try
            {
                _startupService.SetEnabled(previousSettings.StartWithWindows);
            }
            catch
            {
            }

            await ReportOperationErrorAsync("The application settings could not be saved.", exception);
        }
    }

    private void ApplySettings(bool restoreSavedPosition)
    {
        Topmost = false;

        OnPropertyChanged(nameof(DisplayMode));
        OnPropertyChanged(nameof(ItemSize));
        OnPropertyChanged(nameof(BarOrientation));
        OnPropertyChanged(nameof(BarTheme));
        ConfigureBarTheme();
        ConfigureBarOrientation();
        UpdateBarDimensions((_developmentGames ?? _gameLibrary.GetGames()).Count);
        UpdatePositionControls();

        var left = restoreSavedPosition && _settings.RestoreWindowPosition
            ? _settings.WindowX
            : double.IsNaN(Left) ? null : Left;
        var top = restoreSavedPosition && _settings.RestoreWindowPosition
            ? _settings.WindowY
            : double.IsNaN(Top) ? null : Top;
        var position = WindowPositionService.Resolve(
            left,
            top,
            Width,
            Height,
            GetDisplayAreas());

        Left = position.Left;
        Top = position.Top;
        _positionNeedsSave = restoreSavedPosition
            && _settings.RestoreWindowPosition
            && (_settings.WindowX != position.Left || _settings.WindowY != position.Top);

        if (IsLoaded)
        {
            PlaceAtDesktopLevel();
        }
    }

    private static IReadOnlyList<DisplayArea> GetDisplayAreas()
    {
        return Forms.Screen.AllScreens
            .Select(screen => new DisplayArea(
                screen.WorkingArea.Left,
                screen.WorkingArea.Top,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height,
                screen.Primary))
            .ToArray();
    }

    private async Task PersistWindowPositionAsync()
    {
        _positionNeedsSave = false;
        if (!_settings.RestoreWindowPosition
            || !double.IsFinite(Left)
            || !double.IsFinite(Top)
            || (_settings.WindowX == Left && _settings.WindowY == Top))
        {
            return;
        }

        var updatedSettings = _settings with { WindowX = Left, WindowY = Top };
        try
        {
            await _settingsRepository.SaveAsync(updatedSettings);
            _settings = updatedSettings;
        }
        catch (Exception exception)
        {
            await _logger.LogAsync("The Playline window position could not be saved.", exception);
        }
    }

    private void RefreshGames()
    {
        var games = _developmentGames ?? _gameLibrary.GetGames();
        UpdateBarDimensions(games.Count);
        GamesItemsControl.ItemsSource = null;
        GamesItemsControl.ItemsSource = games;
        EmptyStateText.Text = "Nenhum jogo adicionado";
        EmptyStatePanel.Visibility = games.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (IsLoaded)
        {
            KeepBarInsideWorkingArea();
        }
    }

    private void ConfigureBarOrientation()
    {
        var isVertical = _settings.BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical;

        GamesItemsControl.ItemsPanel = (ItemsPanelTemplate)FindResource(
            isVertical ? "VerticalGamesPanel" : "HorizontalGamesPanel");
        GamesScrollViewer.HorizontalScrollBarVisibility = isVertical
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Hidden;
        GamesScrollViewer.VerticalScrollBarVisibility = isVertical
            ? ScrollBarVisibility.Hidden
            : ScrollBarVisibility.Disabled;
        GamesScrollViewer.PanningMode = isVertical
            ? PanningMode.VerticalOnly
            : PanningMode.HorizontalOnly;

        GamesRow.Height = new GridLength(1, GridUnitType.Star);
        MenuRow.Height = isVertical ? GridLength.Auto : new GridLength(0);
        GamesColumn.Width = new GridLength(1, GridUnitType.Star);
        MenuColumn.Width = isVertical ? new GridLength(0) : GridLength.Auto;
        Grid.SetRow(GamesHost, 0);
        Grid.SetColumn(GamesHost, 0);
        Grid.SetRow(MenuButton, isVertical ? 1 : 0);
        Grid.SetColumn(MenuButton, isVertical ? 0 : 1);

        MenuButton.Margin = isVertical
            ? new Thickness(0, 4, 0, 0)
            : new Thickness(0);
        MenuButton.HorizontalAlignment = isVertical
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Stretch;
        MenuButton.VerticalAlignment = VerticalAlignment.Center;

        EmptyStateContent.Orientation = isVertical
            ? Orientation.Vertical
            : Orientation.Horizontal;
        EmptyAddButton.Margin = isVertical
            ? new Thickness(0, 6, 0, 0)
            : new Thickness(8, 0, 0, 0);

        OverflowMenu.Placement = isVertical ? PlacementMode.Right : PlacementMode.Bottom;
        OverflowMenu.HorizontalOffset = isVertical ? 4 : -116;
        OverflowMenu.VerticalOffset = isVertical ? 0 : 4;
    }

    private void ConfigureBarTheme()
    {
        var iconsOnly = _settings.BarTheme == BarTheme.IconsOnly;
        WindowChrome.Margin = iconsOnly ? new Thickness(0) : new Thickness(5);
        WindowChrome.Background = iconsOnly
            ? Brushes.Transparent
            : (Brush)FindResource("PlaylineWindowBrush");
        WindowChrome.BorderBrush = iconsOnly
            ? Brushes.Transparent
            : (Brush)FindResource("PlaylineBorderBrush");
        WindowChrome.BorderThickness = iconsOnly ? new Thickness(0) : new Thickness(1);
        WindowChrome.Effect = iconsOnly
            ? null
            : (System.Windows.Media.Effects.Effect)FindResource("PlaylineWindowShadow");
        GlassEffects.Visibility = iconsOnly ? Visibility.Collapsed : Visibility.Visible;
        LayoutRoot.Margin = iconsOnly ? new Thickness(2) : new Thickness(6, 4, 6, 4);
    }

    private void UpdateBarDimensions(int gameCount)
    {
        var isVertical = _settings.BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical;
        var iconsOnly = _settings.BarTheme == BarTheme.IconsOnly;
        var horizontalFrameSize = iconsOnly ? 4 : 22;
        var verticalFrameSize = iconsOnly ? 4 : 18;
        var itemWidth = (isVertical, _settings.DisplayMode, _settings.ItemSize) switch
        {
            (true, GameDisplayMode.Name, GameItemSize.Small) => 96,
            (true, GameDisplayMode.Name, GameItemSize.Large) => 144,
            (true, GameDisplayMode.Name, _) => 116,
            (true, _, GameItemSize.Small) => 40,
            (true, _, GameItemSize.Large) => 62,
            (true, _, _) => 50,
            (false, GameDisplayMode.Name, GameItemSize.Small) => 110,
            (false, GameDisplayMode.Name, GameItemSize.Large) => 176,
            (false, GameDisplayMode.Name, _) => 142,
            (false, _, GameItemSize.Small) => 56,
            (false, _, GameItemSize.Large) => 94,
            _ => 72
        };
        var itemHeight = (isVertical, _settings.ItemSize) switch
        {
            (true, GameItemSize.Small) => 40,
            (true, GameItemSize.Large) => 62,
            (true, _) => 50,
            (false, GameItemSize.Small) => 40,
            (false, GameItemSize.Large) => 62,
            _ => 50
        };
        var menuHeight = (isVertical, _settings.ItemSize) switch
        {
            (true, GameItemSize.Small) => 30,
            (true, GameItemSize.Large) => 40,
            (true, _) => 34,
            (false, GameItemSize.Small) => 40,
            (false, GameItemSize.Large) => 62,
            _ => 50
        };
        var menuWidth = isVertical
            ? menuHeight
            : _settings.ItemSize switch
            {
                GameItemSize.Small => 56,
                GameItemSize.Large => 94,
                _ => 72
            };
        MenuButton.Width = menuWidth;
        MenuButton.Height = menuHeight;

        MinWidth = 0;
        MinHeight = 0;
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;

        if (isVertical)
        {
            var targetWidth = gameCount == 0 ? 190 : itemWidth + horizontalFrameSize;
            var desiredHeight = gameCount == 0
                ? 122
                : (gameCount * itemHeight) + menuHeight + (iconsOnly ? 8 : 26);
            var minimumHeight = iconsOnly && gameCount > 0 ? 1 : 100;

            Width = targetWidth;
            Height = Math.Clamp(desiredHeight, minimumHeight, 500);
            MinWidth = targetWidth;
            MaxWidth = targetWidth;
            MinHeight = minimumHeight;
            MaxHeight = 500;
            return;
        }

        var targetHeight = itemHeight + verticalFrameSize;
        var desiredWidth = gameCount == 0
            ? 238
            : (gameCount * itemWidth) + menuWidth + horizontalFrameSize;
        var minimumWidth = iconsOnly && gameCount > 0 ? 1 : 190;

        Width = Math.Clamp(desiredWidth, minimumWidth, 600);
        Height = targetHeight;
        MinWidth = minimumWidth;
        MaxWidth = 600;
        MinHeight = targetHeight;
        MaxHeight = targetHeight;
    }

    private void KeepBarInsideWorkingArea()
    {
        var position = WindowPositionService.Resolve(
            double.IsFinite(Left) ? Left : null,
            double.IsFinite(Top) ? Top : null,
            Width,
            Height,
            GetDisplayAreas());
        Left = position.Left;
        Top = position.Top;
    }

    private async Task EnsureGameArtworkAsync()
    {
        if (_developmentGames is not null)
        {
            return;
        }

        var iconUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in _gameLibrary.GetGames())
        {
            var iconPath = await _gameArtwork.GetOrCreateAsync(game);
            if (!string.IsNullOrWhiteSpace(iconPath)
                && !string.Equals(
                    Path.GetFullPath(iconPath),
                    string.IsNullOrWhiteSpace(game.IconPath) ? null : Path.GetFullPath(game.IconPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                iconUpdates[game.Id] = iconPath;
            }
        }

        if (iconUpdates.Count > 0 && await _gameLibrary.UpdateIconPathsAsync(iconUpdates))
        {
            RefreshGames();
        }
    }

    private async Task ReportOperationErrorAsync(string logMessage, Exception exception)
    {
        await _logger.LogAsync(logMessage, exception);
        ShowStatus(exception.Message, isError: true);
    }

    private void ShowStatus(string message, bool isError = false)
    {
        _statusTimer?.Stop();
        StatusText.Text = message;
        StatusBanner.BorderBrush = isError
            ? new SolidColorBrush(Color.FromRgb(215, 139, 57))
            : (Brush)FindResource("PlaylineBorderBrush");
        StatusBanner.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = StatusDuration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            StatusBanner.Visibility = Visibility.Collapsed;
            if (ReferenceEquals(_statusTimer, timer))
            {
                _statusTimer = null;
            }
        };
        _statusTimer = timer;
        timer.Start();
    }

    private bool BeginModalOperation()
    {
        if (_dialogOpen)
        {
            return false;
        }

        _dialogOpen = true;
        _suppressAutoHide = true;
        CancelAutoHide();
        return true;
    }

    private void EndModalOperation()
    {
        _dialogOpen = false;
        _suppressAutoHide = false;
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(PlaceAtDesktopLevel));
    }

    private void MainWindow_Activated(object? sender, EventArgs e) => CancelAutoHide();

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (!_dialogOpen && IsVisible)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(PlaceAtDesktopLevel));
        }

        if (!_settings.AutoHide || _suppressAutoHide || _dialogOpen || HasOpenContextMenu() || !IsVisible)
        {
            return;
        }

        CancelAutoHide();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = AutoHideDelay
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(_autoHideTimer, timer))
            {
                _autoHideTimer = null;
            }

            if (_settings.AutoHide
                && !_suppressAutoHide
                && !_dialogOpen
                && !HasOpenContextMenu()
                && !IsActive)
            {
                HideToTray();
            }
        };
        _autoHideTimer = timer;
        timer.Start();
    }

    private void HideToTray()
    {
        CancelAutoHide();
        Hide();
    }

    private void CancelAutoHide()
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = null;
    }

    private bool HasOpenContextMenu()
    {
        return OverflowMenu.IsOpen
            || FindVisualChildren<GameItem>(GamesItemsControl).Any(item => item.IsContextMenuOpen);
    }

    private void GamesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var direction = e.Delta > 0 ? -1 : 1;
        if (_settings.BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical)
        {
            if (scrollViewer.ScrollableHeight <= 0)
            {
                return;
            }

            var targetOffset = Math.Clamp(
                scrollViewer.VerticalOffset + (direction * WheelScrollStep),
                0,
                scrollViewer.ScrollableHeight);
            scrollViewer.ScrollToVerticalOffset(targetOffset);
        }
        else
        {
            if (scrollViewer.ScrollableWidth <= 0)
            {
                return;
            }

            var targetOffset = Math.Clamp(
                scrollViewer.HorizontalOffset + (direction * WheelScrollStep),
                0,
                scrollViewer.ScrollableWidth);
            scrollViewer.ScrollToHorizontalOffset(targetOffset);
        }

        e.Handled = true;
    }

    private void MenuButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _barMoveStart = e.GetPosition(this);
            _suppressMenuClick = false;
        }
    }

    private async void MenuButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_settings.LockWindowPosition
            || e.LeftButton != MouseButtonState.Pressed
            || _barMoveStart is not Point start)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _barMoveStart = null;
        _suppressMenuClick = true;
        MenuButton.ReleaseMouseCapture();
        e.Handled = true;

        try
        {
            DragMove();
            await PersistWindowPositionAsync();
            PlaceAtDesktopLevel();
        }
        catch (InvalidOperationException)
        {
            // The pointer can be released between the movement threshold and DragMove.
        }
        finally
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => _suppressMenuClick = false));
        }
    }

    private void MenuButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _barMoveStart = null;
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressMenuClick)
        {
            _suppressMenuClick = false;
            return;
        }

        if (MenuButton.ContextMenu is not { } menu)
        {
            return;
        }

        menu.PlacementTarget = MenuButton;
        menu.IsOpen = true;
    }

    private async void LockPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var shouldLock = !_settings.LockWindowPosition;
        var updatedSettings = _settings with
        {
            LockWindowPosition = shouldLock,
            RestoreWindowPosition = shouldLock || _settings.RestoreWindowPosition,
            WindowX = shouldLock ? Left : _settings.WindowX,
            WindowY = shouldLock ? Top : _settings.WindowY
        };

        try
        {
            await _settingsRepository.SaveAsync(updatedSettings);
            _settings = updatedSettings;
            UpdatePositionControls();
            ShowStatus(shouldLock ? "Posição fixada." : "Posição liberada.");
        }
        catch (Exception exception)
        {
            UpdatePositionControls();
            await ReportOperationErrorAsync("The window lock preference could not be saved.", exception);
        }
    }

    private async void CenterBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var position = WindowPositionService.CenterHorizontally(
            Left,
            Top,
            Width,
            Height,
            GetDisplayAreas());
        Left = position.Left;
        Top = position.Top;
        await PersistWindowPositionAsync();
        PlaceAtDesktopLevel();
        ShowStatus("Barra centralizada no monitor.");
    }

    private void UpdatePositionControls()
    {
        LockPositionMenuItem.IsChecked = _settings.LockWindowPosition;
        LockPositionMenuItem.Header = _settings.LockWindowPosition
            ? "✓ Posição fixa"
            : "Fixar posição";
        MenuButton.Cursor = _settings.LockWindowPosition ? Cursors.Hand : Cursors.SizeAll;
        MenuButton.ToolTip = _settings.LockWindowPosition
            ? "Posição fixa • clique para abrir o menu"
            : "Arraste para mover • clique para abrir o menu";
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && OverflowMenu.IsOpen)
        {
            OverflowMenu.IsOpen = false;
            MenuButton.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ExitApplication();
            e.Handled = true;
            return;
        }

        var backwardKey = _settings.BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical
            ? Key.Up
            : Key.Left;
        var forwardKey = _settings.BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical
            ? Key.Down
            : Key.Right;
        if (e.Key == backwardKey || e.Key == forwardKey)
        {
            FocusAdjacentGame(e.Key == forwardKey ? 1 : -1);
            e.Handled = true;
        }
    }

    private void FocusAdjacentGame(int direction)
    {
        var items = FindVisualChildren<GameItem>(GamesItemsControl).ToArray();
        if (items.Length == 0)
        {
            return;
        }

        var currentIndex = Array.FindIndex(items, item => item.IsKeyboardFocusWithin);
        var nextIndex = currentIndex < 0
            ? direction > 0 ? 0 : items.Length - 1
            : (currentIndex + direction + items.Length) % items.Length;
        _ = items[nextIndex].FocusGame();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T matchingChild)
            {
                yield return matchingChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void OverflowMenu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            OverflowMenu.IsOpen = false;
            MenuButton.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ExitApplication();
            e.Handled = true;
        }
    }

    private async void WindowChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.LockWindowPosition
            || e.ChangedButton != MouseButton.Left
            || e.ButtonState != MouseButtonState.Pressed
            || e.OriginalSource is DependencyObject source && IsInsideInteractiveControl(source))
        {
            return;
        }

        try
        {
            DragMove();
            await PersistWindowPositionAsync();
            PlaceAtDesktopLevel();
        }
        catch (InvalidOperationException)
        {
            // The pointer can be released between the routed event and DragMove.
        }
    }

    private static bool IsInsideInteractiveControl(DependencyObject source)
    {
        DependencyObject? current = source;

        while (current is not null)
        {
            if (current is ButtonBase or ScrollBar)
            {
                return true;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void PlaceAtDesktopLevel()
    {
        if (!IsLoaded || !IsVisible || _dialogOpen || HasOpenContextMenu())
        {
            return;
        }

        Topmost = false;
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            _ = DesktopWindowLevelService.PlaceImmediatelyAboveDesktop(windowHandle);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
