namespace Playline.Windows.Shortcuts;

public sealed record ShortcutTarget(
    string TargetPath,
    string? Arguments,
    string? WorkingDirectory);

