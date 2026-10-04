using Playline.Core.Contracts;

namespace Playline.Storage.Json;

internal static class CorruptJsonRecovery
{
    public static async Task TryReplaceAsync(
        string filePath,
        Func<CancellationToken, Task> writeReplacement,
        ICriticalErrorLogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var backupPath = $"{filePath}.corrupt-"
                    + $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.bak";
                File.Move(filePath, backupPath);
            }

            await writeReplacement(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or NotSupportedException)
        {
            await logger.LogAsync(
                    $"The corrupt JSON file '{Path.GetFileName(filePath)}' could not be repaired.",
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
