using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clet;

internal sealed class SchemaV1
{
    [JsonPropertyName ("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName ("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName ("value")]
    [JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Value { get; init; }

    [JsonPropertyName ("code")]
    [JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    [JsonPropertyName ("message")]
    [JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    public static SchemaV1 Ok (object? value = null) => new () { Status = "ok", Value = value };

    public static SchemaV1 Cancelled () => new () { Status = "cancelled" };

    public static SchemaV1 Error (string code, string message) =>
        new () { Status = "error", Code = code, Message = message };

    public static SchemaV1 NoResult () => new () { Status = "no-result" };

    public string ToJson ()
    {
        return JsonSerializer.Serialize (this, CletJsonContext.Default.SchemaV1);
    }
}
