using Playline.Core.Models;

namespace Playline.Core.Services;

public static class WindowPositionService
{
    private const double MinimumVisibleWidth = 48;
    private const double MinimumVisibleHeight = 32;

    public static WindowPosition Resolve(
        double? savedLeft,
        double? savedTop,
        double windowWidth,
        double windowHeight,
        IReadOnlyCollection<DisplayArea> displays)
    {
        if (!double.IsFinite(windowWidth) || windowWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth));
        }

        if (!double.IsFinite(windowHeight) || windowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowHeight));
        }

        ArgumentNullException.ThrowIfNull(displays);
        var validDisplays = displays.Where(display => display.IsValid).ToArray();
        if (validDisplays.Length == 0)
        {
            return new WindowPosition(0, 0, false);
        }

        if (savedLeft is double left
            && savedTop is double top
            && double.IsFinite(left)
            && double.IsFinite(top))
        {
            var display = validDisplays
                .Select(area => new
                {
                    Area = area,
                    Intersection = GetIntersection(area, left, top, windowWidth, windowHeight)
                })
                .Where(candidate => candidate.Intersection.Width >= Math.Min(windowWidth, MinimumVisibleWidth)
                    && candidate.Intersection.Height >= Math.Min(windowHeight, MinimumVisibleHeight))
                .OrderByDescending(candidate => candidate.Intersection.Width * candidate.Intersection.Height)
                .Select(candidate => (DisplayArea?)candidate.Area)
                .FirstOrDefault();

            if (display is DisplayArea matchingDisplay)
            {
                return new WindowPosition(
                    Clamp(left, matchingDisplay.Left, matchingDisplay.Left + matchingDisplay.Width - windowWidth),
                    Clamp(top, matchingDisplay.Top, matchingDisplay.Top + matchingDisplay.Height - windowHeight),
                    true);
            }
        }

        var primary = validDisplays.FirstOrDefault(display => display.IsPrimary);
        if (!primary.IsValid)
        {
            primary = validDisplays[0];
        }

        return new WindowPosition(
            primary.Left + Math.Max(0, (primary.Width - windowWidth) / 2),
            primary.Top + Math.Max(0, (primary.Height - windowHeight) / 2),
            false);
    }

    public static WindowPosition CenterHorizontally(
        double currentLeft,
        double currentTop,
        double windowWidth,
        double windowHeight,
        IReadOnlyCollection<DisplayArea> displays)
    {
        if (!double.IsFinite(windowWidth) || windowWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth));
        }

        if (!double.IsFinite(windowHeight) || windowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowHeight));
        }

        ArgumentNullException.ThrowIfNull(displays);
        var validDisplays = displays.Where(display => display.IsValid).ToArray();
        if (validDisplays.Length == 0)
        {
            return new WindowPosition(0, 0, false);
        }

        var display = validDisplays
            .Select(area => new
            {
                Area = area,
                Intersection = GetIntersection(area, currentLeft, currentTop, windowWidth, windowHeight)
            })
            .Where(candidate => candidate.Intersection.Width > 0 && candidate.Intersection.Height > 0)
            .OrderByDescending(candidate => candidate.Intersection.Width * candidate.Intersection.Height)
            .Select(candidate => (DisplayArea?)candidate.Area)
            .FirstOrDefault();

        var targetDisplay = display ?? validDisplays.FirstOrDefault(area => area.IsPrimary);
        if (!targetDisplay.IsValid)
        {
            targetDisplay = validDisplays[0];
        }

        var centeredLeft = targetDisplay.Left + Math.Max(0, (targetDisplay.Width - windowWidth) / 2);
        var top = double.IsFinite(currentTop)
            ? Clamp(currentTop, targetDisplay.Top, targetDisplay.Top + targetDisplay.Height - windowHeight)
            : targetDisplay.Top + Math.Max(0, (targetDisplay.Height - windowHeight) / 2);
        return new WindowPosition(centeredLeft, top, false);
    }

    private static (double Width, double Height) GetIntersection(
        DisplayArea display,
        double left,
        double top,
        double width,
        double height)
    {
        var intersectionWidth = Math.Max(
            0,
            Math.Min(left + width, display.Left + display.Width) - Math.Max(left, display.Left));
        var intersectionHeight = Math.Max(
            0,
            Math.Min(top + height, display.Top + display.Height) - Math.Max(top, display.Top));

        return (intersectionWidth, intersectionHeight);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);
    }
}
