using Xunit;

namespace Clet.UnitTests;

public class ExitCodesTests
{
    [Fact]
    public void Constants_MatchSpec ()
    {
        Assert.Equal (0, ExitCodes.Ok);
        Assert.Equal (1, ExitCodes.NoResult);
        Assert.Equal (2, ExitCodes.UsageError);
        Assert.Equal (65, ExitCodes.ValidationError);
        Assert.Equal (74, ExitCodes.IoError);
        Assert.Equal (130, ExitCodes.Cancelled);
    }

    [Theory]
    [InlineData (CletRunStatus.Ok, null, 0)]
    [InlineData (CletRunStatus.Cancelled, null, 130)]
    [InlineData (CletRunStatus.NoResult, null, 1)]
    [InlineData (CletRunStatus.Error, "validation", 65)]
    [InlineData (CletRunStatus.Error, "io", 74)]
    [InlineData (CletRunStatus.Error, "anything-else", 2)]
    public void FromResult_MapsStatusToExit (CletRunStatus status, string? errorCode, int expected)
    {
        BoxedCletResult result = new (status, null, errorCode, null);

        Assert.Equal (expected, ExitCodes.FromResult (result));
    }
}
