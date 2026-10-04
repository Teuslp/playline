namespace Playline.Core.Models;

public sealed record Game
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public string? ExecutablePath { get; init; }

    public string? Arguments { get; init; }

    public string? WorkingDirectory { get; init; }

    public string? LaunchUri { get; init; }

    public string? IconPath { get; init; }

    public GameSource Source { get; init; } = GameSource.Manual;

    public bool IsFavorite { get; init; }

    public int SortOrder { get; init; } = int.MaxValue;

    public DateTimeOffset DateAdded { get; init; } = DateTimeOffset.UtcNow;
}
