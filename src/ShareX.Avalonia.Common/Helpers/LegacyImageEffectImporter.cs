#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using Newtonsoft.Json.Serialization;
using SkiaSharp;
using System.IO.Compression;
using XerahS.Common;

namespace XerahS.Common.Helpers;

/// <summary>
/// Imports legacy ShareX .sxie image effect presets.
/// Maps compatible effects to ShareX.Editor.ImageEffects classes.
/// </summary>
public static class LegacyImageEffectImporter
{
    private const string ConfigFileName = "Config.json";

    /// <summary>
    /// Maps legacy ShareX.ImageEffectsLib type names to their property schema.
    /// Key: legacy class name (e.g., "Brightness")
    /// Value: tuple of (target type name in ShareX.Editor, property mappings)
    /// </summary>
    private static readonly Dictionary<string, EffectMapping> SupportedEffects = new()
    {
        // Adjustments
        ["Brightness"] = new("BrightnessImageEffect", new() { ["Value"] = "Amount" }),
        ["Contrast"] = new("ContrastImageEffect", new() { ["Value"] = "Amount" }),
        ["Hue"] = new("HueImageEffect", new() { ["Value"] = "Amount" }),
        ["Saturation"] = new("SaturationImageEffect", new() { ["Value"] = "Amount" }),
        ["Gamma"] = new("GammaImageEffect", new() { ["Value"] = "Amount" }),
        ["Alpha"] = new("AlphaImageEffect", new() { ["Value"] = "Amount" }),
        ["Colorize"] = new("ColorizeImageEffect", new() { ["Color"] = "Color", ["Strength"] = "Strength" }),
        ["ReplaceColor"] = new("ReplaceColorImageEffect", new() { ["SourceColor"] = "TargetColor", ["TargetColor"] = "ReplaceColor", ["Threshold"] = "Tolerance" }),
        ["SelectiveColor"] = new("SelectiveColorImageEffect", new()), // Complex, needs special handling
        
        // Filters
        ["Inverse"] = new("InvertImageEffect", new()),
        ["Grayscale"] = new("GrayscaleImageEffect", new() { ["Percentage"] = "Strength" }),
        ["BlackWhite"] = new("BlackAndWhiteImageEffect", new()),
        ["Sepia"] = new("SepiaImageEffect", new()),
        ["Polaroid"] = new("PolaroidImageEffect", new()),
        
        // Transforms
        ["Flip"] = new("FlipImageEffect", new() { ["Horizontally"] = "Horizontal", ["Vertically"] = "Vertical" }),
        ["Rotate"] = new("RotateImageEffect", new() { ["Angle"] = "Angle" }),
        ["Resize"] = new("ResizeImageEffect", new() { ["Width"] = "_width", ["Height"] = "_height" }),
        ["AutoCrop"] = new("AutoCropImageEffect", new()),
    };

    /// <summary>
    /// Import an .sxie file and return the preset data as JSON compatible with ShareX.Editor.
    /// </summary>
    public static LegacyPresetImportResult? ImportSxieFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            string? configJson = ExtractConfigJson(filePath);
            if (string.IsNullOrEmpty(configJson))
                return null;

            return ImportFromJson(configJson);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return new LegacyPresetImportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract Config.json from .sxie ZIP archive.
    /// </summary>
    private static string? ExtractConfigJson(string sxieFilePath)
    {
        using var archive = ZipFile.OpenRead(sxieFilePath);
        var configEntry = archive.Entries.FirstOrDefault(e => 
            e.FullName.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase));

        if (configEntry == null)
            return null;

