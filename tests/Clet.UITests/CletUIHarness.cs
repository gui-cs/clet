using System.Drawing;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Clet.UITests;

/// <summary>
///     Frame-stepped, in-process UI test harness for clets.
///     See <c>tests/SPEC.md</c> §2.3 and §3.2 for design.
/// </summary>
/// <remarks>
///     <para>
///         The harness runs a clet's <c>RunAsync</c> on a thread-pool task and lets the test thread
///         drive input + capture snapshots cooperatively via the <see cref="IApplication.Iteration"/>
///         event. There is no background semaphore-driven loop, no <c>Task.Delay</c>, and no
///         wall-clock waits — the test thread owns the clock.
///     </para>
///     <para>
///         Each <see cref="PressAsync"/> / <see cref="ClickAsync"/> call injects the event and then
///         awaits one full iteration before returning, so the next snapshot reflects the post-input,
///         post-redraw state.
///     </para>
/// </remarks>
internal sealed class CletUIHarness<T> : IAsyncDisposable
{
    private readonly IApplication _app;
    private readonly CancellationTokenSource _cts;
    private readonly Task<CletRunResult<T>> _cletTask;

    // TCS swapped in by callers that want to await the next iteration. The Iteration handler
    // takes whatever's there at fire-time and completes it. Null between awaits.
    private TaskCompletionSource? _nextIteration;

    private CletUIHarness (IApplication app, CancellationTokenSource cts, Task<CletRunResult<T>> cletTask)
    {
        _app = app;
        _cts = cts;
        _cletTask = cletTask;
    }

    /// <summary>Start a harness for the given input clet. Returns once the first iteration has rendered.</summary>
    public static Task<CletUIHarness<T>> StartAsync (
        IClet<T> clet,
        string? initial = null,
        CletRunOptions? options = null,
        int width = 60,
        int height = 10)
        => StartCoreAsync (
            (app, ct) => clet.RunAsync (app, initial, options ?? new CletRunOptions (), ct),
            width, height);

    /// <summary>Start a harness for a viewer clet. Result.Value is always default(T) for viewers.</summary>
    public static Task<CletUIHarness<T>> StartViewerAsync (
        IViewerClet viewer,
        string? initial = null,
        CletRunOptions? options = null,
        int width = 60,
        int height = 15)
        => StartCoreAsync (
            async (app, ct) =>
            {
                CletRunResult result = await viewer.RunAsync (app, initial, options ?? new CletRunOptions (), ct);
                return new CletRunResult<T>
                {
                    Status = result.Status,
                    ErrorCode = result.ErrorCode,
                    ErrorMessage = result.ErrorMessage,
                };
            },
            width, height);

