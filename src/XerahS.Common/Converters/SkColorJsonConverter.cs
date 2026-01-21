using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Linq;

namespace XerahS.Common.Converters
{
    public sealed class SkColorJsonConverter : JsonConverter<SKColor>
    {
        public override void WriteJson(JsonWriter writer, SKColor value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.Alpha}, {value.Red}, {value.Green}, {value.Blue}");
        }

        public override SKColor ReadJson(JsonReader reader, Type objectType, SKColor existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String && reader.Value is string text)
            {
                return ParseColor(text);
            }
            
            // Handle legacy or existing object structure if necessary, or just return existingValue
            // If the JSON is { "Alpha": 0, "Red": 0 ... }, this reader might not be hit if it expects a string,
            // but we want to force it to read properly.
            // Actually, if the JSON contains an object, this ReadJson will be called with StartObject token?
            // JsonConverter<T> handles this.
            
            if (reader.TokenType == JsonToken.StartObject)
            {
                 // Fallback for object format: { "Alpha": 0, "Red": 0, ... }
                 // We can use a temporary object to deserialize, then convert.
                 // But simpler: just load into JObject
                 var obj = Newtonsoft.Json.Linq.JObject.Load(reader);
                 byte a = obj["Alpha"]?.ToObject<byte>() ?? 0;
                 byte r = obj["Red"]?.ToObject<byte>() ?? 0;
                 byte g = obj["Green"]?.ToObject<byte>() ?? 0;
                 byte b = obj["Blue"]?.ToObject<byte>() ?? 0;
                 return new SKColor(r, g, b, a);
            }

            return existingValue;
        }

        private static SKColor ParseColor(string? colorString)
        {
            if (string.IsNullOrWhiteSpace(colorString))
                return SKColors.Transparent;

            if (colorString.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                return SKColors.Transparent;
            if (colorString.Equals("Black", StringComparison.OrdinalIgnoreCase))
                return SKColors.Black;
            if (colorString.Equals("White", StringComparison.OrdinalIgnoreCase))
                return SKColors.White;

            var parts = colorString.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length == 4 &&
                byte.TryParse(parts[0], out var a) &&
                byte.TryParse(parts[1], out var r) &&
                byte.TryParse(parts[2], out var g) &&
                byte.TryParse(parts[3], out var b))
            {
                return new SKColor(r, g, b, a);
            }

            if (parts.Length == 3 &&
                byte.TryParse(parts[0], out r) &&
                byte.TryParse(parts[1], out g) &&
                byte.TryParse(parts[2], out b))
            {
                return new SKColor(r, g, b);
            }

            return SKColors.Transparent;
        }
    }
}
