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

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using XerahS.Editor.ImageEffects.Adjustments;
using SkiaSharp;
using System.IO.Compression;
using XerahS.Common.Helpers;
using XerahS.Core;
using XerahS.Core.Helpers;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class ImageEffectPresetSerializerTests
{
    [Test]
    public void SaveLoadXsie_RoundTripsEffects()
    {
        var preset = new ImageEffectPreset
        {
            Name = "TestPreset",
            Effects =
            {
                new BrightnessImageEffect { Amount = 25 },
                new ColorizeImageEffect { Strength = 40, Color = new SKColor(10, 20, 30, 40) }
            }
        };

        var path = Path.Combine(Path.GetTempPath(), $"xip0020-{Guid.NewGuid():N}.xsie");

        try
        {
            ImageEffectPresetSerializer.SaveXsieFile(path, preset);
            var loaded = ImageEffectPresetSerializer.LoadXsieFile(path);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name, Is.EqualTo("TestPreset"));
            Assert.That(loaded.Effects.Count, Is.EqualTo(2));

            Assert.That(loaded.Effects[0], Is.TypeOf<BrightnessImageEffect>());
            var brightness = (BrightnessImageEffect)loaded.Effects[0];
            Assert.That(brightness.Amount, Is.EqualTo(25).Within(0.01));

            Assert.That(loaded.Effects[1], Is.TypeOf<ColorizeImageEffect>());
            var colorize = (ColorizeImageEffect)loaded.Effects[1];
            Assert.That(colorize.Strength, Is.EqualTo(40).Within(0.01));
            Assert.That(colorize.Color, Is.EqualTo(new SKColor(10, 20, 30, 40)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void ExportSxie_WritesLegacyConfig()
    {
        var effects = new object[]
        {
            new BrightnessImageEffect { Amount = 15 },
            new ColorizeImageEffect { Strength = 60, Color = new SKColor(200, 10, 20, 255) }
        };

        var path = Path.Combine(Path.GetTempPath(), $"xip0020-{Guid.NewGuid():N}.sxie");

        try
        {
            var result = LegacyImageEffectExporter.ExportSxieFile(path, "LegacyPreset", effects);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("Config.json");
            Assert.That(entry, Is.Not.Null);

            using var reader = new StreamReader(entry!.Open());
            var configJson = reader.ReadToEnd();

            var json = JObject.Parse(configJson);
            Assert.That(json["Name"]?.ToString(), Is.EqualTo("LegacyPreset"));

            var effectsArray = json["Effects"] as JArray;
            Assert.That(effectsArray, Is.Not.Null);
            Assert.That(effectsArray!.Count, Is.EqualTo(2));

            Assert.That(effectsArray[0]?["$type"]?.ToString(), Is.EqualTo("ShareX.ImageEffectsLib.Brightness, ShareX.ImageEffectsLib"));
            Assert.That(effectsArray[1]?["$type"]?.ToString(), Is.EqualTo("ShareX.ImageEffectsLib.Colorize, ShareX.ImageEffectsLib"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
