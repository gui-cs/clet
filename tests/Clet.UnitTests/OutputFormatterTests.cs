using Xunit;

namespace Clet.UnitTests;

public class OutputFormatterTests
{
    [Fact]
    public void Json_OkWithValue_EmitsValueAndStatus ()
    {
        BoxedCletResult result = new (CletRunStatus.Ok, 2, null, null);
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: true, stdout, stderr);

        string line = stdout.ToString ().TrimEnd ();
        Assert.Equal ("{\"schemaVersion\":1,\"status\":\"ok\",\"value\":2}", line);
        Assert.Empty (stderr.ToString ());
    }

    [Fact]
    public void Json_ViewerOk_OmitsValue ()
    {
        BoxedCletResult result = new (CletRunStatus.Ok, null, null, null);
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: true, stdout, stderr);

        Assert.Equal ("{\"schemaVersion\":1,\"status\":\"ok\"}", stdout.ToString ().TrimEnd ());
    }

    [Fact]
    public void Json_Cancelled_OmitsValueAndCode ()
    {
        BoxedCletResult result = new (CletRunStatus.Cancelled, null, null, null);
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: true, stdout, stderr);

        Assert.Equal ("{\"schemaVersion\":1,\"status\":\"cancelled\"}", stdout.ToString ().TrimEnd ());
    }

    [Fact]
    public void Json_Error_IncludesCodeAndMessage ()
    {
        BoxedCletResult result = new (CletRunStatus.Error, null, "validation", "bad input");
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: true, stdout, stderr);

        string line = stdout.ToString ().TrimEnd ();
        Assert.Contains ("\"status\":\"error\"", line);
        Assert.Contains ("\"code\":\"validation\"", line);
        Assert.Contains ("\"message\":\"bad input\"", line);
    }

    [Fact]
    public void Plain_OkWithValue_WritesValueToStdout ()
    {
        BoxedCletResult result = new (CletRunStatus.Ok, "prod", null, null);
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: false, stdout, stderr);

        Assert.Equal ("prod", stdout.ToString ().TrimEnd ());
        Assert.Empty (stderr.ToString ());
    }

    [Fact]
    public void Plain_Cancelled_WritesNothing ()
    {
        BoxedCletResult result = new (CletRunStatus.Cancelled, null, null, null);
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: false, stdout, stderr);

        Assert.Empty (stdout.ToString ());
        Assert.Empty (stderr.ToString ());
    }

    [Fact]
    public void Plain_Error_WritesToStderr ()
    {
        BoxedCletResult result = new (CletRunStatus.Error, null, "validation", "bad input");
        StringWriter stdout = new ();
        StringWriter stderr = new ();

        OutputFormatter.Write (result, jsonOutput: false, stdout, stderr);

        Assert.Empty (stdout.ToString ());
        Assert.Contains ("validation", stderr.ToString ());
        Assert.Contains ("bad input", stderr.ToString ());
    }
}
