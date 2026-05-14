using Terminal.Gui.App;
using Terminal.Gui.Document.Search;
using Terminal.Gui.Editor;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Clet;

internal sealed class FindReplaceDialog : Dialog
{
    private readonly Editor _editor;
    private readonly TextField _findField;
    private readonly TextField _replaceField;
    private readonly CheckBox _matchCase;
    private readonly CheckBox _wholeWord;
    private readonly CheckBox _regex;
    private readonly Label _statusLabel;

    internal FindReplaceDialog (Editor editor, bool showReplace = false)
    {
        _editor = editor;
        Title = "Find and Replace";
        Width = Dim.Percent (60);
        Height = 14;

        _findField = new () { X = 12, Y = 0, Width = Dim.Fill (1) };
        _replaceField = new () { X = 12, Y = 0, Width = Dim.Fill (1) };
        _matchCase = new () { Title = "Match _case" };
        _wholeWord = new () { Title = "_Whole word" };
        _regex = new () { Title = "Re_gex" };
        _statusLabel = new () { X = 0, Width = Dim.Fill (), Text = "" };

        // Pre-populate from editor selection
        if (_editor.HasSelection)
        {
            int start = Math.Min (_editor.SelectionStart, _editor.SelectionEnd);
            int end = Math.Max (_editor.SelectionStart, _editor.SelectionEnd);
            string selected = _editor.Document?.Text?.Substring (start, end - start) ?? "";

            if (!selected.Contains ('\n'))
            {
                _findField.Text = selected;
            }
        }

        // Find tab content
        Label findLabel = new () { Text = "_Find what:", X = 0, Y = 0 };

        View findTab = new ()
        {
            Title = "Find",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };
        findTab.Add (findLabel, _findField);

        // Replace tab content
        Label replaceLabel = new () { Text = "_Replace:", X = 0, Y = 0 };

        View replaceTab = new ()
        {
            Title = "Replace",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };
        replaceTab.Add (replaceLabel, _replaceField);

        // Tabs
        Tabs tabs = new ()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill (),
            Height = 3,
        };
        tabs.InsertTab (0, findTab);
        tabs.InsertTab (1, replaceTab);

        if (showReplace)
        {
            tabs.Value = replaceTab;
        }

        // Buttons
        Button findNextBtn = new () { Text = "Find _Next", IsDefault = true };
        findNextBtn.Accepting += (_, _) => DoFind (forward: true);

        Button findPrevBtn = new () { Text = "Find _Previous" };
        findPrevBtn.Accepting += (_, _) => DoFind (forward: false);

        Button replaceBtn = new () { Text = "_Replace" };
        replaceBtn.Accepting += (_, _) => DoReplace ();

        Button replaceAllBtn = new () { Text = "Replace _All" };
        replaceAllBtn.Accepting += (_, _) => DoReplaceAll ();

        // Layout below tabs
        View controlsArea = new ()
        {
            X = 0,
            Y = Pos.Bottom (tabs),
            Width = Dim.Fill (),
            Height = 5,
        };

        _matchCase.X = 0;
        _matchCase.Y = 0;
        _wholeWord.X = Pos.Right (_matchCase) + 2;
        _wholeWord.Y = 0;
        _regex.X = Pos.Right (_wholeWord) + 2;
        _regex.Y = 0;

        findNextBtn.X = 0;
        findNextBtn.Y = 1;
        findPrevBtn.X = Pos.Right (findNextBtn) + 1;
        findPrevBtn.Y = 1;
        replaceBtn.X = Pos.Right (findPrevBtn) + 1;
        replaceBtn.Y = 1;
        replaceAllBtn.X = Pos.Right (replaceBtn) + 1;
        replaceAllBtn.Y = 1;
        _statusLabel.Y = 2;

        controlsArea.Add (_matchCase, _wholeWord, _regex);
        controlsArea.Add (findNextBtn, findPrevBtn, replaceBtn, replaceAllBtn);
        controlsArea.Add (_statusLabel);

        Button closeBtn = new () { Text = "Close", X = Pos.Center (), Y = Pos.Bottom (controlsArea) };
        closeBtn.Accepting += (_, _) => RequestStop ();

        Add (tabs, controlsArea, closeBtn);
    }

    private void ApplySearchStrategy ()
    {
        string searchText = _findField.Text ?? "";

        if (string.IsNullOrEmpty (searchText))
        {
            _editor.SearchStrategy = null;

            return;
        }

        bool ignoreCase = _matchCase.Value != CheckState.Checked;
        bool wholeWord = _wholeWord.Value == CheckState.Checked;
        SearchMode mode = _regex.Value == CheckState.Checked ? SearchMode.RegEx : SearchMode.Normal;

        try
        {
            _editor.SearchStrategy = SearchStrategyFactory.Create (searchText, ignoreCase, wholeWord, mode);
            _statusLabel.Text = "";
        }
        catch (SearchPatternException ex)
        {
            _statusLabel.Text = $"Pattern error: {ex.Message}";
            _editor.SearchStrategy = null;
        }
    }

    private void DoFind (bool forward)
    {
        ApplySearchStrategy ();

        if (_editor.SearchStrategy is null)
        {
            return;
        }

        bool found = forward ? _editor.FindNext (true) : _editor.FindPrevious (true);
        _statusLabel.Text = found ? "" : "No match";
    }

    private void DoReplace ()
    {
        ApplySearchStrategy ();

        if (_editor.SearchStrategy is null)
        {
            return;
        }

        string replacement = _replaceField.Text ?? "";
        _editor.ReplaceNext (replacement, true);
    }

    private void DoReplaceAll ()
    {
        ApplySearchStrategy ();

        if (_editor.SearchStrategy is null)
        {
            return;
        }

        string replacement = _replaceField.Text ?? "";
        int count = _editor.ReplaceAll (replacement);
        _statusLabel.Text = count > 0 ? $"Replaced {count} occurrences" : "No match";
    }
}
