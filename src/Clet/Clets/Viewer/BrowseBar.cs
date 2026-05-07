using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

/// <summary>
/// Top StatusBar with back/forward navigation and a location breadcrumb for viewer clets in browser mode.
/// Manages a history stack and exposes <see cref="Back"/> and <see cref="Forward"/> shortcuts whose
/// <c>Enabled</c> state tracks stack depth.
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

    /// <summary>The location breadcrumb shortcut (read-only label).</summary>
    public Shortcut Location { get; }

    /// <summary>The StatusBar positioned at Y=0.</summary>
    public StatusBar Bar { get; }

    /// <summary>Called when back/forward navigation fires. The argument is the target location string.</summary>
    public Action<string>? OnNavigate { get; set; }

    public BrowseBar (string? initialLocation)
    {
        _current = initialLocation;

        Back = new Shortcut
        {
            AlignmentModes = AlignmentModes.EndToStart,
            Title = Glyphs.LeftArrow.ToString (),
            Key = Key.CursorLeft.WithCtrl,
            Enabled = false,
        };
        Back.Accepting += (_, _) => NavigateBack ();

        Forward = new Shortcut
        {
            Title = Glyphs.RightArrow.ToString (),
            Key = Key.CursorRight.WithCtrl,
            Enabled = false,
        };
        Forward.Accepting += (_, _) => NavigateForward ();

        Location = new Shortcut
        {
            Title = initialLocation ?? "(inline)",
            MouseHighlightStates = MouseState.None,
            Enabled = false,
        };

        Bar = new StatusBar ([Back, Location, Forward])
        {
            Y = 0,
            AlignmentModes = AlignmentModes.StartToEnd,
        };
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

    /// <summary>
    /// Sets the current location without affecting the history stacks.
    /// Use for the initial load.
    /// </summary>
    public void SetCurrent (string location)
    {
        _current = location;
        UpdateButtons ();
    }

    /// <summary>Updates the location breadcrumb text.</summary>
    public void SetLocationTitle (string title) => Location.Title = title;

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