        using var stream = configEntry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parse legacy JSON and convert to new format.
    /// </summary>
    public static LegacyPresetImportResult ImportFromJson(string json)
    {
        var result = new LegacyPresetImportResult();

        try
        {
            var legacyPreset = JObject.Parse(json);
            
            result.PresetName = legacyPreset["Name"]?.ToString() ?? "Imported Preset";
            
            var effectsArray = legacyPreset["Effects"] as JArray;
            if (effectsArray == null)
            {
                result.Success = true;
                return result;
            }

            foreach (var effectToken in effectsArray)
            {
                var effectObj = effectToken as JObject;
                if (effectObj == null) continue;

                var typeString = effectObj["$type"]?.ToString();
                if (string.IsNullOrEmpty(typeString)) continue;

                // Parse type: "ShareX.ImageEffectsLib.Brightness, ShareX.ImageEffectsLib"
                var typeParts = typeString.Split(',');
                if (typeParts.Length < 1) continue;

                var fullTypeName = typeParts[0].Trim();
                var className = fullTypeName.Split('.').LastOrDefault();

                if (string.IsNullOrEmpty(className)) continue;

                // Check if enabled (default true)
                var enabled = effectObj["Enabled"]?.Value<bool>() ?? true;
                if (!enabled)
                {
                    result.SkippedEffects.Add($"{className} (disabled)");
                    continue;
                }

                if (SupportedEffects.TryGetValue(className, out var mapping))
                {
                    var mappedEffect = MapEffect(effectObj, className, mapping);
                    if (mappedEffect != null)
                    {
                        result.MappedEffects.Add(mappedEffect);
                    }
                }
                else
                {
                    result.SkippedEffects.Add(className);
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            DebugHelper.WriteException(ex);
        }

        return result;
    }

    private static MappedEffect? MapEffect(JObject effectObj, string legacyClassName, EffectMapping mapping)
    {
        var mapped = new MappedEffect
        {
            TargetTypeName = mapping.TargetTypeName,
            Properties = new Dictionary<string, object?>()
        };

        foreach (var propMap in mapping.PropertyMappings)
        {
            var legacyPropName = propMap.Key;
            var targetPropName = propMap.Value;

            var value = effectObj[legacyPropName];
            if (value != null)
            {
                // Handle color conversion
                if (targetPropName.Contains("Color", StringComparison.OrdinalIgnoreCase))
                {
                    var colorValue = ParseLegacyColor(value.ToString());
                    mapped.Properties[targetPropName] = colorValue;
                }
                else
                {
                    mapped.Properties[targetPropName] = value.ToObject<object>();
                }
            }
        }

        return mapped;
    }

    /// <summary>
    /// Parse legacy color format: "A, R, G, B" or named color.
    /// </summary>
    private static SKColor ParseLegacyColor(string? colorString)
    {
        if (string.IsNullOrEmpty(colorString))
            return SKColors.Transparent;

        // Named colors
        if (colorString.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            return SKColors.Transparent;
        if (colorString.Equals("Black", StringComparison.OrdinalIgnoreCase))
            return SKColors.Black;
        if (colorString.Equals("White", StringComparison.OrdinalIgnoreCase))
            return SKColors.White;

        // "A, R, G, B" format
        var parts = colorString.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length == 4 &&
            byte.TryParse(parts[0], out var a) &&
            byte.TryParse(parts[1], out var r) &&
            byte.TryParse(parts[2], out var g) &&
            byte.TryParse(parts[3], out var b))
        {
            return new SKColor(r, g, b, a);
        }

        // "R, G, B" format
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

/// <summary>
/// Result of importing a legacy preset.
/// </summary>
public class LegacyPresetImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string PresetName { get; set; } = "";
    public List<MappedEffect> MappedEffects { get; set; } = new();
    public List<string> SkippedEffects { get; set; } = new();
}

/// <summary>
/// A mapped effect ready for instantiation.
/// </summary>
public class MappedEffect
{
    public string TargetTypeName { get; set; } = "";
    public Dictionary<string, object?> Properties { get; set; } = new();
}

/// <summary>
/// Describes how to map a legacy effect to a new one.
/// </summary>
internal record EffectMapping(string TargetTypeName, Dictionary<string, string> PropertyMappings);
