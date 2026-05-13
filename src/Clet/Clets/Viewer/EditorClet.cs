using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Document;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class EditorClet : IViewerClet
{
    public string PrimaryAlias => "edit";
    public IReadOnlyList<string> Aliases => ["edit", "editor"];
    public string Description => "Edit text files with menus, undo/redo, find/replace, and glob support.";
    public CletKind Kind => CletKind.Viewer;
    public Type ResultType => typeof(void);
    public bool AcceptsPositionalArgs => true;

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("readonly", "r", typeof (bool),
            "Open the file in read-only mode.",
            false, "false"),
    ];

    public async Task<CletRunResult> RunAsync (
        IApplication app,
        string? content,
        CletRunOptions options,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        // --- Expand positional args (glob patterns + explicit paths) ---

        List<string> files = [];

        if (options.Arguments is { Count: > 0 } args)
        {
            FileAccessPolicy policy = new (
                Directory.GetCurrentDirectory (),
                options.AllowedFiles,
                options.AllowBinary);

            files = MarkdownContentResolver.ExpandFiles (args, policy, out string? policyError);

            if (policyError is not null)
            {
                return new ()
                {
                    Status = CletRunStatus.Error,
                    ErrorCode = "file-access-denied",
                    ErrorMessage = policyError,
                };
            }

            // Preserve explicit (non-glob) paths for files that don't exist yet.
            // ExpandFiles skips missing files with a warning; for edit we want to
            // open a new empty buffer bound to the given path.
            foreach (string arg in args)
            {
                // Skip glob patterns (only * and ? are wildcards — matching ExpandFiles / Directory.GetFiles semantics).
                if (arg.Contains ('*') || arg.Contains ('?'))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath (arg);

                if (!files.Contains (fullPath))
                {
                    files.Add (fullPath);
                }
            }
        }

        string? filePath = files.Count > 0 ? files [0] : null;
        string? fileName = filePath is not null ? Path.GetFileName (filePath) : null;
        string? lastDirectory = filePath is not null ? Path.GetDirectoryName (filePath) : null;
        string? savedText = string.Empty;

        bool readOnly = options.CletOptions?.TryGetValue ("readonly", out string? roVal) == true
                        && roVal is "true" or "1";

        // --- Build the UI ---

        Runnable window = new ()
        {
            Title = fileName ?? "Untitled",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
            BorderStyle = LineStyle.None,
        };

        Editor editor = new ()
        {
            X = 0,
            Y = 1, // below MenuBar
            Width = Dim.Fill (),
            Height = Dim.Fill (1), // above StatusBar
            ReadOnly = readOnly,
            ShowLineNumbers = true,
            ConvertTabsToSpaces = true,
        };

#pragma warning disable CS0618 // SyntaxHighlighter/SyntaxLanguage are stopgap APIs (see gui-cs/Text #32)
        editor.SyntaxHighlighter = new TextMateSyntaxHighlighter ();

        if (filePath is not null)
        {
            editor.SyntaxLanguage = Path.GetExtension (filePath);
        }
#pragma warning restore CS0618

        // --- StatusBar shortcuts (declared early for capture) ---

        Shortcut cursorPositionShortcut = new ()
            { Title = "Ln 1, Col 1", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut modifiedShortcut = new () { Title = "", MouseHighlightStates = MouseState.None, Enabled = false };

        // --- Local state helpers ---

        bool UnsavedChanges () => editor.Document?.UndoStack.IsOriginalFile == false;

        void UpdateModifiedIndicator ()
        {
            bool dirty = UnsavedChanges ();
            modifiedShortcut.Title = dirty ? "Modified" : "";
            window.Title = dirty ? $"{fileName ?? "Untitled"}*" : fileName ?? "Untitled";
        }

#pragma warning disable CS0618 // SyntaxLanguage is a stopgap API (see gui-cs/Text #32)
        void UpdateSyntaxLanguage (string path)
        {
            editor.SyntaxLanguage = Path.GetExtension (path);
        }
#pragma warning restore CS0618

        void UpdateLocShortcut ()
        {
            TextDocument? document = editor.Document;

            if (document is null)
            {
                cursorPositionShortcut.Title = "Ln 1, Col 1";
            }
            else
            {
                DocumentLine line = document.GetLineByOffset (editor.CaretOffset);
                cursorPositionShortcut.Title = $"Ln {line.LineNumber}, Col {editor.CaretOffset - line.Offset + 1}";
            }
        }

        // --- File operations ---

        void LoadFile (string path)
        {
            string fullPath = Path.GetFullPath (path);

            if (!File.Exists (fullPath))
            {
                filePath = fullPath;
                fileName = Path.GetFileName (fullPath);
                lastDirectory = Path.GetDirectoryName (fullPath);
                savedText = string.Empty;
                editor.Document = new TextDocument ();
                UpdateSyntaxLanguage (fullPath);
                UpdateModifiedIndicator ();

                return;
            }

            string text = File.ReadAllText (fullPath);
            filePath = fullPath;
            fileName = Path.GetFileName (fullPath);
            lastDirectory = Path.GetDirectoryName (fullPath);
            savedText = text;
            editor.ClearSelection ();
            editor.Document = new TextDocument (text);
            editor.CaretOffset = 0;
            UpdateSyntaxLanguage (fullPath);
            UpdateModifiedIndicator ();
        }

        bool SaveFile ()
        {
            if (filePath is null)
            {
                return SaveAs ();
            }

            try
            {
                File.WriteAllText (filePath, editor.Document?.Text ?? string.Empty);
                savedText = editor.Document?.Text ?? string.Empty;
                editor.Document?.UndoStack.MarkAsOriginalFile ();
                UpdateModifiedIndicator ();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery (app, "Error", ex.Message, "Ok");

                return false;
            }

            return true;
        }

        bool SaveAs ()
        {
            SaveDialog sd = new ();

            if (lastDirectory is not null)
            {
                sd.Path = lastDirectory;
            }

            if (fileName is not null)
            {
                sd.Path = Path.Combine (sd.Path ?? ".", fileName);
            }

            app.Run (sd);
            bool canceled = sd.Canceled;
            string path = sd.Path;
            string sdFileName = sd.FileName ?? string.Empty;
            sd.Dispose ();

            if (canceled || string.IsNullOrWhiteSpace (path))
            {
                return false;
            }

            filePath = Path.GetFullPath (path);
            fileName = sdFileName;
            lastDirectory = Path.GetDirectoryName (filePath);

            return SaveFile ();
        }

        bool PromptSaveIfDirty ()
        {
            if (!UnsavedChanges ())
            {
                return true;
            }

            int? result = MessageBox.Query (
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
                return SaveFile (); // Yes
            }

            return true; // No — discard
        }

        void NewFile ()
        {
            if (!PromptSaveIfDirty ())
            {
                return;
            }

            filePath = null;
            fileName = null;
            savedText = string.Empty;
            editor.ClearSelection ();
            editor.Document = new TextDocument ();
            editor.CaretOffset = 0;
            UpdateModifiedIndicator ();
        }

        void OpenFile ()
        {
            if (!PromptSaveIfDirty ())
            {
                return;
            }

            OpenDialog od = new ()
            {
                Title = "Open",
                AllowsMultipleSelection = false,
                AllowedTypes = [new AllowedTypeAny ()],
                MustExist = true,
                OpenMode = OpenMode.File,
            };

            if (lastDirectory is not null)
            {
                od.Path = lastDirectory;
            }

            app.Run (od);

            if (!od.Canceled && od.FilePaths.Count > 0)
            {
                string selectedPath = od.FilePaths [0];
                lastDirectory = Path.GetDirectoryName (Path.GetFullPath (selectedPath));
                LoadFile (selectedPath);
            }

            od.Dispose ();
        }

        void QuitEditor ()
        {
            if (!PromptSaveIfDirty ())
            {
                return;
            }

            window.RequestStop ();
        }

        // --- Clipboard helpers (Editor doesn't have built-in clipboard commands) ---

        void Paste ()
        {
            if (editor.ReadOnly)
            {
                return;
            }

            IClipboard? clipboard = app.Clipboard;

            if (clipboard is null || !clipboard.TryGetClipboardData (out string contents))
            {
                return;
            }

            if (editor.HasSelection)
            {
                editor.ReplaceSelection (contents);
            }
            else
            {
                editor.Document?.Insert (editor.CaretOffset, contents);
            }
        }

        void Copy ()
        {
            if (!editor.HasSelection)
            {
                return;
            }

            app.Clipboard?.TrySetClipboardData (editor.SelectedText);
        }

        void Cut ()
        {
            if (editor.ReadOnly || !editor.HasSelection)
            {
                return;
            }

            Copy ();
            editor.ReplaceSelection (string.Empty);
        }

        // --- MenuBar ---

        MenuBar menu = new ();

        menu.Add (new MenuBarItem ("_File",
        [
            new MenuItem { Title = "_New", Key = Key.N.WithCtrl, Action = NewFile },
            new MenuItem { Title = "_Open", Key = Key.O.WithCtrl, Action = OpenFile },
            new MenuItem { Title = "_Save", Key = Key.S.WithCtrl, Action = () => SaveFile () },
            new MenuItem { Title = "Save _As", Action = () => SaveAs () },
            null!, // separator
            new MenuItem { Title = "_Quit", Key = Key.Q.WithCtrl, Action = QuitEditor },
        ]));

        menu.Add (new MenuBarItem ("_Edit",
        [
            new MenuItem { Title = "_Undo", Key = Key.Z.WithCtrl, Action = () => editor.Document?.UndoStack.Undo () },
            new MenuItem { Title = "_Redo", Key = Key.Y.WithCtrl, Action = () => editor.Document?.UndoStack.Redo () },
            null!, // separator
            new MenuItem { Title = "Cu_t", Key = Key.X.WithCtrl, Action = Cut },
            new MenuItem { Title = "_Copy", Key = Key.C.WithCtrl, Action = Copy },
            new MenuItem { Title = "_Paste", Key = Key.V.WithCtrl, Action = Paste },
            null!, // separator
            new MenuItem { Title = "Select _All", Key = Key.A.WithCtrl, Action = () => editor.SelectAll () },
        ]));

        // --- Wire events ---

        editor.CaretChanged += (_, _) =>
        {
            UpdateModifiedIndicator ();
            UpdateLocShortcut ();
        };

        // --- StatusBar ---

        List<Shortcut> statusItems =
        [
            new Shortcut (Application.GetDefaultKey (Command.Quit), "Quit", QuitEditor),
            new Shortcut (Key.F2, "Open", OpenFile),
            new Shortcut (Key.F3, "Save", () => SaveFile ()),
            modifiedShortcut,
            cursorPositionShortcut,
        ];

        // File selector: dropdown when multiple files, plain label otherwise
        DropDownList? fileSelector = null;

        if (files.Count > 1)
        {
            // Use basenames when they are all distinct; fall back to relative paths
            // so that files like a/readme.md and b/readme.md get unique labels.
            List<string> basenames = [.. files.Select (f => Path.GetFileName (f) ?? f)];
            bool hasCollisions = basenames.Count != basenames.Distinct (StringComparer.OrdinalIgnoreCase).Count ();
            string cwd = Directory.GetCurrentDirectory ();
            List<string> displayNames = hasCollisions
                ? [.. files.Select (f => Path.GetRelativePath (cwd, f))]
                : basenames;

            ObservableCollection<string> displayNamesOc = new (displayNames);

            fileSelector = new DropDownList ()
            {
                Source = new ListWrapper<string> (displayNamesOc),
                ReadOnly = true,
                Text = displayNames [0],
                Width = Dim.Auto (DimAutoStyle.Text, minimumContentDim: 20),
            };

            bool switchingFile = false;

            fileSelector.ValueChanged += (_, _) =>
            {
                if (switchingFile)
                {
                    return;
                }

                // Use the unique display name list — since labels are guaranteed distinct,
                // IndexOf is unambiguous even when files share the same basename.
                int index = displayNames.IndexOf (fileSelector.Text);

                if (index < 0 || index >= files.Count)
                {
                    return;
                }

                if (!PromptSaveIfDirty ())
                {
                    // Revert dropdown to current file
                    switchingFile = true;
                    int currentIndex = filePath is not null ? files.IndexOf (filePath) : -1;

                    if (currentIndex >= 0)
                    {
                        fileSelector.Text = displayNames [currentIndex];
                    }

                    switchingFile = false;

                    return;
                }

                LoadFile (files [index]);
            };

            statusItems.Add (new Shortcut () { CommandView = fileSelector, HelpText = "File" });
        }
        else
        {
            Shortcut fileInfoShortcut = new ()
                { Title = fileName ?? "Untitled", MouseHighlightStates = MouseState.None, Enabled = false };

            // UpdateTitle needs to update this shortcut
            statusItems.Add (fileInfoShortcut);
        }

        StatusBar statusBar = new (statusItems)
            { AlignmentModes = AlignmentModes.StartToEnd | AlignmentModes.IgnoreFirstOrLast };

        // --- Assemble window ---

        window.Add (menu, editor, statusBar);

        // --- Load content after layout ---

        window.Initialized += (_, _) =>
        {
            if (filePath is not null && File.Exists (filePath))
            {
                LoadFile (filePath);
            }
            else if (filePath is not null)
            {
                // File doesn't exist yet — treat as new file at that path
                savedText = string.Empty;
                editor.Document = new TextDocument ();
                UpdateModifiedIndicator ();
            }
            else if (content is not null)
            {
                // Piped content via --initial
                editor.Document = new TextDocument (content);
                savedText = string.Empty; // Mark as unsaved
                UpdateModifiedIndicator ();
            }

            editor.SetFocus ();
        };

        // --- Run ---

        try
        {
            await app.RunAsync (window, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new () { Status = CletRunStatus.Cancelled };
        }

        return new () { Status = CletRunStatus.Ok };
    }
}
