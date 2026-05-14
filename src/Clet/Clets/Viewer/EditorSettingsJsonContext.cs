using System.Text.Json.Serialization;

namespace Clet;

[JsonSerializable (typeof (EditorSettings))]
[JsonSourceGenerationOptions (
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal sealed partial class EditorSettingsJsonContext : JsonSerializerContext;
