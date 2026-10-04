using System.Text.Json;
using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Storage.Json;
using Playline.Storage.Paths;

namespace Playline.Storage.Games;

public sealed class JsonGameRepository(
    AppDataPaths paths,
    ICriticalErrorLogger logger) : IGameRepository
{
    public async Task<IReadOnlyList<Game>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            paths.EnsureCreated();

            if (!File.Exists(paths.GamesFile))
            {
                await SaveAsync([], cancellationToken).ConfigureAwait(false);
                return Array.Empty<Game>();
            }

            var json = await File.ReadAllTextAsync(paths.GamesFile, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                await SaveAsync([], cancellationToken).ConfigureAwait(false);
                return Array.Empty<Game>();
            }

            var document = JsonSerializer.Deserialize<GameLibraryDocument>(
                json,
                JsonDefaults.Options);

            return document?.Games?.ToArray() ?? Array.Empty<Game>();
        }
        catch (JsonException exception)
        {
            await logger.LogAsync(
                    "The game library could not be loaded. An empty library will be used.",
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);

            await CorruptJsonRecovery.TryReplaceAsync(
                    paths.GamesFile,
                    token => SaveAsync([], token),
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);

            return Array.Empty<Game>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await logger.LogAsync(
                    "The game library could not be loaded. An empty library will be used.",
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);

            return Array.Empty<Game>();
        }
    }

    public Task SaveAsync(
        IReadOnlyCollection<Game> games,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);

        var document = new GameLibraryDocument
        {
            Games = games.ToList()
        };

        return JsonFileWriter.WriteAsync(paths.GamesFile, document, cancellationToken);
    }
}
