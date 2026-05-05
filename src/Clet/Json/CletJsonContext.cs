using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Clet;

[JsonSerializable (typeof (SchemaV1))]
[JsonSerializable (typeof (JsonNode))]
[JsonSerializable (typeof (JsonArray))]
[JsonSerializable (typeof (JsonObject))]
[JsonSourceGenerationOptions (
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class CletJsonContext : JsonSerializerContext;
