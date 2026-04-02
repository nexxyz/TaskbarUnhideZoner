using System.Drawing;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Tests;

public sealed class ZoneGeometryTests
{
    [Fact]
    public void ActiveZone_ShouldContainInteriorPoints()
    {
        var zone = new ZoneConfig
        {
            ActiveZone = new RectConfig { X = 0, Y = 1000, Width = 1920, Height = 120 }
        };

        var virtualScreen = new Rectangle(0, 0, 1920, 1080);

        Assert.True(ZoneGeometry.IsInZone(zone, new Point(120, 1079), virtualScreen));
        Assert.True(ZoneGeometry.IsInZone(zone, new Point(120, 1005), virtualScreen));
        Assert.False(ZoneGeometry.IsInZone(zone, new Point(120, 900), virtualScreen));
    }

    [Fact]
    public void ActiveZone_ShouldUseExactRectangle()
    {
        var zone = new ZoneConfig
        {
            ActiveZone = new RectConfig { X = -200, Y = 40, Width = 300, Height = 80 }
        };

        var virtualScreen = new Rectangle(-1920, 0, 3840, 1080);

        Assert.True(ZoneGeometry.IsInZone(zone, new Point(-100, 80), virtualScreen));
        Assert.False(ZoneGeometry.IsInZone(zone, new Point(120, 80), virtualScreen));
    }

}
