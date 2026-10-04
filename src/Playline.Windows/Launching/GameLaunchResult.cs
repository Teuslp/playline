namespace Playline.Windows.Launching;

public sealed record GameLaunchResult(bool Success, string? ErrorMessage = null)
{
    public static GameLaunchResult Succeeded { get; } = new(true);

    public static GameLaunchResult Failed(string message) => new(false, message);
}

