using System.Windows;
using Playline.Core.Models;

namespace Playline.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _originalSettings;

    public SettingsWindow(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();

        _originalSettings = settings.Normalize();
        Settings = _originalSettings;

        DisplayModeComboBox.ItemsSource = new[]
        {
            new Option<GameDisplayMode>(GameDisplayMode.Compact, "Somente ícone"),
            new Option<GameDisplayMode>(GameDisplayMode.Name, "Ícone + nome")
        };
        ItemSizeComboBox.ItemsSource = new[]
        {
            new Option<GameItemSize>(GameItemSize.Small, "Pequeno"),
            new Option<GameItemSize>(GameItemSize.Medium, "Médio"),
            new Option<GameItemSize>(GameItemSize.Large, "Grande")
        };
        BarOrientationComboBox.ItemsSource = new[]
        {
            new Option<BarOrientation>(BarOrientation.Horizontal, "Horizontal"),
            new Option<BarOrientation>(BarOrientation.Vertical, "Vertical")
        };
        BarThemeComboBox.ItemsSource = new[]
        {
            new Option<BarTheme>(BarTheme.Glass, "Vidro transparente"),
            new Option<BarTheme>(BarTheme.IconsOnly, "Somente ícones")
        };
        AfterLaunchComboBox.ItemsSource = new[]
        {
            new Option<AfterLaunchAction>(AfterLaunchAction.KeepOpen, "Manter aberto"),
            new Option<AfterLaunchAction>(AfterLaunchAction.Minimize, "Minimizar"),
            new Option<AfterLaunchAction>(AfterLaunchAction.Hide, "Ocultar"),
            new Option<AfterLaunchAction>(AfterLaunchAction.Exit, "Fechar Playline")
        };

        Select(DisplayModeComboBox, _originalSettings.DisplayMode);
        Select(ItemSizeComboBox, _originalSettings.ItemSize);
        Select(BarOrientationComboBox, _originalSettings.BarOrientation);
        Select(BarThemeComboBox, _originalSettings.BarTheme);
        Select(AfterLaunchComboBox, _originalSettings.AfterLaunchAction);
        StartWithWindowsCheckBox.IsChecked = _originalSettings.StartWithWindows;
        AutoHideCheckBox.IsChecked = _originalSettings.AutoHide;
        RestorePositionCheckBox.IsChecked = _originalSettings.RestoreWindowPosition;
        LockPositionCheckBox.IsChecked = _originalSettings.LockWindowPosition;
    }

    public AppSettings Settings { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var lockWindowPosition = LockPositionCheckBox.IsChecked == true;
        var restoreWindowPosition = lockWindowPosition || RestorePositionCheckBox.IsChecked == true;
        Settings = _originalSettings with
        {
            DisplayMode = SelectedValue(DisplayModeComboBox, GameDisplayMode.Compact),
            ItemSize = SelectedValue(ItemSizeComboBox, GameItemSize.Medium),
            BarOrientation = SelectedValue(BarOrientationComboBox, BarOrientation.Horizontal),
            BarTheme = SelectedValue(BarThemeComboBox, BarTheme.Glass),
            AfterLaunchAction = SelectedValue(AfterLaunchComboBox, AfterLaunchAction.KeepOpen),
            AlwaysOnTop = false,
            StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
            AutoHide = AutoHideCheckBox.IsChecked == true,
            RestoreWindowPosition = restoreWindowPosition,
            LockWindowPosition = lockWindowPosition,
            WindowX = restoreWindowPosition ? _originalSettings.WindowX : null,
            WindowY = restoreWindowPosition ? _originalSettings.WindowY : null,
            CloseAfterGameLaunch = false,
            IconSize = 0
        };

        DialogResult = true;
    }

    private static void Select<T>(System.Windows.Controls.ComboBox comboBox, T value)
        where T : struct, Enum
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<Option<T>>()
            .First(option => EqualityComparer<T>.Default.Equals(option.Value, value));
    }

    private static T SelectedValue<T>(System.Windows.Controls.ComboBox comboBox, T fallback)
        where T : struct, Enum
    {
        return comboBox.SelectedItem is Option<T> option ? option.Value : fallback;
    }

    private sealed record Option<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }
}
