namespace Playline.Core.Models;

public readonly record struct DisplayArea(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary = false)
{
    public bool IsValid => double.IsFinite(Left)
        && double.IsFinite(Top)
        && double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}

