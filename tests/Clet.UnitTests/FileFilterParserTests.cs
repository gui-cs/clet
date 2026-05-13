using Xunit;

namespace Clet.UnitTests;

public class FileFilterParserTests
{
    [Fact]
    public void ParseExtensions_Null_ReturnsEmpty ()
    {
        Assert.Empty (FileFilterParser.ParseExtensions (null));
    }

    [Fact]
    public void ParseExtensions_Whitespace_ReturnsEmpty ()
    {
        Assert.Empty (FileFilterParser.ParseExtensions ("   "));
    }

    [Fact]
    public void ParseExtensions_StarDotForm_StripsStar ()
    {
        Assert.Equal (new[] { ".cs" }, FileFilterParser.ParseExtensions ("*.cs"));
    }

    [Fact]
    public void ParseExtensions_DotForm_KeptAsIs ()
    {
        Assert.Equal (new[] { ".cs" }, FileFilterParser.ParseExtensions (".cs"));
    }

    [Fact]
    public void ParseExtensions_BareForm_GetsLeadingDot ()
    {
        Assert.Equal (new[] { ".cs" }, FileFilterParser.ParseExtensions ("cs"));
    }

    [Fact]
    public void ParseExtensions_CommaSeparated_AllNormalized ()
    {
        Assert.Equal (new[] { ".cs", ".md", ".txt" }, FileFilterParser.ParseExtensions ("*.cs, .md, txt"));
    }

    [Fact]
    public void ParseExtensions_DuplicatesCollapsed_CaseInsensitive ()
    {
        Assert.Equal (new[] { ".cs" }, FileFilterParser.ParseExtensions ("*.cs,.CS,cs"));
    }

    [Fact]
    public void ParseExtensions_LoneStarOrEmpty_DroppedAsTooShort ()
    {
        Assert.Empty (FileFilterParser.ParseExtensions ("*"));
    }
}