    private static async Task<CletUIHarness<T>> StartCoreAsync (
        Func<IApplication, CancellationToken, Task<CletRunResult<T>>> run,
        int width,
        int height)
    {
        IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        app.Driver?.SetScreenSize (width, height);

        CancellationTokenSource cts = new ();

        // Iteration event handler — completes whatever TCS is currently set in `pending`.
        TaskCompletionSource? pending = null;
        EventHandler<EventArgs<IApplication?>>? handler = (_, _) =>
        {
            TaskCompletionSource? tcs = Interlocked.Exchange (ref pending, null);
            tcs?.TrySetResult ();
        };
        app.Iteration += handler;

        // Run the clet on a background task — its app.RunAsync owns the loop thread.
        Task<CletRunResult<T>> task = Task.Run (async () => await run (app, cts.Token));

        // Wait until the View has actually been drawn (Driver.Contents has non-space cells).
        // The first Iteration event can fire before the layout/draw pass completes — Views that
        // depend on Dim.Fill() (TextField, Markdown) need a second pass before their content
        // shows up, while statically-sized Views (SelectClet's OptionSelector) render in one.
        // Waiting on "non-empty contents" is more robust than counting iterations.
        const int maxStartupIterations = 20;
        for (int i = 0; i < maxStartupIterations; i++)
        {
            TaskCompletionSource tcs = new (TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write (ref pending, tcs);

            // Race against task completion (e.g. pre-cancelled token returns immediately).
            await Task.WhenAny (tcs.Task, task);

            if (task.IsCompleted)
            {
                break;
            }

            if (HasNonSpaceContent (app.Driver?.Contents))
            {
                break;
            }
        }

        CletUIHarness<T> harness = new (app, cts, task);

        // Re-wire the handler to flow into the harness's _nextIteration field for subsequent waits.
        app.Iteration -= handler;
        app.Iteration += harness.OnIteration;

        return harness;
    }

    private static bool HasNonSpaceContent (Cell [,]? contents)
    {
        if (contents is null)
        {
            return false;
        }

        int rows = contents.GetLength (0);
        int cols = contents.GetLength (1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                string g = contents [r, c].Grapheme;
                if (!string.IsNullOrEmpty (g) && g != " ")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnIteration (object? sender, EventArgs<IApplication?> _)
    {
        TaskCompletionSource? tcs = Interlocked.Exchange (ref _nextIteration, null);
        tcs?.TrySetResult ();
    }

    private Task WaitForNextIterationAsync ()
    {
        TaskCompletionSource tcs = new (TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write (ref _nextIteration, tcs);
        return tcs.Task;
    }

    /// <summary>
    ///     Wait for either the next iteration or the clet to complete (whichever comes first).
    ///     Critical: keys that accept/cancel the clet make the loop exit, so no further iterations
    ///     fire — awaiting the iteration alone would deadlock. The race against <c>_cletTask</c>
    ///     unblocks the awaiter cleanly when that happens.
    /// </summary>
    private async Task WaitForIterationOrCompletionAsync ()
    {
        Task next = WaitForNextIterationAsync ();
        Task completed = await Task.WhenAny (next, _cletTask);

        if (completed == _cletTask)
        {
            // Loop exited; the pending TCS will never resolve naturally. Drain it.
            TaskCompletionSource? tcs = Interlocked.Exchange (ref _nextIteration, null);
            tcs?.TrySetResult ();
        }
    }

    /// <summary>Inject a key, then wait one iteration (or for the clet to complete).</summary>
    public async Task PressAsync (Key key)
    {
        _app.InjectKey (key);
        await WaitForIterationOrCompletionAsync ();
    }

    /// <summary>Inject a left click at the given screen position, then wait one iteration.</summary>
    public async Task ClickAsync (int x, int y)
    {
        _app.InjectMouse (new Mouse
        {
            ScreenPosition = new Point (x, y),
            Position = new Point (x, y),
            Flags = MouseFlags.LeftButtonClicked,
        });
        await WaitForIterationOrCompletionAsync ();
    }

    /// <summary>Pump iterations until <paramref name="predicate"/> is true, or the clet completes, with a hard cap.</summary>
    public async Task WaitForAsync (Func<IApplication, bool> predicate, int maxIterations = 50)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            if (predicate (_app) || _cletTask.IsCompleted)
            {
                return;
            }
            await WaitForIterationOrCompletionAsync ();
        }

        if (!predicate (_app) && !_cletTask.IsCompleted)
        {
            throw new TimeoutException ($"WaitForAsync exceeded {maxIterations} iterations.");
        }
    }

    /// <summary>The IApplication driving the clet. Use sparingly — prefer the harness API where possible.</summary>
    public IApplication App => _app;

    /// <summary>
    ///     Snapshot the current screen as plain text, one line per row, trailing whitespace trimmed
    ///     per row. Source: <c>app.Driver.Contents</c> (pre-clipping).
    /// </summary>
    public string SnapshotText ()
    {
        Cell [,]? contents = _app.Driver?.Contents;

        if (contents is null)
        {
            return string.Empty;
        }

        int rows = contents.GetLength (0);
        int cols = contents.GetLength (1);
        StringBuilder sb = new (rows * (cols + 1));

        for (int r = 0; r < rows; r++)
        {
            int lineStart = sb.Length;
            for (int c = 0; c < cols; c++)
            {
                string g = contents [r, c].Grapheme;
                sb.Append (string.IsNullOrEmpty (g) ? " " : g);
            }

            // Trim trailing whitespace from this row.
            int end = sb.Length;
            while (end > lineStart && char.IsWhiteSpace (sb [end - 1]))
            {
                end--;
            }
            sb.Length = end;
            sb.Append ('\n');
        }

        return sb.ToString ();
    }

    /// <summary>Snapshot the screen as raw <c>Cell[,]</c> for tests that need attribute/style assertions.</summary>
    public Cell [,]? SnapshotCells () => _app.Driver?.Contents;

    /// <summary>Asserts the given text appears starting at <paramref name="row"/>, <paramref name="col"/>.</summary>
    public void AssertCellsAt (int row, int col, string expected)
    {
        Cell [,]? contents = _app.Driver?.Contents
                             ?? throw new InvalidOperationException ("No driver contents available.");

        StringBuilder actual = new (expected.Length);
        int cols = contents.GetLength (1);

        for (int i = 0; i < expected.Length && col + i < cols; i++)
        {
            string g = contents [row, col + i].Grapheme;
            actual.Append (string.IsNullOrEmpty (g) ? " " : g);
        }

        Assert.Equal (expected, actual.ToString ());
    }

    /// <summary>
    ///     Compare the current text snapshot against a stored golden file under
    ///     <c>tests/Clet.UITests/Goldens/&lt;name&gt;</c>. Set <c>CLET_REGEN_GOLDENS=1</c> to rewrite mismatched
    ///     goldens in place; the test still fails on a regen so a regen is always a deliberate two-run
    ///     cycle.
    /// </summary>
    public void AssertMatchesGolden (string fileName)
    {
        string actual = SnapshotText ();
        string path = ResolveGoldenPath (fileName);
        bool regen = Environment.GetEnvironmentVariable ("CLET_REGEN_GOLDENS") == "1";

        if (!File.Exists (path))
        {
            if (regen)
            {
                Directory.CreateDirectory (Path.GetDirectoryName (path)!);
                File.WriteAllText (path, actual);
                Assert.Fail ($"Golden created at {path}. Re-run without CLET_REGEN_GOLDENS to verify.");
            }

            Assert.Fail ($"Golden not found: {path}. Run with CLET_REGEN_GOLDENS=1 to create it.");
        }

        string expected = File.ReadAllText (path).Replace ("\r\n", "\n");
        actual = actual.Replace ("\r\n", "\n");

        if (expected == actual)
        {
            return;
        }

        if (regen)
        {
            File.WriteAllText (path, actual);
            Assert.Fail ($"Golden updated at {path}. Re-run without CLET_REGEN_GOLDENS to verify.");
        }

        Assert.Equal (expected, actual);
    }

    private static string ResolveGoldenPath (string fileName)
    {
        // Goldens travel with the test binary (CopyToOutputDirectory). Resolve relative to the assembly.
        string assemblyDir = Path.GetDirectoryName (typeof (CletUIHarness<T>).Assembly.Location)!;
        return Path.Combine (assemblyDir, "Goldens", fileName);
    }

    /// <summary>Cancel the clet's run and return its final result.</summary>
    public async Task<CletRunResult<T>> StopAndGetResultAsync ()
    {
        if (!_cletTask.IsCompleted)
        {
            _cts.Cancel ();
        }

        try
        {
            return await _cletTask;
        }
        catch (OperationCanceledException)
        {
            return new CletRunResult<T> { Status = CletRunStatus.Cancelled };
        }
    }

    public async ValueTask DisposeAsync ()
    {
        try
        {
            if (!_cletTask.IsCompleted)
            {
                _cts.Cancel ();
                try { await _cletTask; } catch { /* best-effort drain */ }
            }
        }
        finally
        {
            _app.Iteration -= OnIteration;
            _cts.Dispose ();
            _app.Dispose ();
        }
    }
}
