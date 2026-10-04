using System.Diagnostics;
using System.IO;
using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Windows.Icons;
using Playline.Windows.Shortcuts;

namespace Playline.Windows.Games;

public sealed class ManualGameService(
    ShellLinkShortcutResolver shortcutResolver,
    IconCacheService iconCache)
{
    public async Task<Game> CreateAsync(
        string selectedPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);

        var fullPath = Path.GetFullPath(selectedPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected game file does not exist.", fullPath);
        }

        var extension = Path.GetExtension(fullPath);
        string executablePath;
        string? arguments;
        string? workingDirectory;
        string fallbackName;

        if (string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var shortcut = shortcutResolver.Resolve(fullPath);
            executablePath = shortcut.TargetPath;
            arguments = shortcut.Arguments;
            workingDirectory = shortcut.WorkingDirectory;
            fallbackName = Path.GetFileNameWithoutExtension(fullPath);
        }
        else if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            executablePath = fullPath;
            arguments = null;
            workingDirectory = Path.GetDirectoryName(fullPath);
            fallbackName = Path.GetFileNameWithoutExtension(fullPath);
        }
        else
        {
            throw new NotSupportedException("Only .exe and .lnk files can be added manually.");
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The game executable does not exist.", executablePath);
        }

        workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetDirectoryName(executablePath)
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(workingDirectory));

        var id = GameIdentity.CreateManualId(executablePath);
        var game = new Game
        {
            Id = id,
            Name = GetFriendlyName(executablePath, fallbackName),
            ExecutablePath = Path.GetFullPath(executablePath),
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            Source = GameSource.Manual
        };

        var iconPath = await iconCache
            .GetOrCreateAsync($"{id}|{GameIdentity.NormalizePath(executablePath)}", executablePath, cancellationToken)
            .ConfigureAwait(false);

        return game with { IconPath = iconPath };
    }

    private static string GetFriendlyName(string executablePath, string fallbackName)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);

            if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
            {
                return versionInfo.FileDescription.Trim();
            }

            if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
            {
                return versionInfo.ProductName.Trim();
            }
        }
        catch (Exception exception) when (exception is FileNotFoundException
                                             or IOException
                                             or UnauthorizedAccessException)
        {
        }

        return fallbackName;
    }
}
