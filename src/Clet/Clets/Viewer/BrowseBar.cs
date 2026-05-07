using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

/// <summary>
/// Back/forward navigation history for viewer clets in browser mode.
/// Exposes <see cref="Back"/> and <see cref="Forward"/> shortcuts (single-glyph wide)
/// for insertion into an existing StatusBar. Manages the history stacks internally.
/// </summary>
internal sealed class BrowseBar
{
    private readonly Stack<string> _backStack = new ();
    private readonly Stack<string> _forwardStack = new ();
    private string? _current;

    /// <summary>The back shortcut (Ctrl+Left).</summary>
    public Shortcut Back { get; }

    /// <summary>The forward shortcut (Ctrl+Right).</summary>
    public Shortcut Forward { get; }

    /// <summary>Called when back/forward navigation fires. The argument is the target location string.</summary>
    public Action<string>? OnNavigate { get; set; }

    public BrowseBar (string? initialLocation)
    {
        _current = initialLocation;

        Back = new Shortcut
        {
            Title = Glyphs.LeftArrow.ToString (),
            Key = Key.CursorLeft.WithCtrl,
            Command = Command.Left,
            Action = NavigateBack,
            Enabled = false,
        };
        Back.AlignmentModes = AlignmentModes.StartToEnd;
        Back.KeyView.Visible = false;

        Forward = new Shortcut
        {
            Title = Glyphs.RightArrow.ToString (),
            Key = Key.CursorRight.WithCtrl,
            Command = Command.Right,
            Action = NavigateForward,
            Enabled = false,
        };
        Forward.KeyView.Visible = false;
    }

    /// <summary>
    /// Records a navigation from the current location to <paramref name="location"/>.
    /// Pushes the current location onto the back stack and clears the forward stack.
    /// </summary>
    public void Push (string location)
    {
        if (_current is not null)
        {
            _backStack.Push (_current);
        }

        _forwardStack.Clear ();
        _current = location;
        UpdateButtons ();
    }

    private void NavigateBack ()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        if (_current is not null)
        {
            _forwardStack.Push (_current);
        }

        _current = _backStack.Pop ();
        OnNavigate?.Invoke (_current);
        UpdateButtons ();
    }

    private void NavigateForward ()
    {
        if (_forwardStack.Count == 0)
        {
            return;
        }

        if (_current is not null)
        {
            _backStack.Push (_current);
        }

        _current = _forwardStack.Pop ();
        OnNavigate?.Invoke (_current);
        UpdateButtons ();
    }

    private void UpdateButtons ()
    {
        Back.Enabled = _backStack.Count > 0;
        Forward.Enabled = _forwardStack.Count > 0;
    }
}
