using Terminal.Gui.App;
using Terminal.Gui.Editor;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

/// <summary>
/// Settings dialog for the editor clet. Provides tabs for Config and Tab Settings.
/// </summary>
internal sealed class EditorSettingsDialog : Dialog
{
    private readonly CheckBox _autoCompleteCheck;
    private readonly NumericUpDown<int> _indentSize;
    private readonly CheckBox _convertTabsCheck;
    private readonly CheckBox _autoIndentCheck;

    internal bool WasAccepted { get; private set; }

    internal EditorSettingsDialog (Editor editor)
    {
        Title = "Settings";
        Width = Dim.Percent (60);
        Height = 16;

        // --- Tab Settings tab ---
        View tabSettingsTab = new ()
        {
            Title = "_Tab Settings",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        _indentSize = new ()
        {
            X = 20,
            Y = 1,
            Value = editor.IndentationSize,
            Width = 8,
        };
        _indentSize.ValueChanging += (_, e) =>
        {
            if (e.NewValue is < 1)
            {
                e.Handled = true;
            }
        };

        _convertTabsCheck = new ()
        {
            X = 1,
            Y = 3,
            Title = "Con_vert Tabs to Spaces",
            Value = editor.ConvertTabsToSpaces ? CheckState.Checked : CheckState.UnChecked,
        };

        _autoIndentCheck = new ()
        {
            X = 1,
            Y = 5,
            Title = "_Auto Indent",
            Value = editor.IndentationStrategy is not null ? CheckState.Checked : CheckState.UnChecked,
        };

        tabSettingsTab.Add (
            new Label () { X = 1, Y = 1, Text = "_Indent size:" },
            _indentSize,
            _convertTabsCheck,
            _autoIndentCheck);

        _autoCompleteCheck = new ()
        {
            X = 1,
            Y = 1,
            Title = "Auto _Complete (Ctrl+Space)",
            Value = editor.CompletionProvider is not null ? CheckState.Checked : CheckState.UnChecked,
        };

        // --- Config tab ---
        View configTab = new ()
        {
            Title = "_Config",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        configTab.Add (_autoCompleteCheck);

        // --- Tabs ---
        Tabs tabs = new ()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill (),
            Height = Dim.Fill (2),
        };

        tabs.InsertTab (0, configTab);
        tabs.InsertTab (1, tabSettingsTab);

        Button okBtn = new ()
        {
            Text = "OK",
            X = Pos.Center () - 6,
            Y = Pos.Bottom (tabs),
            IsDefault = true,
        };

        Button cancelBtn = new ()
        {
            Text = "Cancel",
            X = Pos.Right (okBtn) + 2,
            Y = Pos.Bottom (tabs),
        };

        okBtn.Accepting += (_, _) =>
        {
            WasAccepted = true;
            RequestStop ();
        };

        cancelBtn.Accepting += (_, _) => RequestStop ();

        Add (tabs, okBtn, cancelBtn);
    }

    /// <summary>
    /// Applies the accepted settings to the editor. Call only when <see cref="WasAccepted"/> is true.
    /// </summary>
    internal void ApplyTo (Editor editor)
    {
        editor.IndentationSize = Math.Max (1, _indentSize.Value);
        editor.ConvertTabsToSpaces = _convertTabsCheck.Value == CheckState.Checked;
        editor.IndentationStrategy = _autoIndentCheck.Value == CheckState.Checked
            ? new DefaultIndentationStrategy ()
            : null;
        editor.CompletionProvider = _autoCompleteCheck.Value == CheckState.Checked
            ? new WordCompletionProvider ()
            : null;
    }
}
