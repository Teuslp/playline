namespace Playline.Discovery.Epic;

public interface IEpicManifestLocator
{
    IReadOnlyList<string> LocateManifestDirectories();
}

