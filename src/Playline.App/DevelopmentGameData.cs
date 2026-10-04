#if DEBUG
using Playline.Core.Models;

namespace Playline.App;

internal static class DevelopmentGameData
{
    private static readonly string[] Names =
    [
        "Cyberpunk 2077",
        "Counter-Strike 2",
        "Minecraft",
        "Fortnite",
        "GTA V",
        "Rocket League",
        "Hades",
        "Baldur's Gate 3",
        "Forza Horizon 5",
        "Stardew Valley"
    ];

    public static IReadOnlyList<Game>? FromArguments(IEnumerable<string> arguments)
    {
        const string prefix = "--demo-count=";
        var countArgument = arguments.FirstOrDefault(
            argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (countArgument is null
            || !int.TryParse(countArgument[prefix.Length..], out var count)
            || count < 0)
        {
            return null;
        }

        return Enumerable.Range(0, count)
            .Select(index => new Game
            {
                Name = index < Names.Length
                    ? Names[index]
                    : $"Jogo {index + 1}"
            })
            .ToArray();
    }
}
#endif
