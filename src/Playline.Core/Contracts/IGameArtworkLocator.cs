using Playline.Core.Models;

namespace Playline.Core.Contracts;

public interface IGameArtworkLocator
{
    string? Locate(Game game);
}
