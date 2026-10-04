using Microsoft.Win32;

namespace Playline.Discovery.Steam;

public sealed class SteamInstallationLocator : ISteamInstallationLocator
{
    public IReadOnlyList<string> LocateInstallations()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRegistryCandidate(candidates, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath");
        AddRegistryCandidate(candidates, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Valve\Steam", "InstallPath");
        AddRegistryCandidate(candidates, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Valve\Steam", "InstallPath");

        AddCandidate(candidates, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));
        AddCandidate(candidates, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam"));

        return candidates.Where(Directory.Exists).ToArray();
    }

    private static void AddRegistryCandidate(
        ISet<string> candidates,
        RegistryHive hive,
        RegistryView view,
        string keyPath,
        string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(keyPath);
            AddCandidate(candidates, key?.GetValue(valueName) as string);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
        }
    }

    private static void AddCandidate(ISet<string> candidates, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        try
        {
            candidates.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate)));
        }
        catch (Exception exception) when (exception is ArgumentException
                                             or NotSupportedException
                                             or PathTooLongException)
        {
        }
    }
}
