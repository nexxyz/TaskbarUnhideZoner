using System.Drawing;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Tests;

public sealed class ZoneGeometryTests
{
    [Fact]
    public void BottomEdgeZone_ShouldContainBottomPixels()
    {
        var zone = new ZoneConfig
        {
            Mode = ZoneMode.EdgeBar,
            Edge = EdgePosition.Bottom,
            EdgeThicknessPx = 3
        };

        var screenBounds = new Rectangle(0, 0, 1920, 1080);
        var edgeRect = ZoneGeometry.GetEdgeRectangle(zone, screenBounds);

        Assert.True(edgeRect.Contains(new Point(120, 1079)));
        Assert.True(edgeRect.Contains(new Point(120, 1078)));
        Assert.False(edgeRect.Contains(new Point(120, 1075)));
    }

    [Fact]
    public void ActiveZone_ShouldUseExactRectangle()
    {
        var zone = new ZoneConfig
        {
            Mode = ZoneMode.HotZone,
            ActiveZone = new RectConfig { X = -200, Y = 40, Width = 300, Height = 80 }
        };

        var virtualScreen = new Rectangle(-1920, 0, 3840, 1080);

        Assert.True(ZoneGeometry.IsInZone(zone, new Point(-100, 80), virtualScreen));
        Assert.False(ZoneGeometry.IsInZone(zone, new Point(120, 80), virtualScreen));
    }

    [Fact]
    public void ActiveZone_ShouldDriveDetectionRegardlessOfMode()
    {
        var zone = new ZoneConfig
        {
            Mode = ZoneMode.EdgeBar,
            Edge = EdgePosition.Bottom,
            ActiveZone = new RectConfig { X = 50, Y = 50, Width = 200, Height = 120 }
        };

        var virtualScreen = new Rectangle(0, 0, 1920, 1080);

        Assert.True(ZoneGeometry.IsInZone(zone, new Point(120, 100), virtualScreen));
        Assert.False(ZoneGeometry.IsInZone(zone, new Point(120, 500), virtualScreen));
    }
}
