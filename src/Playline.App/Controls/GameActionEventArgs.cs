using Playline.Core.Models;

namespace Playline.App.Controls;

public sealed class GameActionEventArgs(Game game) : EventArgs
{
    public Game Game { get; } = game;
}
