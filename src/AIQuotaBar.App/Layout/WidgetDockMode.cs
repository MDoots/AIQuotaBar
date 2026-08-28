namespace AIQuotaBar.App.Layout;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonConverter(typeof(WidgetDockModeJsonConverter))]
public enum WidgetDockMode
{
    Floating = 0,
    Top = 1,
    Bottom = 2
}

public sealed class WidgetDockModeJsonConverter : JsonConverter<WidgetDockMode>
{
    public override WidgetDockMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (Enum.TryParse<WidgetDockMode>(stringValue, ignoreCase: true, out var mode) &&
                Enum.IsDefined(typeof(WidgetDockMode), mode))
            {
                return mode;
            }

            return WidgetDockMode.Floating;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var intValue) && Enum.IsDefined(typeof(WidgetDockMode), intValue))
            {
                return (WidgetDockMode)intValue;
            }

            return WidgetDockMode.Floating;
        }

        return WidgetDockMode.Floating;
    }

    public override void Write(Utf8JsonWriter writer, WidgetDockMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
