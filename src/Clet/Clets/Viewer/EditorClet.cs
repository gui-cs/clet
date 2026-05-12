using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class EditorClet : IViewerClet
{
    public string PrimaryAlias => "edit";
    public IReadOnlyList<string> Aliases => ["edit", "editor"];
    public string Description => "Edit a text file with menus, undo/redo, and find/replace.";
    public CletKind Kind => CletKind.Viewer;
    public Type ResultType => typeof(void);
    public bool AcceptsPositionalArgs => true;

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new("readonly", "r", typeof(bool),
            "Open the file in read-only mode.",
            false, "false"),
    ];

    public async Task<CletRunResult> RunAsync(
        IApplication app,
        string? content,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new() { Status = CletRunStatus.Cancelled };
        }

        // Resolve file path from positional args
        string? filePath = options.Arguments?.FirstOrDefault();
        string? fileName = null;
        string? lastDirectory = null;
        string? savedText = string.Empty;

        bool readOnly = options.CletOptions?.TryGetValue("readonly", out string? roVal) == true
                        && roVal is "true" or "1";

        if (filePath is not null)
        {
            filePath = Path.GetFullPath(filePath);
            fileName = Path.GetFileName(filePath);
            lastDirectory = Path.GetDirectoryName(filePath);
        }

        // --- Build the UI ---

        Runnable window = new()
        {
            Title = fileName ?? "Untitled",
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
        };

        Editor editor = new()
        {
            X = 0,
            Y = 1, // below MenuBar
            Width = Dim.Fill(),
            Height = Dim.Fill(1), // above StatusBar
            ReadOnly = readOnly,
            ShowLineNumbers = true,
            ConvertTabsToSpaces = true,
        };

        // --- StatusBar shortcuts (declared early for capture) ---

        Shortcut cursorPositionShortcut = new()
            { Title = "Ln 1, Col 1", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut fileInfoShortcut = new()
            { Title = fileName ?? "Untitled", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut modifiedShortcut = new() { Title = "", MouseHighlightStates = MouseState.None, Enabled = false };

        // --- Local state helpers ---

        bool UnsavedChanges() => editor.Document?.UndoStack.IsOriginalFile == false;

        void UpdateModifiedIndicator()
        {
            bool dirty = UnsavedChanges();
            modifiedShortcut.Title = dirty ? "Modified" : "";
            window.Title = dirty ? $"{fileName ?? "Untitled"}*" : fileName ?? "Untitled";
        }

        void UpdateTitle()
        {
            fileInfoShortcut.Title = fileName ?? "Untitled";
            UpdateModifiedIndicator();
        }

        void UpdateLocShortcut()
        {
            TextDocument? document = editor.Document;

            if (document is null)
            {
                cursorPositionShortcut.Title = "Ln 1, Col 1";
            }
            else
            {
                DocumentLine line = document.GetLineByOffset(editor.CaretOffset);
                cursorPositionShortcut.Title = $"Ln {line.LineNumber}, Col {editor.CaretOffset - line.Offset + 1}";
            }
        }

        // --- File operations ---

        void LoadFile(string path)
        {
            string fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                // New file — just set metadata
                filePath = fullPath;
                fileName = Path.GetFileName(fullPath);
                lastDirectory = Path.GetDirectoryName(fullPath);
                savedText = string.Empty;
                editor.Document = new TextDocument();
                UpdateTitle();

                return;
            }

            string text = File.ReadAllText(fullPath);
            filePath = fullPath;
            fileName = Path.GetFileName(fullPath);
            lastDirectory = Path.GetDirectoryName(fullPath);
            savedText = text;
            editor.ClearSelection();
            editor.Document = new TextDocument(text);
            editor.CaretOffset = 0;
            UpdateTitle();
        }

        bool SaveFile()
        {
            if (filePath is null)
            {
                return SaveAs();
            }

            try
            {
                File.WriteAllText(filePath, editor.Document?.Text ?? string.Empty);
                savedText = editor.Document?.Text ?? string.Empty;
                editor.Document?.UndoStack.MarkAsOriginalFile();
                UpdateModifiedIndicator();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(app, "Error", ex.Message, "Ok");

                return false;
            }

            return true;
        }

        bool SaveAs()
        {
            SaveDialog sd = new();

            if (lastDirectory is not null)
            {
                sd.Path = lastDirectory;
            }

            if (fileName is not null)
            {
                sd.Path = Path.Combine(sd.Path ?? ".", fileName);
            }

            app.Run(sd);
            bool canceled = sd.Canceled;
            string path = sd.Path;
            string sdFileName = sd.FileName ?? string.Empty;
            sd.Dispose();

            if (canceled || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            filePath = Path.GetFullPath(path);
            fileName = sdFileName;
            lastDirectory = Path.GetDirectoryName(filePath);

            return SaveFile();
        }

        bool PromptSaveIfDirty()
        {
            if (!UnsavedChanges())
            {
                return true;
            }

            int? result = MessageBox.Query(
                app,
                "Unsaved Changes",
                $"Save changes to {fileName ?? "Untitled"}?",
                "Cancel", "No", "Yes");

            if (result is null or 0)
            {
                return false; // Cancel
            }

            if (result == 2)
            {
                return SaveFile(); // Yes
            }

            return true; // No — discard
        }

        void NewFile()
        {
            if (!PromptSaveIfDirty())
            {
                return;
            }

            filePath = null;
            fileName = null;
            savedText = string.Empty;
            editor.ClearSelection();
            editor.Document = new TextDocument();
            editor.CaretOffset = 0;
            UpdateTitle();
        }

        void OpenFile()
        {
            if (!PromptSaveIfDirty())
            {
                return;
            }

            OpenDialog od = new()
            {
                Title = "Open",
                AllowsMultipleSelection = false,
                AllowedTypes = [new AllowedTypeAny()],
                MustExist = true,
                OpenMode = OpenMode.File,
            };

            if (lastDirectory is not null)
            {
                od.Path = lastDirectory;
            }

            app.Run(od);

            if (!od.Canceled && od.FilePaths.Count > 0)
            {
                string selectedPath = od.FilePaths[0];
                lastDirectory = Path.GetDirectoryName(Path.GetFullPath(selectedPath));
                LoadFile(selectedPath);
            }

            od.Dispose();
        }

        void QuitEditor()
        {
            if (!PromptSaveIfDirty())
            {
                return;
            }

            window.RequestStop();
        }

        // --- Clipboard helpers (Editor doesn't have built-in clipboard commands) ---

        void Paste()
        {
            if (editor.ReadOnly)
            {
                return;
            }

            IClipboard? clipboard = app.Clipboard;

            if (clipboard is null || !clipboard.TryGetClipboardData(out string contents))
            {
                return;
            }

            if (editor.HasSelection)
            {
                editor.ReplaceSelection(contents);
            }
            else
            {
                editor.Document?.Insert(editor.CaretOffset, contents);
            }
        }

        void Copy()
        {
            if (!editor.HasSelection)
            {
                return;
            }

            app.Clipboard?.TrySetClipboardData(editor.SelectedText);
        }

        void Cut()
        {
            if (editor.ReadOnly || !editor.HasSelection)
            {
                return;
            }

            Copy();
            editor.ReplaceSelection(string.Empty);
        }

        // --- MenuBar ---

        MenuBar menu = new();

        menu.Add(new MenuBarItem("_File",
        [
            new MenuItem { Title = "_New", Key = Key.N.WithCtrl, Action = NewFile },
            new MenuItem { Title = "_Open", Key = Key.O.WithCtrl, Action = OpenFile },
            new MenuItem { Title = "_Save", Key = Key.S.WithCtrl, Action = () => SaveFile() },
            new MenuItem { Title = "Save _As", Action = () => SaveAs() },
            null!, // separator
            new MenuItem { Title = "_Quit", Key = Key.Q.WithCtrl, Action = QuitEditor },
        ]));

        menu.Add(new MenuBarItem("_Edit",
        [
            new MenuItem { Title = "_Undo", Key = Key.Z.WithCtrl, Action = () => editor.Document?.UndoStack.Undo() },
            new MenuItem { Title = "_Redo", Key = Key.Y.WithCtrl, Action = () => editor.Document?.UndoStack.Redo() },
            null!, // separator
            new MenuItem { Title = "Cu_t", Key = Key.X.WithCtrl, Action = Cut },
            new MenuItem { Title = "_Copy", Key = Key.C.WithCtrl, Action = Copy },
            new MenuItem { Title = "_Paste", Key = Key.V.WithCtrl, Action = Paste },
            null!, // separator
            new MenuItem { Title = "Select _All", Key = Key.A.WithCtrl, Action = () => editor.SelectAll() },
        ]));

        // --- Wire events ---

        editor.CaretChanged += (_, _) =>
        {
            UpdateModifiedIndicator();
            UpdateLocShortcut();
        };

        // --- StatusBar ---

        StatusBar statusBar = new(
        [
            new Shortcut(Application.GetDefaultKey(Command.Quit), "Quit", QuitEditor),
            new Shortcut(Key.F2, "Open", OpenFile),
            new Shortcut(Key.F3, "Save", () => SaveFile()),
            modifiedShortcut,
            cursorPositionShortcut,
            fileInfoShortcut,
        ]) { AlignmentModes = AlignmentModes.StartToEnd | AlignmentModes.IgnoreFirstOrLast };

        // --- Assemble window ---

        window.Add(menu, editor, statusBar);

        // --- Load content after layout ---

        window.Initialized += (_, _) =>
        {
            if (filePath is not null && File.Exists(filePath))
            {
                LoadFile(filePath);
            }
            else if (filePath is not null)
            {
                // File doesn't exist yet — treat as new file at that path
                savedText = string.Empty;
                editor.Document = new TextDocument();
                UpdateTitle();
            }
            else if (content is not null)
            {
                // Piped content via --initial
                editor.Document = new TextDocument(content);
                savedText = string.Empty; // Mark as unsaved
                UpdateModifiedIndicator();
            }

            editor.SetFocus();
        };

        // --- Run ---

        try
        {
            await app.RunAsync(window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new() { Status = CletRunStatus.Cancelled };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new() { Status = CletRunStatus.Cancelled };
        }

        return new() { Status = CletRunStatus.Ok };
    }
}
