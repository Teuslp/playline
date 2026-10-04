using System.IO;
using System.Windows;
using Microsoft.Win32;
using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.App;

public partial class EditGameWindow : Window
{
    private readonly Game _originalGame;

    public EditGameWindow(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        InitializeComponent();

        _originalGame = game;
        EditedGame = game;
        NameTextBox.Text = game.Name;
        ExecutablePathTextBox.Text = game.ExecutablePath ?? string.Empty;
        ArgumentsTextBox.Text = game.Arguments ?? string.Empty;
        WorkingDirectoryTextBox.Text = game.WorkingDirectory ?? string.Empty;
        IconPathTextBox.Text = game.IconPath ?? string.Empty;
    }

    public Game EditedGame { get; private set; }

    private void BrowseExecutableButton_Click(object sender, RoutedEventArgs e)
    {
        var previousExecutable = NormalizeOptionalPath(ExecutablePathTextBox.Text);
        var previousExecutableDirectory = previousExecutable is null
            ? null
            : Path.GetDirectoryName(previousExecutable);
        var workingDirectoryWasAutomatic = string.IsNullOrWhiteSpace(WorkingDirectoryTextBox.Text)
            || string.Equals(
                NormalizeOptionalPath(WorkingDirectoryTextBox.Text),
                previousExecutableDirectory,
                StringComparison.OrdinalIgnoreCase);
        var dialog = new OpenFileDialog
        {
            Title = "Procurar executável do jogo",
            Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = GetExistingDirectory(previousExecutable)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ExecutablePathTextBox.Text = dialog.FileName;
        if (workingDirectoryWasAutomatic)
        {
            WorkingDirectoryTextBox.Text = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }
    }

    private void BrowseWorkingDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        var currentDirectory = NormalizeOptionalPath(WorkingDirectoryTextBox.Text);
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar diretório de trabalho",
            Multiselect = false,
            InitialDirectory = Directory.Exists(currentDirectory)
                ? currentDirectory
                : GetExistingDirectory(ExecutablePathTextBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
        {
            WorkingDirectoryTextBox.Text = dialog.FolderName;
        }
    }

    private void BrowseIconButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar imagem do jogo",
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.ico;*.bmp)|*.png;*.jpg;*.jpeg;*.ico;*.bmp|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = GetExistingDirectory(IconPathTextBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
        {
            IconPathTextBox.Text = dialog.FileName;
        }
    }

    private void UseAutomaticIconButton_Click(object sender, RoutedEventArgs e) =>
        IconPathTextBox.Clear();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Informe um nome para o jogo.", "Playline", MessageBoxButton.OK, MessageBoxImage.Information);
            NameTextBox.Focus();
            return;
        }

        var executablePath = NormalizeOptionalPath(ExecutablePathTextBox.Text);
        if (_originalGame.Source == GameSource.Manual
            && string.IsNullOrWhiteSpace(_originalGame.LaunchUri)
            && (executablePath is null || !File.Exists(executablePath)))
        {
            MessageBox.Show(this, "Selecione um executável existente.", "Playline", MessageBoxButton.OK, MessageBoxImage.Information);
            ExecutablePathTextBox.Focus();
            return;
        }

        EditedGame = _originalGame with
        {
            Name = name,
            ExecutablePath = executablePath,
            Arguments = NullIfWhiteSpace(ArgumentsTextBox.Text),
            WorkingDirectory = NormalizeOptionalPath(WorkingDirectoryTextBox.Text),
            IconPath = NormalizeOptionalPath(IconPathTextBox.Text)
        };

        DialogResult = true;
    }

    private static string? NormalizeOptionalPath(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : GameIdentity.NormalizePath(value);
    }

    private static string? NullIfWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetExistingDirectory(string? path)
    {
        var normalizedPath = NormalizeOptionalPath(path ?? string.Empty);
        if (normalizedPath is not null)
        {
            if (Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            var directory = Path.GetDirectoryName(normalizedPath);
            if (Directory.Exists(directory))
            {
                return directory;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    }
}
