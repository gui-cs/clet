using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMateSharp.Grammars;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class EditorClet : IViewerClet
{
    public string PrimaryAlias => "edit";
    public IReadOnlyList<string> Aliases => ["edit", "editor"];
    public string Description => "Edit text files with menus, undo/redo, find/replace, and glob support.";
    public CletKind Kind => CletKind.Viewer;
    public Type ResultType => typeof (void);
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
                options.AllowBinary,
                allowAllExtensions: true);

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

            foreach (string arg in args)
            {
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

        string? filePath = files.Count > 0 ? files[0] : null;
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
            Y = 1,
            Width = Dim.Fill (),
            Height = Dim.Fill (1),
            ReadOnly = readOnly,
            GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding,
            ConvertTabsToSpaces = true,
        };

        editor.HighlightingDefinition = filePath is not null
            ? HighlightingManager.Instance.GetDefinitionByExtension (Path.GetExtension (filePath))
            : null;

        // --- Folding support ---

        BraceFoldingStrategy braceFoldingStrategy = new ();

        void InstallFolding ()
        {
            if (editor.Document is null)
            {
                return;
            }

            FoldingManager fm = new (editor.Document);
            braceFoldingStrategy.UpdateFoldings (fm, editor.Document);
            editor.FoldingManager = fm;

            editor.Document.Changed += (_, _) =>
            {
                if (editor.FoldingManager is not null && editor.Document is not null)
                {
                    braceFoldingStrategy.UpdateFoldings (editor.FoldingManager, editor.Document);
                }
            };
        }

        // --- Markdown preview ---

        Markdown? markdownPreview = null;
        CheckBox? previewCheckBox = null;
        bool isMarkdownFile = filePath is not null
            && Path.GetExtension (filePath).Equals (".md", StringComparison.OrdinalIgnoreCase);

        void UpdatePreviewVisibility ()
        {
            bool show = previewCheckBox?.Value == CheckState.Checked && isMarkdownFile;

            if (show)
            {
                if (markdownPreview is null)
                {
                    markdownPreview = new Markdown ()
                    {
                        X = Pos.Percent (50),
                        Y = 1,
                        Width = Dim.Fill (),
                        Height = Dim.Fill (1),
                        SyntaxHighlighter = new TextMateSyntaxHighlighter (ThemeName.DarkPlus),
                    };

                    markdownPreview.Text = editor.Document?.Text ?? "";

                    editor.Document!.Changed += (_, _) =>
                    {
                        if (markdownPreview.Visible)
                        {
                            markdownPreview.Text = editor.Document?.Text ?? "";
                        }
                    };

                    window.Add (markdownPreview);
                }

                editor.Width = Dim.Percent (50);
                markdownPreview.Visible = true;
                markdownPreview.Text = editor.Document?.Text ?? "";
            }
            else
            {
                editor.Width = Dim.Fill ();

                if (markdownPreview is not null)
                {
                    markdownPreview.Visible = false;
                }
            }
        }

        // --- StatusBar shortcuts (declared early for capture) ---

        Shortcut cursorPositionShortcut = new ()
        { Title = "Ln 1, Col 1", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut modifiedShortcut = new () { Title = "", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut languageShortcut = new ()
        { Title = "Plain Text", MouseHighlightStates = MouseState.None, Enabled = false };

        // Filename shortcut for MenuBar
        Shortcut filenameShortcut = new ()
        {
            Title = fileName ?? "<untitled>",
            MouseHighlightStates = MouseState.None,
        };

        // --- Local state helpers ---

        bool UnsavedChanges () => editor.Document?.UndoStack.IsOriginalFile == false;

        void UpdateModifiedIndicator ()
        {
            bool dirty = UnsavedChanges ();
            modifiedShortcut.Title = dirty ? "Modified" : "";
            window.Title = dirty ? $"{fileName ?? "Untitled"}*" : fileName ?? "Untitled";
        }

        void UpdateLanguageShortcut ()
        {
            languageShortcut.Title = editor.HighlightingDefinition?.Name ?? "Plain Text";
        }

        void UpdateSyntaxLanguage (string path)
        {
            editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (Path.GetExtension (path));
            UpdateLanguageShortcut ();
        }

        void UpdateLocShortcut ()
        {
            TextDocument? document = editor.Document;

            if (document is null)
            {
                cursorPositionShortcut.Title = "Ln 1, Col 1";

                return;
            }

            DocumentLine line = document.GetLineByOffset (editor.CaretOffset);
            string loc = $"Ln {line.LineNumber}, Col {editor.CaretOffset - line.Offset + 1}";

            if (editor.HasMultipleCarets)
            {
                loc += $" ({editor.AdditionalCaretOffsets.Count + 1} carets)";
            }

            cursorPositionShortcut.Title = loc;
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
                InstallFolding ();
                UpdateModifiedIndicator ();
                filenameShortcut.Title = fileName;
                isMarkdownFile = Path.GetExtension (fullPath).Equals (".md", StringComparison.OrdinalIgnoreCase);

                if (previewCheckBox is not null)
                {
                    previewCheckBox.Visible = isMarkdownFile;
                }

                UpdatePreviewVisibility ();

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
            InstallFolding ();
            UpdateModifiedIndicator ();
            filenameShortcut.Title = fileName;
            isMarkdownFile = Path.GetExtension (fullPath).Equals (".md", StringComparison.OrdinalIgnoreCase);

            if (previewCheckBox is not null)
            {
                previewCheckBox.Visible = isMarkdownFile;
            }

            UpdatePreviewVisibility ();
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
                return false;
            }

            if (result == 2)
            {
                return SaveFile ();
            }

            return true;
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
            editor.HighlightingDefinition = null;
            InstallFolding ();
            UpdateModifiedIndicator ();
            UpdateLanguageShortcut ();
            filenameShortcut.Title = "<untitled>";
            isMarkdownFile = false;

            if (previewCheckBox is not null)
            {
                previewCheckBox.Visible = false;
            }

            UpdatePreviewVisibility ();
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
                string selectedPath = od.FilePaths[0];
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

        // --- Clipboard helpers ---

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

        // --- Find/Replace ---

        void ShowFindReplace (bool showReplace = false)
        {
            FindReplaceDialog dlg = new (editor, showReplace);
            app.Run (dlg);
            dlg.Dispose ();
        }

        // --- Edit menu items (reusable for context menu) ---

        MenuItem[] CreateEditMenuItems () =>
        [
            new () { Title = "_Undo", Key = Key.Z.WithCtrl, Action = () => editor.Document?.UndoStack.Undo () },
            new () { Title = "_Redo", Key = Key.Y.WithCtrl, Action = () => editor.Document?.UndoStack.Redo () },
            null!,
            new () { Title = "Cu_t", Key = Key.X.WithCtrl, Action = Cut },
            new () { Title = "_Copy", Key = Key.C.WithCtrl, Action = Copy },
            new () { Title = "_Paste", Key = Key.V.WithCtrl, Action = Paste },
            null!,
            new () { Title = "Select _All", Key = Key.A.WithCtrl, Action = () => editor.SelectAll () },
        ];

        // --- About dialog ---

        void ShowAbout ()
        {
            string editorVersion = VersionInfo.GetAssemblyVersion (
                typeof (Editor).Assembly, "unknown");

            Dialog about = new ()
            {
                Title = "About clet edit",
                Width = Dim.Percent (50),
                Height = 12,
            };

            Label info = new ()
            {
                X = 1,
                Y = 0,
                Width = Dim.Fill (1),
                Text = $"""
                         clet {VersionInfo.GetCletVersion ()}
                         Terminal.Gui {VersionInfo.GetTerminalGuiVersion ()}
                         Terminal.Gui.Editor {editorVersion}

                         https://github.com/gui-cs/clet
                         """,
            };

            Button ok = new () { Text = "OK", X = Pos.Center (), Y = Pos.Bottom (info) + 1, IsDefault = true };
            ok.Accepting += (_, _) => about.RequestStop ();
            about.Add (info, ok);
            app.Run (about);
            about.Dispose ();
        }

        // --- Options menu state ---

        bool optLineNumbers = true;
        bool optFoldIndicators = true;
        bool optConvertTabs = true;
        bool optAutoIndent = false;
        bool optUseThemeBg = false;
        bool optWordWrap = false;

        void UpdateGutterOptions ()
        {
            GutterOptions g = GutterOptions.None;

            if (optLineNumbers)
            {
                g |= GutterOptions.LineNumbers;
            }

            if (optFoldIndicators)
            {
                g |= GutterOptions.Folding;
            }

            editor.GutterOptions = g;
        }

        // --- MenuBar ---

        MenuBar menu = new () { AlignmentModes = AlignmentModes.IgnoreFirstOrLast };

        filenameShortcut.Accepting += (_, _) => OpenFile ();

        menu.Add (new MenuBarItem ("_File",
        [
            new MenuItem { Title = "_New", Key = Key.N.WithCtrl, Action = NewFile },
            new MenuItem { Title = "_Open", Key = Key.O.WithCtrl, Action = OpenFile },
            new MenuItem { Title = "_Save", Key = Key.S.WithCtrl, Action = () => SaveFile () },
            new MenuItem { Title = "Save _As", Action = () => SaveAs () },
            null!,
            new MenuItem { Title = "_Find...", Key = Key.F.WithCtrl, Action = () => ShowFindReplace () },
            new MenuItem { Title = "_Replace...", Key = Key.H.WithCtrl, Action = () => ShowFindReplace (true) },
            null!,
            new MenuItem { Title = "_Quit", Key = Key.Q.WithCtrl, Action = QuitEditor },
        ]));

        menu.Add (new MenuBarItem ("_Edit", CreateEditMenuItems ()));

        // Options menu items with toggle titles
        MenuItem optLineNumbersItem = new () { Title = "✓ _Line Numbers" };
        MenuItem optFoldIndicatorsItem = new () { Title = "✓ _Fold Indicators" };
        MenuItem optConvertTabsItem = new () { Title = "✓ _Convert Tabs To Spaces" };
        MenuItem optAutoIndentItem = new () { Title = "  _Auto Indent" };
        MenuItem optUseThemeBgItem = new () { Title = "  Use _Theme Background" };
        MenuItem optWordWrapItem = new () { Title = "  _Word Wrap" };

        string ToggleTitle (bool on, string label) => on ? $"✓ {label}" : $"  {label}";

        optLineNumbersItem.Action = () =>
        {
            optLineNumbers = !optLineNumbers;
            optLineNumbersItem.Title = ToggleTitle (optLineNumbers, "_Line Numbers");
            UpdateGutterOptions ();
        };

        optFoldIndicatorsItem.Action = () =>
        {
            optFoldIndicators = !optFoldIndicators;
            optFoldIndicatorsItem.Title = ToggleTitle (optFoldIndicators, "_Fold Indicators");
            UpdateGutterOptions ();
        };

        optConvertTabsItem.Action = () =>
        {
            optConvertTabs = !optConvertTabs;
            optConvertTabsItem.Title = ToggleTitle (optConvertTabs, "_Convert Tabs To Spaces");
            editor.ConvertTabsToSpaces = optConvertTabs;
        };

        optAutoIndentItem.Action = () =>
        {
            optAutoIndent = !optAutoIndent;
            optAutoIndentItem.Title = ToggleTitle (optAutoIndent, "_Auto Indent");
            editor.IndentationStrategy = optAutoIndent ? new DefaultIndentationStrategy () : null;
        };

        optUseThemeBgItem.Action = () =>
        {
            optUseThemeBg = !optUseThemeBg;
            optUseThemeBgItem.Title = ToggleTitle (optUseThemeBg, "Use _Theme Background");
            editor.UseThemeBackground = optUseThemeBg;
        };

        optWordWrapItem.Action = () =>
        {
            optWordWrap = !optWordWrap;
            optWordWrapItem.Title = ToggleTitle (optWordWrap, "_Word Wrap");
            editor.WordWrap = optWordWrap;
        };

        menu.Add (new MenuBarItem ("_Options",
        [
            optLineNumbersItem,
            optFoldIndicatorsItem,
            optConvertTabsItem,
            optAutoIndentItem,
            optUseThemeBgItem,
            optWordWrapItem,
        ]));

        menu.Add (new MenuBarItem ("_Help",
        [
            new MenuItem { Title = "_About", Action = ShowAbout },
        ]));

        // --- Right-click context menu ---

        editor.MouseEvent += (_, e) =>
        {
            if (!e.Flags.HasFlag (MouseFlags.RightButtonClicked))
            {
                return;
            }

            PopoverMenu contextMenu = new (CreateEditMenuItems ());
            contextMenu.Visible = true;
        };

        // --- Wire find/replace events ---

        editor.FindRequested += (_, _) => ShowFindReplace ();
        editor.ReplaceRequested += (_, _) => ShowFindReplace (true);

        // --- Wire events ---

        editor.CaretChanged += (_, _) =>
        {
            UpdateModifiedIndicator ();
            UpdateLocShortcut ();
        };

        // --- StatusBar ---

        NumericUpDown<int> indentSpinner = new () { Value = editor.IndentationSize, Width = 5 };
        indentSpinner.ValueChanged += (_, e) => editor.IndentationSize = e.NewValue;

        CheckBox showTabsCheck = new () { Title = "↹", Value = CheckState.UnChecked };
        showTabsCheck.ValueChanged += (_, e) =>
            editor.ShowTabs = e.NewValue == CheckState.Checked;

        previewCheckBox = new () { Title = "Preview", Value = CheckState.UnChecked, Visible = isMarkdownFile };
        previewCheckBox.ValueChanged += (_, _) => UpdatePreviewVisibility ();

        List<Shortcut> statusItems =
        [
            new Shortcut (Application.GetDefaultKey (Command.Quit), "Quit", QuitEditor),
            new Shortcut (Key.F2, "Open", OpenFile),
            new Shortcut (Key.F3, "Save", () => SaveFile ()),
            modifiedShortcut,
            cursorPositionShortcut,
            languageShortcut,
            new () { CommandView = indentSpinner, HelpText = "Indent" },
            new () { CommandView = showTabsCheck, HelpText = "" },
            new () { CommandView = previewCheckBox, HelpText = "" },
            filenameShortcut,
        ];

        // File selector: dropdown when multiple files, plain label otherwise
        DropDownList? fileSelector = null;

        if (files.Count > 1)
        {
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
                Text = displayNames[0],
                Width = Dim.Auto (DimAutoStyle.Text, minimumContentDim: 20),
            };

            bool switchingFile = false;

            fileSelector.ValueChanged += (_, _) =>
            {
                if (switchingFile)
                {
                    return;
                }

                int index = displayNames.IndexOf (fileSelector.Text);

                if (index < 0 || index >= files.Count)
                {
                    return;
                }

                if (!PromptSaveIfDirty ())
                {
                    switchingFile = true;
                    int currentIndex = filePath is not null ? files.IndexOf (filePath) : -1;

                    if (currentIndex >= 0)
                    {
                        fileSelector.Text = displayNames[currentIndex];
                    }

                    switchingFile = false;

                    return;
                }

                LoadFile (files[index]);
            };

            statusItems.Add (new Shortcut () { CommandView = fileSelector, HelpText = "File" });
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
                savedText = string.Empty;
                editor.Document = new TextDocument ();
                InstallFolding ();
                UpdateModifiedIndicator ();
            }
            else if (content is not null)
            {
                editor.Document = new TextDocument (content);
                savedText = string.Empty;
                InstallFolding ();
                UpdateModifiedIndicator ();
            }

            UpdateLanguageShortcut ();
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
