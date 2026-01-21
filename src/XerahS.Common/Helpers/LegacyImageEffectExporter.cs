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
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System.IO.Compression;
using System.Reflection;

namespace XerahS.Common.Helpers;

/// <summary>
/// Exports XerahS image effect presets to legacy ShareX .sxie format.
/// </summary>
public static class LegacyImageEffectExporter
{
    private const string ConfigFileName = "Config.json";

    private static readonly Dictionary<string, LegacyEffectMapping> SupportedEffects = new()
    {
        // Adjustments
        ["BrightnessImageEffect"] = new("Brightness", new() { ["Amount"] = "Value" }),
        ["ContrastImageEffect"] = new("Contrast", new() { ["Amount"] = "Value" }),
        ["HueImageEffect"] = new("Hue", new() { ["Amount"] = "Value" }),
        ["SaturationImageEffect"] = new("Saturation", new() { ["Amount"] = "Value" }),
        ["GammaImageEffect"] = new("Gamma", new() { ["Amount"] = "Value" }),
        ["AlphaImageEffect"] = new("Alpha", new() { ["Amount"] = "Value" }),
        ["ColorizeImageEffect"] = new("Colorize", new() { ["Color"] = "Color", ["Strength"] = "Strength" }),
        ["ReplaceColorImageEffect"] = new("ReplaceColor", new() { ["TargetColor"] = "SourceColor", ["ReplaceColor"] = "TargetColor", ["Tolerance"] = "Threshold" }),

        // Filters
        ["InvertImageEffect"] = new("Inverse", new()),
        ["GrayscaleImageEffect"] = new("Grayscale", new() { ["Strength"] = "Percentage" }),
        ["BlackAndWhiteImageEffect"] = new("BlackWhite", new()),
        ["SepiaImageEffect"] = new("Sepia", new()),
        ["PolaroidImageEffect"] = new("Polaroid", new()),

        // Transforms
        ["AutoCropImageEffect"] = new("AutoCrop", new()),
    };

    public static LegacyPresetExportResult ExportSxieFile(string filePath, string presetName, IEnumerable<object> effects)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new LegacyPresetExportResult
            {
                Success = false,
                ErrorMessage = "Missing output file path."
            };
        }

        try
        {
            var effectArray = new JArray();
            var result = new LegacyPresetExportResult();

            foreach (var effect in effects)
            {
                if (effect == null) continue;

                var effectObject = BuildLegacyEffect(effect, result.SkippedEffects);
                if (effectObject != null)
                {
                    effectArray.Add(effectObject);
                }
            }

            var presetJson = new JObject
            {
                ["Name"] = presetName ?? "Preset",
                ["Effects"] = effectArray
            };

            string configJson = JsonConvert.SerializeObject(presetJson, Formatting.Indented);
            WriteZip(filePath, configJson);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return new LegacyPresetExportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static JObject? BuildLegacyEffect(object effect, List<string> skippedEffects)
    {
        var type = effect.GetType();
        var typeName = type.Name;

        if (typeName.Equals("FlipImageEffect", StringComparison.Ordinal))
        {
            return BuildFlipEffect(effect);
        }

        if (typeName.Equals("RotateImageEffect", StringComparison.Ordinal))
        {
            return BuildRotateEffect(effect);
        }

        if (typeName.Equals("ResizeImageEffect", StringComparison.Ordinal))
        {
            return BuildResizeEffect(effect);
        }

        if (!SupportedEffects.TryGetValue(typeName, out var mapping))
        {
            skippedEffects.Add(typeName);
            return null;
        }

        var effectObject = new JObject
        {
            ["$type"] = $"ShareX.ImageEffectsLib.{mapping.LegacyTypeName}, ShareX.ImageEffectsLib",
            ["Enabled"] = true
        };

        foreach (var propertyMap in mapping.PropertyMappings)
        {
            var value = GetMemberValue(effect, propertyMap.Key);
            if (value == null) continue;

            effectObject[propertyMap.Value] = JToken.FromObject(ConvertLegacyValue(value));
        }

        return effectObject;
    }

    private static JObject? BuildFlipEffect(object effect)
    {
        var direction = GetMemberValue(effect, "_direction");
        if (direction == null) return null;

        var directionText = direction.ToString() ?? string.Empty;
        bool horizontal = directionText.Equals("Horizontal", StringComparison.OrdinalIgnoreCase);
        bool vertical = directionText.Equals("Vertical", StringComparison.OrdinalIgnoreCase);

        return new JObject
        {
            ["$type"] = "ShareX.ImageEffectsLib.Flip, ShareX.ImageEffectsLib",
            ["Enabled"] = true,
            ["Horizontally"] = horizontal,
            ["Vertically"] = vertical
        };
    }

    private static JObject? BuildRotateEffect(object effect)
    {
        var angle = GetMemberValue(effect, "_angle");
        if (angle == null) return null;

        return new JObject
        {
            ["$type"] = "ShareX.ImageEffectsLib.Rotate, ShareX.ImageEffectsLib",
            ["Enabled"] = true,
            ["Angle"] = JToken.FromObject(ConvertLegacyValue(angle))
        };
    }

    private static JObject? BuildResizeEffect(object effect)
    {
        var width = GetMemberValue(effect, "_width");
        var height = GetMemberValue(effect, "_height");

        if (width == null || height == null) return null;

        return new JObject
        {
            ["$type"] = "ShareX.ImageEffectsLib.Resize, ShareX.ImageEffectsLib",
            ["Enabled"] = true,
            ["Width"] = JToken.FromObject(ConvertLegacyValue(width)),
            ["Height"] = JToken.FromObject(ConvertLegacyValue(height))
        };
    }

    private static object ConvertLegacyValue(object value)
    {
        if (value is SKColor color)
        {
            return $"{color.Alpha}, {color.Red}, {color.Green}, {color.Blue}";
        }

        return value;
    }

    private static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(instance);
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

public sealed class LegacyPresetExportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> SkippedEffects { get; set; } = new();
}

internal record LegacyEffectMapping(string LegacyTypeName, Dictionary<string, string> PropertyMappings);
