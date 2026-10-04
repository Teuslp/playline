using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Playline.Core.Models;

namespace Playline.App.Controls;

public partial class GameItem : UserControl
{
    private Point? _dragStart;
    private bool _isReordering;
    private bool _suppressNextClick;

    public static readonly DependencyProperty GameProperty = DependencyProperty.Register(
        nameof(Game),
        typeof(Game),
        typeof(GameItem),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty DisplayModeProperty = DependencyProperty.Register(
        nameof(DisplayMode),
        typeof(GameDisplayMode),
        typeof(GameItem),
        new PropertyMetadata(GameDisplayMode.Compact, OnVisualPropertyChanged));

    public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(
        nameof(ItemSize),
        typeof(GameItemSize),
        typeof(GameItem),
        new PropertyMetadata(GameItemSize.Medium, OnVisualPropertyChanged));

    public static readonly DependencyProperty BarOrientationProperty = DependencyProperty.Register(
        nameof(BarOrientation),
        typeof(BarOrientation),
        typeof(GameItem),
        new PropertyMetadata(global::Playline.Core.Models.BarOrientation.Horizontal, OnVisualPropertyChanged));

    public GameItem()
    {
        InitializeComponent();
    }

    public event EventHandler<GameActionEventArgs>? OpenRequested;

    public event EventHandler<GameActionEventArgs>? EditRequested;

    public event EventHandler<GameActionEventArgs>? FavoriteRequested;

    public event EventHandler<GameActionEventArgs>? MoveLeftRequested;

    public event EventHandler<GameActionEventArgs>? MoveRightRequested;

    public event EventHandler<GameActionEventArgs>? RemoveRequested;

    public event EventHandler<GameReorderEventArgs>? ReorderRequested;

    public Game? Game
    {
        get => (Game?)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    public GameDisplayMode DisplayMode
    {
        get => (GameDisplayMode)GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    public GameItemSize ItemSize
    {
        get => (GameItemSize)GetValue(ItemSizeProperty);
        set => SetValue(ItemSizeProperty, value);
    }

    public BarOrientation BarOrientation
    {
        get => (BarOrientation)GetValue(BarOrientationProperty);
        set => SetValue(BarOrientationProperty, value);
    }

    public bool FocusGame() => ItemButton.Focus();

    public bool IsContextMenuOpen => ItemButton.ContextMenu?.IsOpen == true;

    private static void OnVisualPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        ((GameItem)dependencyObject).UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (!IsInitialized)
        {
            return;
        }

        var game = Game;
        var name = game?.Name?.Trim();
        var accessibleName = string.IsNullOrWhiteSpace(name) ? "Jogo sem nome" : name;
        var metrics = GetMetrics(ItemSize, BarOrientation);
        var isNameMode = DisplayMode == GameDisplayMode.Name;
        var isInvalid = game is not null
            && game.Source == GameSource.Manual
            && string.IsNullOrWhiteSpace(game.LaunchUri)
            && (string.IsNullOrWhiteSpace(game.ExecutablePath) || !File.Exists(game.ExecutablePath));

        var isVertical = BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical;
        Width = isNameMode ? metrics.NameModeWidth : metrics.CompactWidth;
        Height = metrics.ItemHeight;
        ItemButton.Width = Width - 2;
        ItemButton.Height = Height - (isVertical ? 4 : 2);
        ItemButton.HorizontalContentAlignment = isNameMode
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        IconHost.Width = metrics.IconSize;
        IconHost.Height = metrics.IconSize;
        PlaceholderSurface.CornerRadius = new CornerRadius(metrics.IconCornerRadius);
        IconSurface.CornerRadius = new CornerRadius(metrics.IconCornerRadius);
        IconHighlight.CornerRadius = new CornerRadius(metrics.IconCornerRadius);
        InitialText.FontSize = metrics.InitialFontSize;
        NameText.FontSize = metrics.NameFontSize;
        NameText.Text = accessibleName;
        NameText.Visibility = isNameMode ? Visibility.Visible : Visibility.Collapsed;

        InitialText.Text = string.IsNullOrWhiteSpace(name)
            ? "?"
            : name[..1].ToUpper(CultureInfo.CurrentCulture);
        FavoriteIndicator.Visibility = game?.IsFavorite == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        InvalidIndicator.Visibility = isInvalid ? Visibility.Visible : Visibility.Collapsed;
        FavoriteMenuItem.Header = game?.IsFavorite == true
            ? "Remover dos favoritos"
            : "Favoritar";
        MoveBackwardMenuItem.Header = BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical
            ? "Mover para cima"
            : "Mover para a esquerda";
        MoveForwardMenuItem.Header = BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical
            ? "Mover para baixo"
            : "Mover para a direita";

        if (isVertical)
        {
            ItemSeparator.Width = Math.Max(26, Width * 0.58);
            ItemSeparator.Height = 1;
            ItemSeparator.HorizontalAlignment = HorizontalAlignment.Center;
            ItemSeparator.VerticalAlignment = VerticalAlignment.Bottom;
        }
        else
        {
            ItemSeparator.Width = 1;
            ItemSeparator.Height = Math.Max(26, Height * 0.68);
            ItemSeparator.HorizontalAlignment = HorizontalAlignment.Right;
            ItemSeparator.VerticalAlignment = VerticalAlignment.Center;
        }

        var tooltip = isInvalid
            ? $"{accessibleName}\nArquivo não encontrado — use Editar para corrigir."
            : accessibleName;
        ItemButton.ToolTip = tooltip;
        ItemButton.Opacity = isInvalid ? 0.62 : 1;
        AutomationProperties.SetName(ItemButton, accessibleName);
        AutomationProperties.SetHelpText(ItemButton, isInvalid ? "Arquivo não encontrado" : string.Empty);
        AutomationProperties.SetAutomationId(ItemButton, $"Game-{game?.Id ?? "Unknown"}");

        var dpi = VisualTreeHelper.GetDpi(this);
        var decodeWidth = (int)Math.Ceiling(
            metrics.IconSize * Math.Max(dpi.DpiScaleX, dpi.DpiScaleY) * 2);
        var icon = LoadIcon(game?.IconPath, decodeWidth);
        if (icon is not null)
        {
            var imageBrush = new ImageBrush(icon)
            {
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                Stretch = Stretch.UniformToFill
            };
            imageBrush.Freeze();
            IconSurface.Background = imageBrush;
        }
        else
        {
            IconSurface.Background = Brushes.Transparent;
        }

        IconSurface.Visibility = icon is null ? Visibility.Collapsed : Visibility.Visible;
        PlaceholderSurface.Visibility = icon is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage? LoadIcon(string? iconPath, int decodeWidth)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodeWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static ItemMetrics GetMetrics(GameItemSize itemSize, BarOrientation barOrientation)
    {
        if (barOrientation == global::Playline.Core.Models.BarOrientation.Horizontal)
        {
            return itemSize switch
            {
                GameItemSize.Small => new ItemMetrics(56, 40, 30, 110, 14, 11, 4),
                GameItemSize.Large => new ItemMetrics(94, 62, 52, 176, 23, 13, 8),
                _ => new ItemMetrics(72, 50, 40, 142, 18, 12, 6)
            };
        }

        return itemSize switch
        {
            GameItemSize.Small => new ItemMetrics(40, 40, 30, 96, 14, 11, 4),
            GameItemSize.Large => new ItemMetrics(62, 62, 52, 144, 23, 13, 8),
            _ => new ItemMetrics(50, 50, 40, 116, 18, 12, 6)
        };
    }

    private void ItemButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _suppressNextClick = false;
    }

    private void ItemButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _dragStart is not Point dragStart
            || Game is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        _isReordering = true;
        _suppressNextClick = true;
        _ = ItemButton.CaptureMouse();
        Mouse.OverrideCursor = BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical
            ? Cursors.SizeNS
            : Cursors.SizeWE;
        e.Handled = true;
    }

    private void ItemButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isReordering || Game is null)
        {
            return;
        }

