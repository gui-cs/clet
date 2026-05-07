using System.Text.Json.Nodes;
using Xunit;

namespace Clet.UnitTests;

public class CletTypeNamesTests
{
    [Theory]
    [InlineData (typeof (string), "string")]
    [InlineData (typeof (int), "int")]
    [InlineData (typeof (long), "int")]
    [InlineData (typeof (short), "int")]
    [InlineData (typeof (decimal), "decimal")]
    [InlineData (typeof (double), "decimal")]
    [InlineData (typeof (float), "decimal")]
    [InlineData (typeof (bool), "bool")]
    [InlineData (typeof (DateTime), "date")]
    [InlineData (typeof (DateOnly), "date")]
    [InlineData (typeof (TimeOnly), "time")]
    [InlineData (typeof (TimeSpan), "duration")]
    [InlineData (typeof (void), "none")]
    public void WireName_MapsKnownTypes (Type type, string expected)
    {
        Assert.Equal (expected, CletTypeNames.WireName (type));
    }

    [Fact]
    public void WireName_JsonArray_ReturnsArray ()
    {
        Assert.Equal ("array", CletTypeNames.WireName (typeof (JsonArray)));
    }

    [Fact]
    public void WireName_JsonObject_ReturnsObject ()
    {
        Assert.Equal ("object", CletTypeNames.WireName (typeof (JsonObject)));
    }

    [Fact]
    public void WireName_JsonNode_ReturnsJson ()
    {
        Assert.Equal ("json", CletTypeNames.WireName (typeof (JsonNode)));
    }

    [Fact]
    public void WireName_NullableInt_ReturnsInt ()
    {
        Assert.Equal ("int", CletTypeNames.WireName (typeof (int?)));
    }

    [Fact]
    public void WireName_NullableBool_ReturnsBool ()
    {
        Assert.Equal ("bool", CletTypeNames.WireName (typeof (bool?)));
    }

    [Fact]
    public void WireName_NullableDateTime_ReturnsDate ()
    {
        Assert.Equal ("date", CletTypeNames.WireName (typeof (DateTime?)));
    }

    [Fact]
    public void WireName_UnknownType_ReturnsTypeName ()
    {
        Assert.Equal ("Guid", CletTypeNames.WireName (typeof (Guid)));
    }
}
