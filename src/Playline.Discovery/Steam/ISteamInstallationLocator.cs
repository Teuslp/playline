namespace Playline.Discovery.Steam;

public interface ISteamInstallationLocator
{
    IReadOnlyList<string> LocateInstallations();
}

