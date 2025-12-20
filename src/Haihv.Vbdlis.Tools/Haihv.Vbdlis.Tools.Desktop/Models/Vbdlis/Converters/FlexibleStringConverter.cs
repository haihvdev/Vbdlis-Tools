using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Haihv.Vbdlis.Tools.Desktop.Models.Vbdlis.Converters;

public sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumberAsString(ref reader),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing string.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    private static string ReadNumberAsString(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var longValue))
        {
            return longValue.ToString(CultureInfo.InvariantCulture);
        }

        return reader.TryGetDecimal(out var decimalValue) 
            ? decimalValue.ToString(CultureInfo.InvariantCulture) 
            : reader.GetDouble().ToString(CultureInfo.InvariantCulture);
    }
}
