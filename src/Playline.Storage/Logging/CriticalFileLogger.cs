using System.Text;
using Playline.Core.Contracts;

namespace Playline.Storage.Logging;

public sealed class CriticalFileLogger : ICriticalErrorLogger
{
    private const int MaximumEntryCharacters = 64 * 1024;
    private readonly string _logsDirectory;
    private readonly long _maximumFileBytes;
    private readonly int _retainedFileCount;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public CriticalFileLogger(
        string logsDirectory,
        long maximumFileBytes = 1024 * 1024,
        int retainedFileCount = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retainedFileCount);

        _logsDirectory = Path.GetFullPath(logsDirectory);
        _maximumFileBytes = maximumFileBytes;
        _retainedFileCount = retainedFileCount;
    }

    public async Task LogAsync(
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        var lockTaken = false;
        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockTaken = true;
            Directory.CreateDirectory(_logsDirectory);

            var logFile = Path.Combine(_logsDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
            var entry = new StringBuilder()
                .Append('[')
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append("] ")
                .AppendLine(message);

            if (exception is not null)
            {
                entry.AppendLine(exception.ToString());
            }

            var entryText = LimitEntry(entry.AppendLine().ToString());
            RotateIfNeeded(logFile, Encoding.UTF8.GetByteCount(entryText));
            await File.AppendAllTextAsync(
                    logFile,
                    entryText,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            PruneOldFiles();
        }
        catch
        {
            // Logging must never be the reason the application stops.
        }
        finally
        {
            if (lockTaken)
            {
                _writeGate.Release();
            }
        }
    }

    private static string LimitEntry(string entry)
    {
        if (entry.Length <= MaximumEntryCharacters)
        {
            return entry;
        }

        return entry[..MaximumEntryCharacters]
            + Environment.NewLine
            + "[entry truncated]"
            + Environment.NewLine;
    }

    private void RotateIfNeeded(string logFile, int incomingBytes)
    {
        if (!File.Exists(logFile)
            || new FileInfo(logFile).Length + incomingBytes <= _maximumFileBytes)
        {
            return;
        }

        var maximumRotation = Math.Max(0, _retainedFileCount - 1);
        if (maximumRotation == 0)
        {
            File.Delete(logFile);
            return;
        }

        var oldestRotation = GetRotationPath(logFile, maximumRotation);
        if (File.Exists(oldestRotation))
        {
            File.Delete(oldestRotation);
        }

        for (var index = maximumRotation - 1; index >= 1; index--)
        {
            var source = GetRotationPath(logFile, index);
            if (File.Exists(source))
            {
                File.Move(source, GetRotationPath(logFile, index + 1), overwrite: true);
            }
        }

        File.Move(logFile, GetRotationPath(logFile, 1), overwrite: true);
    }

    private void PruneOldFiles()
    {
        foreach (var oldFile in Directory
                     .EnumerateFiles(_logsDirectory, "*.log")
                     .Select(path => new FileInfo(path))
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                     .Skip(_retainedFileCount))
        {
            oldFile.Delete();
        }
    }

    private static string GetRotationPath(string logFile, int rotation)
    {
        var directory = Path.GetDirectoryName(logFile)!;
        var fileName = Path.GetFileNameWithoutExtension(logFile);
        return Path.Combine(directory, $"{fileName}.{rotation}.log");
    }
}
