using System.Text.Json.Serialization;

namespace Clet;

[JsonSerializable (typeof (SchemaV1))]
[JsonSourceGenerationOptions (
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class CletJsonContext : JsonSerializerContext;
