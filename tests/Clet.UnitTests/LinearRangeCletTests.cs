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
    }

    [Fact]
    public void Options_IncludesOptionsAndKind ()
    {
        LinearRangeClet clet = new ();

        Assert.Contains (clet.Options, o => o.Name == "options");
        Assert.Contains (clet.Options, o => o.Name == "kind");
    }

    [Fact]
    public async Task RunAsync_NoOptions_ReturnsValidationError ()
    {
        LinearRangeClet clet = new ();
        CletRunOptions options = new ();

        using CancellationTokenSource cts = new ();

        // Validation fires before the TUI loop; null IApplication is fine.
        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            null!, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("validation", result.ErrorCode);
        Assert.Contains ("requires --options", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task RunAsync_UnknownKind_ReturnsValidationError ()
    {
        LinearRangeClet clet = new ();
        CletRunOptions options = new ()
        {
            CletOptions = new Dictionary<string, string>
            {
                ["options"] = "a,b,c",
                ["kind"] = "diagonal",
            },
        };

        using CancellationTokenSource cts = new ();

        CletRunResult<System.Text.Json.Nodes.JsonObject?> result = await clet.RunAsync (
            null!, null, options, cts.Token);

        Assert.Equal (CletRunStatus.Error, result.Status);
        Assert.Equal ("validation", result.ErrorCode);
        Assert.Contains ("unknown --kind", result.ErrorMessage ?? "");
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
