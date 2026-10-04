using System.Security.Cryptography;
using System.Text;
using Playline.Core.Models;

namespace Playline.Core.Services;

public static class GameIdentity
{
    public static string CreateManualId(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var normalizedPath = NormalizePath(executablePath)
            ?? throw new ArgumentException("The executable path is invalid.", nameof(executablePath));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant()));

        return $"manual-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public static string CreateLauncherId(string launcher, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return $"{launcher.Trim().ToLowerInvariant()}-{identifier.Trim().ToLowerInvariant()}";
    }

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expandedPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return expandedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public static bool AreEquivalent(Game first, Game second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (!string.IsNullOrWhiteSpace(first.Id)
            && string.Equals(first.Id, second.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstPath = NormalizePath(first.ExecutablePath);
        var secondPath = NormalizePath(second.ExecutablePath);
        if (firstPath is not null
            && secondPath is not null
            && string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(first.LaunchUri)
            && !string.IsNullOrWhiteSpace(second.LaunchUri)
            && string.Equals(first.LaunchUri, second.LaunchUri, StringComparison.OrdinalIgnoreCase);
    }
}
