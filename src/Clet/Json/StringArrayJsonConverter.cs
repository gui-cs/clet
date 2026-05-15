using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clet;

/// <summary>
/// JSON converter for <see cref="string"/>[] that allows
/// <see cref="Terminal.Gui.Configuration.ConfigurationManager"/> to deserialize
/// a JSON array of strings into a <see langword="string"/>[] property.
/// </summary>
internal sealed class StringArrayJsonConverter : JsonConverter<string[]>
{
    public override string[]? Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException ($"Expected JSON array, got {reader.TokenType}.");
        }

        List<string> result = [];

        while (reader.Read () && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? val = reader.GetString ();

                if (!string.IsNullOrWhiteSpace (val))
                {
                    result.Add (val);
                }
            }
        }

        return result.Count > 0 ? result.ToArray () : [];
    }

    public override void Write (Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray ();

        foreach (string item in value)
        {
            writer.WriteStringValue (item);
        }

        writer.WriteEndArray ();
    }
}
