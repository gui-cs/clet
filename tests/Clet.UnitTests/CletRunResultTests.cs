using Xunit;

namespace Clet.UnitTests;

public class CletRunResultTests
{
    [Fact]
    public void CletRunResult_DefaultStatus_IsOk ()
    {
        CletRunResult result = new () { Status = CletRunStatus.Ok };

        Assert.Equal (CletRunStatus.Ok, result.Status);
        Assert.Null (result.ErrorCode);
        Assert.Null (result.ErrorMessage);
    }

    [Fact]
    public void CletRunResultT_ToUntyped_PreservesStatus ()
    {
        CletRunResult<int?> typed = new () { Status = CletRunStatus.Cancelled };
        CletRunResult untyped = typed.ToUntyped ();

        Assert.Equal (CletRunStatus.Cancelled, untyped.Status);
    }

    [Fact]
    public void CletRunResultT_ToUntyped_PreservesError ()
    {
        CletRunResult<string> typed = new ()
        {
            Status = CletRunStatus.Error,
            ErrorCode = "TEST",
            ErrorMessage = "Something failed",
        };
        CletRunResult untyped = typed.ToUntyped ();

        Assert.Equal ("TEST", untyped.ErrorCode);
        Assert.Equal ("Something failed", untyped.ErrorMessage);
    }

    [Fact]
    public void CletRunResultT_Value_CanBeSet ()
    {
        CletRunResult<int?> result = new () { Status = CletRunStatus.Ok, Value = 5 };

        Assert.Equal (5, result.Value);
    }
}
