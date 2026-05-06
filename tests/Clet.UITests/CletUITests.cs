using Terminal.Gui.Input;
using Xunit;

namespace Clet.UITests;

/// <summary>
///     Starter UI tests demonstrating the <see cref="CletUIHarness{T}"/>. Consolidated
///     into a single class so xUnit serializes them — TG's <c>IApplication</c> has
///     enough process-global state that two harness instances running in parallel
///     collide. If you ever spread these across multiple classes, disable test
///     parallelization at the assembly level (xUnit v3:
///     <c>[assembly: CollectionBehavior(DisableTestParallelization = true)]</c>).
///
///     <para>
///         <b>Test order sensitivity (known sharp edge):</b> The first harness instantiation
///         per process pays a JIT/init cost where some Views (TextField in particular)
///         don't render their initial state within the harness's
///         <c>maxStartupIterations</c> budget. Subsequent harness instantiations in the
///         same process render fully on the first iteration. As a workaround, tests with
///         simpler Views (Select, Markdown) run first and "warm up" the process before the
///         TextClet tests run. If you reorder these tests and the TextClet ones fail with
///         empty snapshots, you've hit this — keep TextClet last or investigate the
///         underlying race in the harness's startup loop.
///     </para>
///     <para>See <c>tests/SPEC.md</c> §2.3 and §3.2 for the design intent.</para>
/// </summary>
public class CletUITests
{
    [Fact]
    public async Task SelectClet_InitialRender_ShowsAllOptionLabels ()
    {
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "Apple,Banana,Cherry" },
        };

        await using CletUIHarness<string?> harness = await CletUIHarness<string?>.StartAsync (
            new SelectClet (), options: options);

        string snapshot = harness.SnapshotText ();

        Assert.Contains ("Apple", snapshot);
        Assert.Contains ("Banana", snapshot);
        Assert.Contains ("Cherry", snapshot);
        Assert.Contains ("Select an option", snapshot);
    }

    [Fact]
    public async Task MarkdownClet_InitialRender_ShowsInlineContent ()
    {
        await using CletUIHarness<object?> harness = await CletUIHarness<object?>.StartViewerAsync (
            new MarkdownClet (),
            initial: "# Hello\n\nThis is a test.");

        string snapshot = harness.SnapshotText ();

        Assert.Contains ("Hello", snapshot);
        Assert.Contains ("test", snapshot);
    }

    [Fact]
    public async Task TextClet_InitialRender_ShowsTitleAndInitialValue ()
    {
        await using CletUIHarness<string?> harness = await CletUIHarness<string?>.StartAsync (
            new TextClet (), initial: "hello");

        string snapshot = harness.SnapshotText ();

        Assert.Contains ("Enter text", snapshot);
        Assert.Contains ("hello", snapshot);
    }

    [Fact]
    public async Task TextClet_EnterAccepts_ReturnsCurrentText ()
    {
        await using CletUIHarness<string?> harness = await CletUIHarness<string?>.StartAsync (
            new TextClet (), initial: "ok");

        await harness.PressAsync (Key.Enter);

        CletRunResult<string?> result = await harness.StopAndGetResultAsync ();

        Assert.Equal (CletRunStatus.Ok, result.Status);
        Assert.Equal ("ok", result.Value);
    }
}
