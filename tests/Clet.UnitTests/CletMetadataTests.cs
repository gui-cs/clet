#pragma warning disable xUnit1026 // Theory methods sharing MemberData intentionally ignore unused columns

using Xunit;

namespace Clet.UnitTests;

/// <summary>
/// Shared Theory-driven tests for common clet metadata properties (PrimaryAlias, Kind, ResultType,
/// Description, Aliases, AcceptsPositionalArgs). Reduces ~50 lines of boilerplate per clet to a
/// single data row.
/// </summary>
public class CletMetadataTests
{
    private static ICletRegistry SharedRegistry ()
    {
        ICletRegistry registry = new CletRegistry ();
        BuiltInClets.RegisterAll (registry);

        return registry;
    }

    public static IEnumerable<object[]> AllCletMetadata ()
    {
        ICletRegistry registry = SharedRegistry ();

        yield return [new SelectClet (), "select", CletKind.Input, typeof (string), true];
        yield return [new IntClet (), "int", CletKind.Input, typeof (int), false];
        yield return [new DecimalClet (), "decimal", CletKind.Input, typeof (decimal), false];
        yield return [new TextClet (), "text", CletKind.Input, typeof (string), false];
        yield return [new ConfirmClet (), "confirm", CletKind.Input, typeof (bool), false];
        yield return [new ColorClet (), "color", CletKind.Input, typeof (string), false];
        yield return [new DateClet (), "date", CletKind.Input, typeof (string), false];
        yield return [new TimeClet (), "time", CletKind.Input, typeof (string), false];
        yield return [new DurationClet (), "duration", CletKind.Input, typeof (string), false];
        yield return [new MultiSelectClet (), "multi-select", CletKind.Input, typeof (System.Text.Json.Nodes.JsonArray), true];
        yield return [new LinearRangeClet (), "linear-range", CletKind.Input, typeof (System.Text.Json.Nodes.JsonObject), true];
        yield return [new PickFileClet (), "pick-file", CletKind.Input, typeof (System.Text.Json.Nodes.JsonNode), false];
        yield return [new PickDirectoryClet (), "pick-directory", CletKind.Input, typeof (string), false];
        yield return [new MarkdownClet (), "md", CletKind.Viewer, typeof (void), true];
        yield return [new HelpClet (registry), "help", CletKind.Viewer, typeof (void), true];
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void PrimaryAlias_MatchesExpected (IClet clet, string expectedAlias, CletKind _, Type __, bool ___)
    {
        Assert.Equal (expectedAlias, clet.PrimaryAlias);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void Kind_MatchesExpected (IClet clet, string _, CletKind expectedKind, Type __, bool ___)
    {
        Assert.Equal (expectedKind, clet.Kind);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void ResultType_MatchesExpected (IClet clet, string _, CletKind __, Type expectedType, bool ___)
    {
        Assert.Equal (expectedType, clet.ResultType);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void Description_IsNotEmpty (IClet clet, string _, CletKind __, Type ___, bool ____)
    {
        Assert.NotEmpty (clet.Description);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void Aliases_ContainsPrimaryAlias (IClet clet, string expectedAlias, CletKind _, Type __, bool ___)
    {
        Assert.Contains (expectedAlias, clet.Aliases);
    }

    [Theory]
    [MemberData (nameof (AllCletMetadata))]
    internal void AcceptsPositionalArgs_MatchesExpected (IClet clet, string _, CletKind __, Type ___, bool expectedPositional)
    {
        Assert.Equal (expectedPositional, clet.AcceptsPositionalArgs);
    }
}
