using Microsoft.Win32;

namespace Playline.Discovery.Epic;

public sealed class EpicManifestLocator : IEpicManifestLocator
{
    public IReadOnlyList<string> LocateManifestDirectories()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        AddCandidate(candidates, Path.Combine(
            commonApplicationData,
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests"));

        AddRegistryCandidates(candidates, RegistryHive.LocalMachine, RegistryView.Registry32);
        AddRegistryCandidates(candidates, RegistryHive.LocalMachine, RegistryView.Registry64);
        AddRegistryCandidates(candidates, RegistryHive.CurrentUser, RegistryView.Default);

        return candidates.Where(Directory.Exists).ToArray();
    }

    private static void AddRegistryCandidates(
        ISet<string> candidates,
        RegistryHive hive,
        RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(@"Software\Epic Games\EpicGamesLauncher");

            AddAppDataCandidate(candidates, key?.GetValue("AppDataPath") as string);
            AddInstallCandidate(candidates, key?.GetValue("InstallLocation") as string);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
        }
    }

    private static void AddAppDataCandidate(ISet<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddCandidate(candidates, Path.Combine(path, "EpicGamesLauncher", "Data", "Manifests"));
        AddCandidate(candidates, Path.Combine(path, "Data", "Manifests"));
    }

    private static void AddInstallCandidate(ISet<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddCandidate(candidates, Path.Combine(path, "Epic", "EpicGamesLauncher", "Data", "Manifests"));
        AddCandidate(candidates, Path.Combine(path, "Data", "Manifests"));
    }

    private static void AddCandidate(ISet<string> candidates, string path)
    {
        try
        {
            candidates.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)));
        }
        catch (Exception exception) when (exception is ArgumentException
                                             or NotSupportedException
                                             or PathTooLongException)
        {
        }
    }
}
