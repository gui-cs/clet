using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TextMateSharp.Grammars;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class MarkdownClet : IViewerClet
{
    /// <summary>8 M character cap on stdin content to prevent OOM from untrusted piped input.</summary>
    internal const int MaxStdinChars = 8 * 1024 * 1024;

    public string PrimaryAlias => "md";
    public IReadOnlyList<string> Aliases => ["md", "markdown"];
    public string Description => "Renders Markdown files in a themed, scrollable viewer.";
    public CletKind Kind => CletKind.Viewer;
    public Type ResultType => typeof (void);

    public IReadOnlyList<CletOptionDescriptor> Options =>
    [
        new ("theme", "t", typeof (string),
            $"Syntax-highlighting theme. Available: {string.Join (", ", Enum.GetNames<ThemeName> ())}",
            false, nameof (ThemeName.DarkPlus)),
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

        // Resolve content: file args → inline content → stdin → error
        List<string> files = [];

        if (options.Arguments is { Count: > 0 })
        {
            files = ExpandFiles (options.Arguments);

            if (files.Count == 0)
            {
                return new () { Status = CletRunStatus.Error, ErrorCode = "io", ErrorMessage = "No matching files found." };
            }
        }
        else if (!string.IsNullOrEmpty (content))
        {
            // Inline content via --initial; render directly
        }
        else if (Console.IsInputRedirected)
        {
            // Read stdin with an 8 M character cap to prevent OOM
            char[] buffer = new char[MaxStdinChars + 1];
            int totalRead = 0;
            int charsRead;

            while (totalRead <= MaxStdinChars
                   && (charsRead = Console.In.Read (buffer, totalRead, buffer.Length - totalRead)) > 0)
            {
                totalRead += charsRead;
            }

            if (totalRead > MaxStdinChars)
            {
                return new ()
                {
                    Status = CletRunStatus.Error,
                    ErrorCode = "input-too-large",
                    ErrorMessage = $"stdin exceeds the 8 M character limit.",
                };
            }

            content = new string (buffer, 0, totalRead);

            if (string.IsNullOrEmpty (content))
            {
                return new () { Status = CletRunStatus.Error, ErrorCode = "io", ErrorMessage = "No input received from stdin." };
            }
        }
        else
        {
            return new () { Status = CletRunStatus.Error, ErrorCode = "io", ErrorMessage = "No file specified. Usage: clet md <file.md>" };
        }

        // Parse --theme option
        ThemeName syntaxTheme = ThemeName.DarkPlus;

        if (options.CletOptions?.TryGetValue ("theme", out string? themeStr) == true
            && Enum.TryParse (themeStr, ignoreCase: true, out ThemeName parsed))
        {
            syntaxTheme = parsed;
        }

        Runnable window = new ()
        {
            Title = options.Title ?? "Markdown Viewer",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        Markdown markdownView = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (1), // leave room for StatusBar
            SyntaxHighlighter = new TextMateSyntaxHighlighter (syntaxTheme),
        };

        markdownView.ViewportSettings |= ViewportSettingsFlags.HasHorizontalScrollBar;

        // --- StatusBar items ---

        Shortcut lineCountShortcut = new () { Title = "0 lines", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut fileSizeShortcut = new () { Title = "0 B", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut statusShortcut = new (Key.Empty, "Ready", null);

        // --- MarkdownView event wiring ---

        markdownView.LinkClicked += (_, e) =>
        {
            statusShortcut.Title = e.Url;
            e.Handled = true;
        };

        markdownView.SubViewsLaidOut += (_, _) =>
        {
            lineCountShortcut.Title = $"{markdownView.LineCount} lines";
        };

        // --- Build StatusBar ---

        List<Shortcut> statusItems =
        [
            new (Application.GetDefaultKey (Command.Quit), "Quit", window.RequestStop),
        ];

        // Theme selector
        DropDownList<ThemeName> themeDropDown = new () { Value = syntaxTheme, CanFocus = false };

        themeDropDown.ValueChanged += (_, e) =>
        {
            if (e.Value is not { } themeName)
            {
                return;
            }

            markdownView.SyntaxHighlighter = new TextMateSyntaxHighlighter (themeName);
        };

        statusItems.Add (new Shortcut { Title = "Theme", CommandView = themeDropDown });

        // Auto-select light or dark syntax theme based on terminal background
        app.Driver!.DefaultAttributeChanged += (_, e) =>
        {
            if (e.NewValue is not { } attr)
            {
                return;
            }

            ThemeName autoTheme = TextMateSyntaxHighlighter.GetThemeForBackground (attr.Background);
            markdownView.SyntaxHighlighter = new TextMateSyntaxHighlighter (autoTheme);
            themeDropDown.Value = autoTheme;
        };

        // Theme background toggle
        CheckBox themeBgCheckBox = new ()
        {
            Text = "Theme _BG",
            Value = markdownView.UseThemeBackground ? CheckState.Checked : CheckState.UnChecked,
        };

        themeBgCheckBox.ValueChanged += (_, e) =>
        {
            markdownView.UseThemeBackground = e.NewValue == CheckState.Checked;
        };

        statusItems.Add (new Shortcut { CommandView = themeBgCheckBox });
        statusItems.AddRange ([lineCountShortcut, fileSizeShortcut, statusShortcut]);

        // File selector when multiple files are provided
        if (files.Count > 1)
        {
            List<string?> fileNames = [.. files.Select (Path.GetFileName)];
            ObservableCollection<string> fileNamesOc = new (fileNames!);

            DropDownList fileSelector = new ()
            {
                Source = new ListWrapper<string> (fileNamesOc),
                ReadOnly = true,
                Text = fileNames [0] ?? string.Empty,
                Width = 30,
            };

            fileSelector.ValueChanged += (_, _) =>
            {
                string selectedName = fileSelector.Text;
                int index = fileNames.IndexOf (selectedName);

                if (index < 0 || index >= files.Count)
                {
                    return;
                }

                LoadFile (files [index]);
            };

            Shortcut fileSelectorShortcut = new () { CommandView = fileSelector, HelpText = "File" };
            statusItems.Insert (1, fileSelectorShortcut);
        }

        StatusBar statusBar = new (statusItems) { AlignmentModes = AlignmentModes.IgnoreFirstOrLast };

        window.Add (markdownView, statusBar);

        // Load content after initial layout
        window.Initialized += (_, _) =>
        {
            if (files.Count > 0)
            {
                LoadFile (files [0]);
            }
            else if (!string.IsNullOrEmpty (content))
            {
                markdownView.Text = content;
                statusShortcut.Title = "(inline)";
            }

        };

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

        void LoadFile (string filePath)
        {
            string fileContent = File.ReadAllText (filePath);
            markdownView.Text = fileContent;

            FileInfo fileInfo = new (filePath);
            fileSizeShortcut.Title = FormatFileSize (fileInfo.Length);
            statusShortcut.Title = Path.GetFileName (filePath);
        }
    }

    private static List<string> ExpandFiles (IReadOnlyList<string> patterns)
    {
        List<string> result = [];

        foreach (string pattern in patterns)
        {
            if (pattern.Contains ('*') || pattern.Contains ('?'))
            {
                string directory = Path.GetDirectoryName (pattern) is { Length: > 0 } dir ? dir : ".";
                string filePattern = Path.GetFileName (pattern);

                if (Directory.Exists (directory))
                {
                    result.AddRange (Directory.GetFiles (directory, filePattern));
                }
            }
            else if (File.Exists (pattern))
            {
                result.Add (Path.GetFullPath (pattern));
            }
            else
            {
                Console.Error.WriteLine ($"Warning: File not found: {pattern}");
            }
        }

        return result;
    }

    private static string FormatFileSize (long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes [order]}";
    }
}
