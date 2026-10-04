using System.Text.Json;

namespace Playline.Storage.Json;

internal static class JsonFileWriter
{
    public static async Task WriteAsync<T>(
        string filePath,
        T value,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The JSON file must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryFile = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                             temporaryFile,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        value,
                        JsonDefaults.Options,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryFile, filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryFile))
                {
                    File.Delete(temporaryFile);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A failed cleanup must not hide the original persistence result.
            }
        }
    }
}
