using System.Text.Json.Serialization;

namespace Playline.Core.Models;

public sealed record AppSettings
{
    public bool StartWithWindows { get; init; }

    public bool AlwaysOnTop { get; init; }

    public GameDisplayMode DisplayMode { get; init; } = GameDisplayMode.Compact;

    public GameItemSize ItemSize { get; init; } = GameItemSize.Medium;

    public BarOrientation BarOrientation { get; init; } = global::Playline.Core.Models.BarOrientation.Horizontal;

    public AfterLaunchAction AfterLaunchAction { get; init; } = AfterLaunchAction.KeepOpen;

    public bool AutoHide { get; init; }

    public bool RestoreWindowPosition { get; init; } = true;

    public bool LockWindowPosition { get; init; }

    public double? WindowX { get; init; }

    public double? WindowY { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CloseAfterGameLaunch { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int IconSize { get; init; }

    public AppSettings Normalize()
    {
        var displayMode = Enum.IsDefined(DisplayMode)
            ? DisplayMode
            : GameDisplayMode.Compact;
        var itemSize = IconSize > 0
            ? FromLegacyIconSize(IconSize)
            : Enum.IsDefined(ItemSize)
                ? ItemSize
                : GameItemSize.Medium;
        var barOrientation = Enum.IsDefined(BarOrientation)
            ? BarOrientation
            : global::Playline.Core.Models.BarOrientation.Horizontal;
        var afterLaunchAction = CloseAfterGameLaunch
            ? AfterLaunchAction.Exit
            : Enum.IsDefined(AfterLaunchAction)
                ? AfterLaunchAction
                : AfterLaunchAction.KeepOpen;
        var hasValidPosition = WindowX is double windowX
            && WindowY is double windowY
            && double.IsFinite(windowX)
            && double.IsFinite(windowY);

        return this with
        {
            DisplayMode = displayMode,
            ItemSize = itemSize,
            BarOrientation = barOrientation,
            AfterLaunchAction = afterLaunchAction,
            RestoreWindowPosition = LockWindowPosition || RestoreWindowPosition,
            WindowX = hasValidPosition ? WindowX : null,
            WindowY = hasValidPosition ? WindowY : null,
            CloseAfterGameLaunch = false,
            IconSize = 0
        };
    }

    private static GameItemSize FromLegacyIconSize(int iconSize)
    {
        return iconSize switch
        {
            <= 44 => GameItemSize.Small,
            >= 60 => GameItemSize.Large,
            _ => GameItemSize.Medium
        };
    }
}
