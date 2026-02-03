#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using XerahS.Editor.ImageEffects;
using SkiaSharp;
using System.IO.Compression;

namespace XerahS.Core.Helpers;

public static class ImageEffectPresetSerializer
{
    private const string ConfigFileName = "Config.json";

    public static void SaveXsieFile(string filePath, ImageEffectPreset preset)
    {
        if (preset == null) throw new ArgumentNullException(nameof(preset));

        var payload = new XsiePreset
        {
            Name = preset.Name,
            Effects = preset.Effects ?? new List<ImageEffect>()
        };

        string json = JsonConvert.SerializeObject(payload, Formatting.Indented, CreateSerializerSettings());
        WriteZip(filePath, json);
    }

    public static ImageEffectPreset? LoadXsieFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var configJson = ExtractConfigJson(filePath);
        if (string.IsNullOrWhiteSpace(configJson))
            return null;

        var payload = JsonConvert.DeserializeObject<XsiePreset>(configJson, CreateSerializerSettings());
        if (payload == null)
            return null;

        return new ImageEffectPreset
        {
            Name = payload.Name ?? "Preset",
            Effects = payload.Effects ?? new List<ImageEffect>()
        };
    }

    private static JsonSerializerSettings CreateSerializerSettings()
    {
        return new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            SerializationBinder = new ImageEffectSerializationBinder(),
            Converters = { new SkColorJsonConverter() }
        };
    }

    private static string? ExtractConfigJson(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var configEntry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase));

        if (configEntry == null)
            return null;

        using var stream = configEntry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void WriteZip(string filePath, string configJson)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(ConfigFileName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(configJson);
    }
}

internal sealed class XsiePreset
{
    public int Version { get; set; } = 1;
    public string? Name { get; set; }
    public List<ImageEffect> Effects { get; set; } = new();
}

internal sealed class ImageEffectSerializationBinder : ISerializationBinder
{
    public Type BindToType(string? assemblyName, string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new JsonSerializationException("Missing type name for image effect.");

        if (!typeName.StartsWith("XerahS.Editor.ImageEffects.", StringComparison.Ordinal))
            throw new JsonSerializationException($"Unsupported image effect type: {typeName}");

        var assembly = typeof(ImageEffect).Assembly;
        var type = assembly.GetType(typeName, throwOnError: false);

        if (type == null)
            throw new JsonSerializationException($"Unknown image effect type: {typeName}");

        return type;
    }

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }
}

internal sealed class SkColorJsonConverter : JsonConverter<SKColor>
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
