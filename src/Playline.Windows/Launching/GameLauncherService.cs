using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.Windows.Launching;

public sealed class GameLauncherService(ICriticalErrorLogger logger)
{
    private static readonly HashSet<string> AllowedUriSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam",
        "com.epicgames.launcher"
    };

    public async Task<GameLaunchResult> LaunchAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        try
        {
            var startInfo = CreateStartInfo(game);
            using var process = Process.Start(startInfo);

            return process is null
                ? GameLaunchResult.Failed("O Windows não conseguiu iniciar o jogo.")
                : GameLaunchResult.Succeeded;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                             or Win32Exception
                                             or FileNotFoundException
                                             or ArgumentException)
        {
            await logger.LogAsync(
                    $"The game '{game.Name}' could not be launched.",
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);

            return GameLaunchResult.Failed(exception.Message);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.LaunchUri))
        {
            if (!Uri.TryCreate(game.LaunchUri, UriKind.Absolute, out var launchUri)
                || !AllowedUriSchemes.Contains(launchUri.Scheme))
            {
                throw new ArgumentException("O endereço de inicialização do jogo é inválido.");
            }

            return new ProcessStartInfo
            {
                FileName = launchUri.AbsoluteUri,
                UseShellExecute = true
            };
        }

        var executablePath = GameIdentity.NormalizePath(game.ExecutablePath);
        if (executablePath is null || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("O executável do jogo não foi encontrado.", executablePath);
        }

        if (!string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("O caminho configurado não aponta para um executável válido.");
        }

        var workingDirectory = GameIdentity.NormalizePath(game.WorkingDirectory);
        if (workingDirectory is null || !Directory.Exists(workingDirectory))
        {
            workingDirectory = Path.GetDirectoryName(executablePath);
        }

        return new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = game.Arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false
        };
    }
}
