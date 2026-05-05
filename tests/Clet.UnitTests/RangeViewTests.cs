using Terminal.Gui.ViewBase;
using Xunit;

namespace Clet.UnitTests;

public class RangeViewTests
{
    [Fact]
    public void Defaults_HasFillWidthAndUnitHeight ()
    {
        RangeView view = new ();

        Assert.Equal (Dim.Fill (), view.Width);
        Assert.Equal (Dim.Absolute (1), view.Height);
    }

    [Fact]
    public void Defaults_LowAndHighStartAtZero ()
    {
        RangeView view = new ();

        Assert.Equal (0, view.LowValue);
        Assert.Equal (0, view.HighValue);
    }

    [Fact]
    public void RangeResult_ReflectsValues ()
    {
        RangeView view = new () { LowValue = 3, HighValue = 7 };

        Assert.Equal ((3, 7), view.RangeResult);
    }

    [Fact]
    public void Increment_AppliesToBothSpinners ()
    {
        RangeView view = new () { Increment = 5, LowValue = 0, HighValue = 0 };

        Assert.Equal (5, view.Increment);
    }
}
