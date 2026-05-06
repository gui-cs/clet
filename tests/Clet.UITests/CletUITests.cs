using Terminal.Gui.Input;
using Xunit;

namespace Clet.UITests;

/// <summary>
///     Starter UI tests demonstrating the <see cref="CletUIHarness{T}"/>.
///     Test parallelization is disabled at the assembly level (see
///     <c>AssemblyAttributes.cs</c>) because TG's <c>IApplication</c> has process-global
///     state (<c>Application.AppModel</c>, ConfigurationManager, scheme manager) that
///     collides under concurrent harness instances.
///     <para>See <c>tests/SPEC.md</c> §2.3 and §3.2 for the design intent.</para>
/// </summary>
public class CletUITests
{
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
}
