using System.Text.Json;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Command = Terminal.Gui.Input.Command;

namespace Clet;

internal sealed class ConfigClet : IViewerClet
{
    /// <summary>The config file name inside ~/.tui/.</summary>
    internal const string ConfigFileName = "clet.config.json";

    public string PrimaryAlias => "config";
    public IReadOnlyList<string> Aliases => ["config"];
    public string Description => "Edit the clet configuration file (~/.tui/clet.config.json).";
    public CletKind Kind => CletKind.Viewer;
    public Type ResultType => typeof (void);

    public IReadOnlyList<CletOptionDescriptor> Options => [];

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

        string configPath = GetConfigPath ();
        EnsureConfigFile (configPath);

        string configText = File.ReadAllText (configPath);

        // Check for pre-existing config errors to show on launch
        string? launchError = ValidateConfig (configPath);

        bool isDirty = false;
        Shortcut statusMessage = new () { Title = "Ready", MouseHighlightStates = MouseState.None, Enabled = false };
        Shortcut cursorPosition = new () { Title = "Ln 1, Col 1", MouseHighlightStates = MouseState.None, Enabled = false };

        Runnable window = new ()
        {
            Title = options.Title ?? $"clet config — {configPath}",
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        TextView editor = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (1), // leave room for StatusBar
            Text = configText,
            Multiline = true,
            TabWidth = 2,
        };

        editor.ContentsChanged += (_, _) =>
        {
            isDirty = true;
            UpdateTitle ();
        };

        editor.UnwrappedCursorPositionChanged += (_, _) =>
        {
            cursorPosition.Title = $"Ln {editor.CurrentRow + 1}, Col {editor.CurrentColumn + 1}";
        };

        // --- StatusBar ---

        Shortcut saveShortcut = new (Key.S.WithCtrl, "Save", () => Save ());
        Shortcut quitShortcut = new (Application.GetDefaultKey (Command.Quit), "Quit", () => TryQuit ());

        StatusBar statusBar = new ([quitShortcut, saveShortcut, statusMessage, cursorPosition])
        {
            AlignmentModes = AlignmentModes.IgnoreFirstOrLast,
        };

        window.Add (editor, statusBar);

        window.Initialized += (_, _) =>
        {
            editor.SetFocus ();

            if (launchError is not null)
            {
                MessageBox.ErrorQuery (
                    app,
                    "Configuration Error",
                    launchError,
                    "OK");

                statusMessage.Title = "Config has errors";
            }
        };

