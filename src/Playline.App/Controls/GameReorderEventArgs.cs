namespace Playline.App.Controls;

public sealed class GameReorderEventArgs(
    string gameId,
    string targetGameId,
    bool insertAfter) : EventArgs
{
    public string GameId { get; } = gameId;

    public string TargetGameId { get; } = targetGameId;

    public bool InsertAfter { get; } = insertAfter;
}

