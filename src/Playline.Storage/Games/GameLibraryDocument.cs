using Playline.Core.Models;

namespace Playline.Storage.Games;

internal sealed record GameLibraryDocument
{
    public List<Game> Games { get; init; } = [];
}

