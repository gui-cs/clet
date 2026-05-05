using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class RangeView : View
{
    private readonly NumericUpDown<int> _low;
    private readonly NumericUpDown<int> _high;

    public RangeView ()
    {
        _low = new NumericUpDown<int> { X = 0, Width = Dim.Percent (45) };
        _high = new NumericUpDown<int> { X = Pos.Right (_low) + 2, Width = Dim.Percent (45) };

        Label separator = new () { X = Pos.Right (_low), Width = 2, Text = ".." };

        Add (_low, separator, _high);
    }

    public int Increment
    {
        get => _low.Increment;
        set
        {
            _low.Increment = value;
            _high.Increment = value;
        }
    }

    public int LowValue
    {
        get => _low.Value;
        set => _low.Value = value;
    }

    public int HighValue
    {
        get => _high.Value;
        set => _high.Value = value;
    }

    public (int Low, int High) RangeResult => (_low.Value, _high.Value);
}
