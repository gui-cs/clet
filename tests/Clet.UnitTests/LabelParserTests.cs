using Xunit;

namespace Clet.UnitTests;

public class LabelParserTests
{
    [Fact]
    public void Split_NullString_ReturnsEmpty ()
    {
        Assert.Empty (LabelParser.Split ((string?)null));
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmpty ()
    {
        Assert.Empty (LabelParser.Split (string.Empty));
    }

    [Fact]
    public void Split_CommaSeparated_ReturnsTokens ()
    {
        Assert.Equal (new[] { "a", "b", "c" }, LabelParser.Split ("a,b,c"));
    }

    [Fact]
    public void Split_TrimsWhitespaceAroundCommas ()
    {
        Assert.Equal (new[] { "a", "b", "c" }, LabelParser.Split ("a, b, c"));
    }

    [Fact]
    public void Split_DropsEmptyEntries ()
    {
        Assert.Equal (new[] { "a", "b" }, LabelParser.Split ("a,,b,"));
    }

    [Fact]
    public void Split_SpaceSeparatedArgs_TreatedAsLabels ()
    {
        Assert.Equal (new[] { "a", "b", "c" }, LabelParser.Split (new[] { "a", "b", "c" }));
    }

    [Fact]
    public void Split_MixedCommasInArgs_FlattenedAndSplit ()
    {
        Assert.Equal (new[] { "a", "b", "c" }, LabelParser.Split (new[] { "a,b", "c" }));
    }

    [Fact]
    public void Split_TrailingCommaInArg_DropsEmpty ()
    {
        Assert.Equal (new[] { "a", "b", "c" }, LabelParser.Split (new[] { "a,", "b,", "c" }));
    }

    [Fact]
    public void Split_SingleArgWithSpacesAfterCommas_Trimmed ()
    {
        Assert.Equal (new[] { "a", "b", "c" }, LabelParser.Split (new[] { "a, b, c" }));
    }
}