        // Override Quit to prompt on unsaved changes
        window.KeyDown += (_, e) =>
        {
            if (e.KeyCode == (Application.GetDefaultKey (Command.Quit).KeyCode))
            {
                e.Handled = true;
                TryQuit ();
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

        void UpdateTitle ()
        {
            string marker = isDirty ? " •" : "";
            window.Title = $"clet config — {configPath}{marker}";
        }

        void Save ()
        {
            Logging.Information ("ConfigClet: Save triggered");

            try
            {
                File.WriteAllText (configPath, editor.Text);
                isDirty = false;
                UpdateTitle ();
                Logging.Information ("ConfigClet: file written successfully");
            }
            catch (Exception ex)
            {
                Logging.Error ($"ConfigClet: file write failed: {ex.Message}");
                statusMessage.Title = $"Save failed: {ex.Message}";

                return;
            }

            // Reload config — catch ALL exceptions since Apply can throw
            // KeyNotFoundException (bad theme), JsonException (bad syntax), etc.
            try
            {
                ConfigurationManager.ThrowOnJsonErrors = true;
                ConfigurationManager.Load (ConfigLocations.All);
                ConfigurationManager.Apply ();
                Logging.Information ("ConfigClet: config reloaded and applied successfully");
                statusMessage.Title = "Saved ✓";
            }
            catch (JsonException jsonEx)
            {
                Logging.Error ($"ConfigClet: config reload threw JsonException: {jsonEx.Message}");
                ResetConfigToDefaults ();
                ShowJsonErrorDialog (jsonEx);
                statusMessage.Title = "Saved with errors";
            }
            catch (Exception applyEx)
            {
                Logging.Error ($"ConfigClet: config reload threw {applyEx.GetType ().Name}: {applyEx.Message}");
                ResetConfigToDefaults ();
                ShowConfigErrorDialog (applyEx);
                statusMessage.Title = "Saved with errors";
            }
            finally
            {
                ConfigurationManager.ThrowOnJsonErrors = false;
            }
        }

        void ShowJsonErrorDialog (JsonException ex)
        {
            // Extract line number (0-based in JsonException, convert to 0-based row for TextView)
            int errorRow = ex.LineNumber.HasValue ? (int)ex.LineNumber.Value : 0;
            int errorCol = ex.BytePositionInLine.HasValue ? (int)ex.BytePositionInLine.Value : 0;

            string details = ex.Message;

            // Strip the redundant " Path: ... | LineNumber: ... | ..." suffix that JsonException adds
            int pathIdx = details.IndexOf (" Path:", StringComparison.Ordinal);

            if (pathIdx > 0)
            {
                details = details [..pathIdx];
            }

            string message = $"{details}\n\nLine {errorRow + 1}, Column {errorCol + 1}";

            MessageBox.ErrorQuery (
                app,
                "Configuration Error",
                message,
                "Go to Error");

            // Navigate the cursor to the error location
            editor.MoveHome ();
            editor.InsertionPoint = new System.Drawing.Point (errorCol, errorRow);
            editor.SetFocus ();
        }

        void ShowConfigErrorDialog (Exception ex)
        {
            MessageBox.ErrorQuery (
                app,
                "Configuration Error",
                ex.Message,
                "OK");

            editor.SetFocus ();
        }

        void TryQuit ()
        {
            if (isDirty)
            {
                int? result = MessageBox.Query (
                    app,
                    "Unsaved Changes",
                    "You have unsaved changes. Save before quitting?",
                    "Save & Quit",
                    "Discard",
                    "Cancel");

                switch (result)
                {
                    case 0:
                        Save ();
                        window.RequestStop ();

                        break;
                    case 1:
                        window.RequestStop ();

                        break;
                    default:
                        // Cancel — do nothing
                        break;
                }
            }
            else
            {
                window.RequestStop ();
            }
        }
    }

    /// <summary>Returns the path to <c>~/.tui/clet.config.json</c>.</summary>
    internal static string GetConfigPath ()
    {
        string home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

        return Path.Combine (home, ".tui", ConfigFileName);
    }

    /// <summary>Creates the config file with annotated defaults if it doesn't already exist.</summary>
    internal static void EnsureConfigFile (string configPath)
    {
        if (File.Exists (configPath))
        {
            return;
        }

        string? dir = Path.GetDirectoryName (configPath);

        if (dir is not null && !Directory.Exists (dir))
        {
            Directory.CreateDirectory (dir);
        }

        File.WriteAllText (configPath, DefaultConfigContent);
    }

    /// <summary>
    /// Validates the config by attempting a Load + Apply cycle.
    /// Returns an error message if something is wrong, or null if valid.
    /// On error, resets ConfigurationManager to hard-coded defaults so the UI can still render.
    /// </summary>
    internal static string? ValidateConfig (string configPath)
    {
        if (!File.Exists (configPath))
        {
            return null;
        }

        try
        {
            ConfigurationManager.ThrowOnJsonErrors = true;
            ConfigurationManager.Load (ConfigLocations.All);
            ConfigurationManager.Apply ();

            return null;
        }
        catch (JsonException ex)
        {
            // Reset to safe defaults so the UI can render
            ResetConfigToDefaults ();

            int line = ex.LineNumber.HasValue ? (int)ex.LineNumber.Value + 1 : 0;
            int col = ex.BytePositionInLine.HasValue ? (int)ex.BytePositionInLine.Value + 1 : 0;

            string details = ex.Message;
            int pathIdx = details.IndexOf (" Path:", StringComparison.Ordinal);

            if (pathIdx > 0)
            {
                details = details [..pathIdx];
            }

            return $"{details}\n\nLine {line}, Column {col}";
        }
        catch (Exception ex)
        {
            // Reset to safe defaults so the UI can render
            ResetConfigToDefaults ();

            return ex.Message;
        }
        finally
        {
            ConfigurationManager.ThrowOnJsonErrors = false;
        }
    }

    /// <summary>Resets ConfigurationManager to hard-coded defaults after a bad config poisons global state.</summary>
    private static void ResetConfigToDefaults ()
    {
        try
        {
            ConfigurationManager.ThrowOnJsonErrors = false;
            ConfigurationManager.Load (ConfigLocations.HardCoded);
            ConfigurationManager.Apply ();
        }
        catch
        {
            // Best-effort reset — if even this fails, the UI will use whatever state remains.
        }
    }

    /// <summary>
    /// Annotated default config content that explains common settings.
    /// JSON with comments (JSONC) — Terminal.Gui's ConfigurationManager supports // comments.
    /// </summary>
    internal const string DefaultConfigContent =
        """
        {
          // ═══════════════════════════════════════════════════════════════════════
          //  clet configuration — ~/.tui/clet.config.json
          //
          //  This file configures Terminal.Gui settings for the `clet` tool.
          //  Terminal.Gui's ConfigurationManager loads this automatically.
          //
          //  Edit and save (Ctrl+S) to apply changes live.
          //  See: https://gui-cs.github.io/Terminal.Gui/docs/config.html
          //  Schema: https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json
          // ═══════════════════════════════════════════════════════════════════════

          "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",

          // ─── General Settings ─────────────────────────────────────────────────

          // Separator character for key bindings displayed in the UI (e.g. "Ctrl+S")
          // "Key.Separator": "+",

          // Set to true to force 16-color mode (useful for minimal terminal emulators)
          // "Driver.Force16Colors": false,

          // Set to true to disable mouse support entirely
          // "Application.IsMouseDisabled": false,

          // ─── Key Bindings ─────────────────────────────────────────────────────
          //
          // Key bindings can be customized per-view or globally. Common examples:
          //
          //   "PopoverMenu.DefaultKey": "Shift+F10",
          //
          // Key names follow the pattern: Ctrl+<key>, Alt+<key>, Shift+<key>, F1–F12
          // Multiple modifiers: "Ctrl+Shift+S"
          //
          // See the schema reference for the full list of bindable commands.

          // "PopoverMenu.DefaultKey": "Shift+F10",

          // ─── Themes ───────────────────────────────────────────────────────────
          //
          // Terminal.Gui ships with a Default theme (based on terminal colors) and
          // supports custom themes. Each theme defines color schemes for different
          // UI elements: Base, Dialog, Menu, Error, and Accent.
          //
          // Built-in themes:
          //   "Default", "Dark", "Light", "Anders", "TurboPascal 5",
          //   "Green Phosphor", "Amber Phosphor", "8-Bit"
          //
          // Set the active theme:
          // "Theme": "Anders",
          //
          // A color scheme has these states:
          //   Normal    — default appearance
          //   Focus     — when the view has keyboard focus
          //   HotNormal — hot-key character in normal state
          //   HotFocus  — hot-key character when focused
          //   Disabled  — when the view is disabled
          //
          // Color values can be:
          //   Named:  "Black", "Blue", "Green", "Cyan", "Red", "Magenta",
          //           "Yellow", "White", "BrightBlue", "BrightGreen", etc.
          //   RGB:    "#FF8800" (hex), "rgb(255,136,0)"
          //
          // Example custom theme (uncomment and modify):

          // "Themes": [
          //   {
          //     "MyCustomTheme": {
          //       "Schemes": [
          //         {
          //           "Base": {
          //             "Normal": {
          //               "Foreground": "White",
          //               "Background": "DarkBlue"
          //             },
          //             "Focus": {
          //               "Foreground": "BrightYellow",
          //               "Background": "Blue"
          //             },
          //             "HotNormal": {
          //               "Foreground": "BrightCyan",
          //               "Background": "DarkBlue"
          //             },
          //             "HotFocus": {
          //               "Foreground": "BrightCyan",
          //               "Background": "Blue"
          //             },
          //             "Disabled": {
          //               "Foreground": "DarkGray",
          //               "Background": "DarkBlue"
          //             }
          //           }
          //         },
          //         {
          //           "Dialog": {
          //             "Normal": {
          //               "Foreground": "Black",
          //               "Background": "LightGray"
          //             }
          //           }
          //         },
          //         {
          //           "Menu": {
          //             "Normal": {
          //               "Foreground": "White",
          //               "Background": "DarkCyan"
          //             }
          //           }
          //         },
          //         {
          //           "Error": {
          //             "Normal": {
          //               "Foreground": "BrightRed",
          //               "Background": "Black"
          //             }
          //           }
          //         }
          //       ]
          //     }
          //   }
          // ]

          // ─── Tracing (for debugging) ──────────────────────────────────────────
          //
          // Enable trace categories to debug Terminal.Gui internals.
          // Useful values: "Lifecycle", "Drawing", "Layout", "Mouse", "Keyboard"
          //
          // "Trace.EnabledCategories": "Lifecycle"
        }
        """;
}
