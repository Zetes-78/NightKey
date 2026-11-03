using System.Text.Json;
using System.Text.Json.Serialization;

namespace NightKey.Core;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create(false);
    public static readonly JsonSerializerOptions Indented = Create(true);

    private static JsonSerializerOptions Create(bool indented)
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}
