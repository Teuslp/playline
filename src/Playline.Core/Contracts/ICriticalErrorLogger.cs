namespace Playline.Core.Contracts;

public interface ICriticalErrorLogger
{
    Task LogAsync(
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}

