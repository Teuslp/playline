using System.Windows;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Discovery;
using Playline.Windows.Icons;

namespace Playline.App;

public partial class DiscoveryWindow : Window
{
    private readonly GameDiscoveryService _discoveryService;
    private readonly GameArtworkService _gameArtwork;
    private readonly GameIdentityIndex _existingGameIndex;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private IReadOnlyList<DiscoveryListItem> _items = Array.Empty<DiscoveryListItem>();

    public DiscoveryWindow(
        GameDiscoveryService discoveryService,
        GameArtworkService gameArtwork,
        IReadOnlyList<Game> existingGames)
    {
        InitializeComponent();

        _discoveryService = discoveryService;
        _gameArtwork = gameArtwork;
        _existingGameIndex = new GameIdentityIndex(existingGames);
        Loaded += Window_Loaded;
    }

    public IReadOnlyList<Game> SelectedGames { get; private set; } = Array.Empty<Game>();

    protected override void OnClosed(EventArgs e)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        base.OnClosed(e);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= Window_Loaded;

        try
        {
            var games = await _discoveryService.ScanAsync(_cancellationTokenSource.Token);
            var gamesWithArtwork = new List<Game>(games.Count);
            foreach (var game in games)
            {
                var iconPath = await _gameArtwork.GetOrCreateAsync(game, _cancellationTokenSource.Token);
                gamesWithArtwork.Add(game with { IconPath = iconPath });
            }

            _items = gamesWithArtwork
                .Select(game => new DiscoveryListItem(
                    game,
                    !_existingGameIndex.Contains(game)))
                .OrderBy(item => item.Game.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            ResultsList.ItemsSource = _items;
            AddSelectedButton.IsEnabled = _items.Any(item => item.CanAdd);
            StatusText.Text = _items.Count == 0
                ? "Nenhum jogo instalado foi encontrado."
                : $"{_items.Count} jogo(s) encontrado(s).";
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedGames = _items
            .Where(item => item.CanAdd && item.IsSelected)
            .Select(item => item.Game)
            .ToArray();

        if (SelectedGames.Count == 0)
        {
            return;
        }

        DialogResult = true;
    }

    private sealed class DiscoveryListItem
    {
        public DiscoveryListItem(Game game, bool canAdd)
        {
            Game = game;
            CanAdd = canAdd;
            IsSelected = canAdd;
        }

        public Game Game { get; }

        public bool CanAdd { get; }

        public bool IsSelected { get; set; }

        public string Description => CanAdd
            ? Game.Source.ToString()
            : $"{Game.Source} — Já adicionado";

        public string Initial => string.IsNullOrWhiteSpace(Game.Name)
            ? "?"
            : Game.Name[..1].ToUpperInvariant();
    }
}
