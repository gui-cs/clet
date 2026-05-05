using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class RangeView : View, IValue<(int Low, int High)?>
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

    public int Minimum
    {
        get => _low.Minimum;
        set
        {
            _low.Minimum = value;
            _high.Minimum = value;
        }
    }

    public int Maximum
    {
        get => _high.Maximum;
        set
        {
            _low.Maximum = value;
            _high.Maximum = value;
        }
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

    (int Low, int High)? IValue<(int Low, int High)?>.Value
    {
        get => (_low.Value, _high.Value);
        set
        {
            if (value is { } v)
            {
                _low.Value = v.Low;
                _high.Value = v.High;
            }
        }
    }
}