        var window = Window.GetWindow(this);
        var hit = window?.InputHitTest(e.GetPosition(window)) as DependencyObject;
        var target = FindAncestor<GameItem>(hit);
        if (target?.Game is not null && !string.Equals(target.Game.Id, Game.Id, StringComparison.OrdinalIgnoreCase))
        {
            var targetPosition = e.GetPosition(target);
            var insertAfter = BarOrientation == global::Playline.Core.Models.BarOrientation.Vertical
                ? targetPosition.Y > target.ActualHeight / 2
                : targetPosition.X > target.ActualWidth / 2;
            ReorderRequested?.Invoke(
                this,
                new GameReorderEventArgs(Game.Id, target.Game.Id, insertAfter));
        }

        _isReordering = false;
        _dragStart = null;
        ItemButton.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        Raise(OpenRequested);
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) => Raise(OpenRequested);

    private void EditMenuItem_Click(object sender, RoutedEventArgs e) => Raise(EditRequested);

    private void FavoriteMenuItem_Click(object sender, RoutedEventArgs e) => Raise(FavoriteRequested);

    private void MoveLeftMenuItem_Click(object sender, RoutedEventArgs e) => Raise(MoveLeftRequested);

    private void MoveRightMenuItem_Click(object sender, RoutedEventArgs e) => Raise(MoveRightRequested);

    private void RemoveMenuItem_Click(object sender, RoutedEventArgs e) => Raise(RemoveRequested);

    private void Raise(EventHandler<GameActionEventArgs>? handler)
    {
        if (Game is not null)
        {
            handler?.Invoke(this, new GameActionEventArgs(Game));
        }
    }

    private readonly record struct ItemMetrics(
        double CompactWidth,
        double ItemHeight,
        double IconSize,
        double NameModeWidth,
        double InitialFontSize,
        double NameFontSize,
        double IconCornerRadius);
}
