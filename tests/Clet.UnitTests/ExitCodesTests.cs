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
    [InlineData ((int)CletRunStatus.Ok, null, 0)]
    [InlineData ((int)CletRunStatus.Cancelled, null, 130)]
    [InlineData ((int)CletRunStatus.NoResult, null, 1)]
    [InlineData ((int)CletRunStatus.Error, "validation", 65)]
    [InlineData ((int)CletRunStatus.Error, "input-too-large", 65)]
    [InlineData ((int)CletRunStatus.Error, "io", 74)]
    [InlineData ((int)CletRunStatus.Error, "anything-else", 2)]
    public void FromResult_MapsStatusToExit (int statusInt, string? errorCode, int expected)
    {
        BoxedCletResult result = new ((CletRunStatus)statusInt, null, errorCode, null);

        Assert.Equal (expected, ExitCodes.FromResult (result));
    }
}
