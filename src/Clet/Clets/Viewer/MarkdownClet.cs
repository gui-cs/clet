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
        new ("cat", null, typeof (bool),
            "Render markdown to stdout without launching the TUI viewer.",
            false, "false"),
        new ("no-browse", null, typeof (bool),
            "Disable browser mode (back/forward navigation, top bar).",
            false, "false"),
    ];

    public bool AcceptsPositionalArgs => true;

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
            FileAccessPolicy policy = new (
                Directory.GetCurrentDirectory (),
                options.AllowedFiles,
                options.AllowBinary);

            files = ExpandFiles (options.Arguments, policy, out string? policyError);

            if (policyError is not null)
            {
                return new () { Status = CletRunStatus.Error, ErrorCode = "file-access-denied", ErrorMessage = policyError };
            }

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

        // Track current file directory for resolving relative links
        string? currentFileDir = files.Count > 0 ? Path.GetDirectoryName (Path.GetFullPath (files [0])) : null;

        // File access policy for link navigation (reuse the same confinement as file loading)
        FileAccessPolicy linkPolicy = new (
            Directory.GetCurrentDirectory (),
            options.AllowedFiles,
            options.AllowBinary);

        // Browser mode
        bool browseMode = !options.NoBrowse;
        string? currentFile = files.Count > 0 ? Path.GetFullPath (files [0]) : null;
        BrowseBar? browseBar = null;

        // Parse --theme option
        ThemeName syntaxTheme = ThemeName.DarkPlus;

        if (options.CletOptions?.TryGetValue ("theme", out string? themeStr) == true
            && Enum.TryParse (themeStr, ignoreCase: true, out ThemeName parsed))
        {
            syntaxTheme = parsed;
        }

        Runnable window = new ()
        {
            Title = options.Title ?? (browseMode ? "Markdown Browser" : "Markdown Viewer"),
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

        // --- StatusBar items (declared early so local functions can capture them) ---

        Shortcut lineCountShortcut = new () { Title = "0 lines", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut fileSizeShortcut = new () { Title = "0 B", MouseHighlightStates = MouseState.None, Enabled = false };

        // Status link — shows the current filename or a clickable URL when the user
        // hovers/clicks a hyperlink in the markdown. Clicking the link in the status
        // bar opens it in the default browser.
        Link statusLink = new () { Text = "Ready", CanFocus = false };
        Shortcut statusShortcut = new () { CommandView = statusLink, MouseHighlightStates = MouseState.None };

        // --- Top bar (browser mode) ---
        if (browseMode)
        {
            string initialLocation = currentFile is not null ? GetRelativeBreadcrumb (currentFile) : "(inline)";
            browseBar = new BrowseBar (initialLocation);
            browseBar.OnNavigate = path => LoadFile (path);

            markdownView.Y = 1;
            window.Add (browseBar.Bar);
        }

        // --- MarkdownView event wiring ---

        markdownView.LinkClicked += (_, e) =>
        {
            if (browseMode)
            {
                // Navigate local .md files within the sandbox
                if (currentFileDir is not null && TryResolveLocalMarkdownLink (e.Url, currentFileDir, linkPolicy, out string? resolvedPath, out string? fragment))
                {
                    browseBar!.Push (resolvedPath);
                    LoadFile (resolvedPath, fragment);
                    e.Handled = true;

                    return;
                }
            }

            // Open http/https links in the default browser — they're safe
            if (e.Url.StartsWith ("http://", StringComparison.OrdinalIgnoreCase)
                || e.Url.StartsWith ("https://", StringComparison.OrdinalIgnoreCase))
            {
                Link.OpenUrl (e.Url);
            }

            // Show URL in status bar as a clickable link
            statusLink.Text = e.Url;
            statusLink.Url = e.Url;
            statusShortcut.MouseHighlightStates = MouseState.In;
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

                browseBar?.Push (files [index]);
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
                string sanitized = TerminalEscapeSanitizer.Sanitize (content)!;
                markdownView.Text = sanitized;
                fileSizeShortcut.Title = FormatFileSize (System.Text.Encoding.UTF8.GetByteCount (sanitized));
                statusLink.Text = options.Title ?? "(inline)";
                statusLink.Url = string.Empty;
                statusShortcut.MouseHighlightStates = MouseState.None;
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

        void LoadFile (string filePath, string? fragment = null)
        {
            string fullPath = Path.GetFullPath (filePath);

            string fileContent = TerminalEscapeSanitizer.Sanitize (File.ReadAllText (fullPath))!;
            markdownView.Text = fileContent;

            currentFile = fullPath;
            currentFileDir = Path.GetDirectoryName (fullPath);

            FileInfo fileInfo = new (fullPath);
            fileSizeShortcut.Title = FormatFileSize (fileInfo.Length);
            statusLink.Text = Path.GetFileName (fullPath);
            statusLink.Url = string.Empty;

            browseBar?.SetLocationTitle (GetRelativeBreadcrumb (fullPath));

            if (!string.IsNullOrEmpty (fragment))
            {
                markdownView.ScrollToAnchor (fragment);
            }
        }

        string GetRelativeBreadcrumb (string fullPath)
        {
            string cwd = Directory.GetCurrentDirectory ();

            return Path.GetRelativePath (cwd, fullPath);
        }
    }

    /// <summary>
    /// Resolves a link URL to a local markdown file path if it passes the file access policy.
    /// </summary>
    internal static bool TryResolveLocalMarkdownLink (
        string url,
        string currentDir,
        FileAccessPolicy policy,
        out string? resolvedPath,
        out string? fragment)
    {
        resolvedPath = null;
        fragment = null;

        // Extract fragment (e.g. #section) before resolving the path
        int fragmentIndex = url.IndexOf ('#');
        string pathPart = fragmentIndex >= 0 ? url [..fragmentIndex] : url;

        if (fragmentIndex >= 0)
        {
            fragment = url [(fragmentIndex + 1)..];
        }

        if (string.IsNullOrWhiteSpace (pathPart))
        {
            return false;
        }

        // Handle file:// URIs
        if (pathPart.StartsWith ("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate (pathPart, UriKind.Absolute, out Uri? fileUri) || !fileUri.IsFile)
            {
                return false;
            }

            pathPart = fileUri.LocalPath;
        }
        // Reject non-local schemes (http://, https://, mailto:, etc.)
        else if (pathPart.Contains ("://", StringComparison.Ordinal))
        {
            return false;
        }

        string fullPath;

        try
        {
            fullPath = Path.IsPathRooted (pathPart)
                ? Path.GetFullPath (pathPart)
                : Path.GetFullPath (Path.Combine (currentDir, pathPart));
        }
        catch
        {
            return false;
        }

        if (!File.Exists (fullPath))
        {
            return false;
        }

        // Delegate all security checks (extension, cwd confinement, binary, size) to the policy
        if (policy.CheckFile (fullPath) is not null)
        {
            return false;
        }

        resolvedPath = fullPath;

        return true;
    }

    private static List<string> ExpandFiles (IReadOnlyList<string> patterns, FileAccessPolicy policy, out string? error)
    {
        List<string> result = [];
        error = null;

        foreach (string pattern in patterns)
        {
            if (pattern.Contains ('*') || pattern.Contains ('?'))
            {
                string directory = Path.GetDirectoryName (pattern) is { Length: > 0 } dir ? dir : ".";
                string filePattern = Path.GetFileName (pattern);

                if (Directory.Exists (directory))
                {
                    string[] matched = Directory.GetFiles (directory, filePattern);
                    string? globError = policy.CheckGlobAggregate (matched);

                    if (globError is not null)
                    {
                        error = globError;

                        return [];
                    }

                    foreach (string file in matched)
                    {
                        string? violation = policy.CheckFile (file);

                        if (violation is not null)
                        {
                            error = violation;

                            return [];
                        }

                        result.Add (Path.GetFullPath (file));
                    }
                }
            }
            else if (File.Exists (pattern))
            {
                string? violation = policy.CheckFile (pattern);

                if (violation is not null)
                {
                    error = violation;

                    return [];
                }

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
