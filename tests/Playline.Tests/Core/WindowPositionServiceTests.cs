using Playline.Core.Models;
using Playline.Core.Services;

namespace Playline.Tests.Core;

public sealed class WindowPositionServiceTests
{
    private static readonly DisplayArea[] Displays =
    [
        new DisplayArea(0, 0, 1920, 1040, IsPrimary: true),
        new DisplayArea(-1280, 0, 1280, 984)
    ];

    [Fact]
    public void Resolve_WhenSavedPositionIsVisible_PreservesIt()
    {
        var position = WindowPositionService.Resolve(250, 120, 580, 88, Displays);

        Assert.Equal(new WindowPosition(250, 120, true), position);
    }

    [Fact]
    public void Resolve_WhenWindowWouldCrossEdge_ClampsToWorkingArea()
    {
        var position = WindowPositionService.Resolve(1800, 1000, 580, 88, Displays);

        Assert.Equal(1340, position.Left);
        Assert.Equal(952, position.Top);
        Assert.True(position.UsedSavedPosition);
    }

    [Fact]
    public void Resolve_WhenSavedMonitorIsGone_CentersOnPrimaryDisplay()
    {
        var position = WindowPositionService.Resolve(4000, 3000, 580, 88, Displays);

        Assert.Equal(670, position.Left);
        Assert.Equal(476, position.Top);
        Assert.False(position.UsedSavedPosition);
    }

    [Fact]
    public void CenterHorizontally_UsesCurrentMonitorAndPreservesVerticalPosition()
    {
        var position = WindowPositionService.CenterHorizontally(-1100, 140, 238, 68, Displays);

        Assert.Equal(-759, position.Left);
        Assert.Equal(140, position.Top);
    }

    [Fact]
    public void CenterHorizontally_WhenWindowIsOffScreen_UsesPrimaryMonitor()
    {
        var position = WindowPositionService.CenterHorizontally(4000, 3000, 238, 68, Displays);

        Assert.Equal(841, position.Left);
        Assert.Equal(972, position.Top);
    }
}
