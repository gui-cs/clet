using Xunit;

namespace Clet.UnitTests;

public class LinearRangeCletTests
{
    [Fact]
    public void Metadata_AdvertisesAlias ()
    {
        LinearRangeClet clet = new ();

        Assert.Equal ("linear-range", clet.PrimaryAlias);
        Assert.Contains ("linear-range", clet.Aliases);
        Assert.Equal (CletKind.Input, clet.Kind);
        Assert.Equal (typeof (System.Text.Json.Nodes.JsonObject), clet.ResultType);
    }

    [Fact]
    public void Options_IncludesModeAndOptions ()
    {
        LinearRangeClet clet = new ();

        Assert.Contains (clet.Options, o => o.Name == "mode");
        Assert.Contains (clet.Options, o => o.Name == "options");
        Assert.Contains (clet.Options, o => o.Name == "orientation");
        Assert.Contains (clet.Options, o => o.Name == "range-kind");
        Assert.Contains (clet.Options, o => o.Name == "allow-empty");
        Assert.Contains (clet.Options, o => o.Name == "hide-legends");
    }

    [Fact]
    public void AcceptsPositionalArgs_IsTrue ()
    {
        LinearRangeClet clet = new ();

        Assert.True (clet.AcceptsPositionalArgs);
    }

    [Fact]
    public async Task RunAsync_NoOptions_ReturnsValidationError ()
    {
        LinearRangeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            null!, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("validation", result.ErrorCode);
        Assert.Contains ("requires --options", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task RunAsync_PreCancelled_ReturnsCancelled ()
    {
        LinearRangeClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string> { ["options"] = "a,b,c" },
        };

        using CancellationTokenSource cts = new ();
        cts.Cancel ();

        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            null!, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Cancelled, result.Status);
    }
}
