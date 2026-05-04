using System.Text.Json;
using Xunit;

namespace Clet.UnitTests;

public class SchemaV1Tests
{
    [Fact]
    public void Ok_WithValue_ProducesCorrectJson ()
    {
        SchemaV1 envelope = SchemaV1.Ok (42);
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal (1, root.GetProperty ("schemaVersion").GetInt32 ());
        Assert.Equal ("ok", root.GetProperty ("status").GetString ());
        Assert.Equal (42, root.GetProperty ("value").GetInt32 ());
        Assert.False (root.TryGetProperty ("code", out _));
        Assert.False (root.TryGetProperty ("message", out _));
    }

    [Fact]
    public void Ok_WithoutValue_HasNoValueField ()
    {
        SchemaV1 envelope = SchemaV1.Ok ();
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal ("ok", root.GetProperty ("status").GetString ());
        Assert.False (root.TryGetProperty ("value", out _));
    }

    [Fact]
    public void Cancelled_ProducesCorrectJson ()
    {
        SchemaV1 envelope = SchemaV1.Cancelled ();
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal (1, root.GetProperty ("schemaVersion").GetInt32 ());
        Assert.Equal ("cancelled", root.GetProperty ("status").GetString ());
        Assert.False (root.TryGetProperty ("value", out _));
        Assert.False (root.TryGetProperty ("code", out _));
        Assert.False (root.TryGetProperty ("message", out _));
    }

    [Fact]
    public void Error_ProducesCorrectJson ()
    {
        SchemaV1 envelope = SchemaV1.Error ("INVALID_INPUT", "Bad data");
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal (1, root.GetProperty ("schemaVersion").GetInt32 ());
        Assert.Equal ("error", root.GetProperty ("status").GetString ());
        Assert.Equal ("INVALID_INPUT", root.GetProperty ("code").GetString ());
        Assert.Equal ("Bad data", root.GetProperty ("message").GetString ());
        Assert.False (root.TryGetProperty ("value", out _));
    }

    [Fact]
    public void NoResult_ProducesCorrectJson ()
    {
        SchemaV1 envelope = SchemaV1.NoResult ();
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal (1, root.GetProperty ("schemaVersion").GetInt32 ());
        Assert.Equal ("no-result", root.GetProperty ("status").GetString ());
        Assert.False (root.TryGetProperty ("value", out _));
    }

    [Fact]
    public void Ok_WithStringValue_ProducesCorrectJson ()
    {
        SchemaV1 envelope = SchemaV1.Ok ("hello");
        string json = envelope.ToJson ();

        using JsonDocument doc = JsonDocument.Parse (json);
        JsonElement root = doc.RootElement;

        Assert.Equal ("hello", root.GetProperty ("value").GetString ());
    }
}
