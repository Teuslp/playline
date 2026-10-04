using Playline.Core.Contracts;

namespace Playline.Tests.TestSupport;

internal sealed class RecordingLogger : ICriticalErrorLogger
{
    public List<(string Message, Exception? Exception)> Entries { get; } = [];

    public Task LogAsync(
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        Entries.Add((message, exception));
        return Task.CompletedTask;
    }
}
