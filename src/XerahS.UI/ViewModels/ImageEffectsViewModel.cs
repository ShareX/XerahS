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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using ShareX.Editor.Extensions;
using ShareX.Editor.ImageEffects;
using ShareX.Editor.ImageEffects.Manipulations;
using SkiaSharp;
using System.Collections.ObjectModel;
using XerahS.Common;
using XerahS.Common.Helpers;
using XerahS.Core;
using XerahS.Core.Helpers;

namespace XerahS.UI.ViewModels
{
    public partial class ImageEffectsViewModel : ViewModelBase
    {
        private TaskSettingsImage settings;
        private SKBitmap? sourcePreviewBitmap;
        private const int PreviewSize = 256;

        public ObservableCollection<ImageEffectPreset> Presets { get; private set; }

        private ImageEffectPreset? selectedPreset;
        public ImageEffectPreset? SelectedPreset
        {
            get => selectedPreset;
            set
            {
                if (SetProperty(ref selectedPreset, value))
                {
                    UpdateEffectsList();
                    UpdatePreview();
                }
            }
        }

        public ObservableCollection<ImageEffect> Effects { get; private set; } = new ObservableCollection<ImageEffect>();

        private ImageEffect? selectedEffect;
        public ImageEffect? SelectedEffect
        {
            get => selectedEffect;
            set => SetProperty(ref selectedEffect, value);
        }

        public List<EffectCategory> AvailableEffects { get; private set; } = new();

        private Bitmap? previewBitmap;
        public Bitmap? PreviewBitmap
        {
            get => previewBitmap;
            private set => SetProperty(ref previewBitmap, value);
        }

        public ImageEffectsViewModel(TaskSettingsImage settings)
        {
            this.settings = settings;
            Presets = new ObservableCollection<ImageEffectPreset>(settings.ImageEffectPresets);

            foreach (var preset in Presets)
            {
                preset.Effects ??= new List<ImageEffect>();
            }

            if (Presets.Count > 0)
            {
                int index = Math.Clamp(settings.SelectedImageEffectPreset, 0, Presets.Count - 1);
                SelectedPreset = Presets[index];
            }
            else
            {
                AddPreset();
            }

            InitializeAvailableEffects();
            GeneratePreviewImage();
            UpdatePreview();
        }

        private void InitializeAvailableEffects()
        {
            var effectTypes = typeof(ImageEffect).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(ImageEffect).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => new { Type = t, Instance = Activator.CreateInstance(t) as ImageEffect })
                .Where(x => x.Instance != null)
                .ToList();

            AvailableEffects = effectTypes
                .GroupBy(x => x.Instance!.Category)
                .OrderBy(x => x.Key)
                .Select(group => new EffectCategory(group.Key.ToString(), group.Select(x => x.Type).ToArray()))
                .ToList();
        }

        private void GeneratePreviewImage()
        {
            sourcePreviewBitmap?.Dispose();

            // Try to load Logo2.png from assets
            try
            {
                var uri = new Uri("avares://ShareX.Avalonia.UI/Assets/Logo2.png");
                using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                sourcePreviewBitmap = SKBitmap.Decode(stream);
            }
            catch
            {
                // Fallback to a simple generated image if logo fails to load
                sourcePreviewBitmap = new SKBitmap(PreviewSize, PreviewSize);
                using var canvas = new SKCanvas(sourcePreviewBitmap);

                using var gradientPaint = new SKPaint();
                var colors = new SKColor[] { new SKColor(70, 130, 180), new SKColor(135, 206, 235) };
                gradientPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(PreviewSize, PreviewSize),
                    colors,
                    null,
                    SKShaderTileMode.Clamp);
                canvas.DrawRect(0, 0, PreviewSize, PreviewSize, gradientPaint);

                using var textPaint = new SKPaint
                {
                    Color = SKColors.White,
                    TextSize = 24,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
                canvas.DrawText("Preview", PreviewSize / 2, PreviewSize / 2, textPaint);
            }
        }

        public void UpdatePreview()
        {
            if (sourcePreviewBitmap == null) return;

            SKBitmap result = sourcePreviewBitmap.Copy();

            try
            {
                if (SelectedPreset != null)
                {
                    foreach (var effect in SelectedPreset.Effects)
                    {
                        var processed = effect.Apply(result);
                        if (processed != result)
                        {
                            result.Dispose();
                            result = processed;
                        }
                    }
                }

                // Convert SKBitmap to Avalonia Bitmap
                using var data = result.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = new MemoryStream();
                data.SaveTo(stream);
                stream.Position = 0;
                PreviewBitmap = new Bitmap(stream);
            }
            finally
            {
                result.Dispose();
            }
        }

        [RelayCommand]
        public void RefreshPreview()
        {
            UpdatePreview();
        }

        private void UpdateEffectsList()
        {
            Effects.Clear();
            if (SelectedPreset != null)
            {
                SelectedPreset.Effects ??= new List<ImageEffect>();
                foreach (var effect in SelectedPreset.Effects)
                {
                    Effects.Add(effect);
                }
            }

            SelectedEffect = Effects.FirstOrDefault();
        }

        [RelayCommand]
        public void AddPreset()
        {
            var preset = new ImageEffectPreset { Name = "New preset" };
            AddPreset(preset);
            SelectedPreset = preset;
        }

        [RelayCommand]
        public void RemovePreset()
        {
            if (SelectedPreset != null && Presets.Count > 1)
            {
                var preset = SelectedPreset;
                int index = Presets.IndexOf(preset);
                Presets.Remove(preset);
                settings.ImageEffectPresets.Remove(preset);
                SelectedPreset = Presets[Math.Clamp(index, 0, Presets.Count - 1)];
            }
        }

        [RelayCommand]
        public void AddEffect(Type effectType)
        {
            if (SelectedPreset != null && Activator.CreateInstance(effectType) is ImageEffect effect)
            {
                SelectedPreset.Effects.Add(effect);
                Effects.Add(effect);
                SelectedEffect = effect;
                UpdatePreview();
            }
        }

        [RelayCommand]
        public void RemoveEffect()
        {
            if (SelectedPreset != null && SelectedEffect != null)
            {
                var effect = SelectedEffect;
                SelectedPreset.Effects.Remove(effect);
                Effects.Remove(effect);
                SelectedEffect = Effects.FirstOrDefault();
                UpdatePreview();
            }
        }

        [RelayCommand]
        public async Task SavePresetAsync()
        {
            if (SelectedPreset == null)
                return;

            var topLevel = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel?.StorageProvider == null)
            {
                DebugHelper.WriteLine("[ImageEffects] Unable to open save picker (no window).");
                return;
            }

            var options = new FilePickerSaveOptions
            {
                Title = "Save Image Effects Preset",
                SuggestedFileName = string.IsNullOrWhiteSpace(SelectedPreset.Name) ? "Preset.xsie" : $"{SelectedPreset.Name}.xsie",
                DefaultExtension = "xsie",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("XerahS Image Effects") { Patterns = new[] { "*.xsie" } },
                    new FilePickerFileType("ShareX Image Effects") { Patterns = new[] { "*.sxie" } }
                }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file == null)
                return;

            var filePath = file.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                if (extension == ".sxie")
                {
                    var result = LegacyImageEffectExporter.ExportSxieFile(filePath, SelectedPreset.Name, SelectedPreset.Effects);
                    if (!result.Success)
                    {
                        DebugHelper.WriteLine($"[ImageEffects] Legacy export failed: {result.ErrorMessage}");
                    }
                }
                else
                {
                    if (extension != ".xsie")
                    {
                        filePath = $"{filePath}.xsie";
                    }

                    ImageEffectPresetSerializer.SaveXsieFile(filePath, SelectedPreset);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to save image effects preset.");
            }
        }

        [RelayCommand]
        public async Task LoadPresetAsync()
        {
            var topLevel = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel?.StorageProvider == null)
            {
                DebugHelper.WriteLine("[ImageEffects] Unable to open file picker (no window).");
                return;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "Load Image Effects Preset",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Effects Preset") { Patterns = new[] { "*.xsie", "*.sxie" } }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
                return;

            var filePath = files[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                ImageEffectPreset? preset = extension == ".sxie"
                    ? LoadLegacyPreset(filePath)
                    : ImageEffectPresetSerializer.LoadXsieFile(filePath);

                if (preset == null)
                    return;

                AddPreset(preset);
                SelectedPreset = preset;
                UpdatePreview();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load image effects preset.");
            }
        }

        private ImageEffectPreset? LoadLegacyPreset(string filePath)
        {
            var importResult = LegacyImageEffectImporter.ImportSxieFile(filePath);
            if (importResult == null || !importResult.Success)
            {
                DebugHelper.WriteLine($"[ImageEffects] Legacy import failed: {importResult?.ErrorMessage}");
                return null;
            }

            var preset = new ImageEffectPreset
            {
                Name = importResult.PresetName ?? "Imported Preset",
                MappedEffects = importResult.MappedEffects.Select(mapped => new MappedEffectData
                {
                    TargetTypeName = mapped.TargetTypeName,
                    Properties = mapped.Properties
                }).ToList()
            };

            foreach (var mapped in importResult.MappedEffects)
            {
                var effect = CreateEffectFromMapped(mapped);
                if (effect != null)
                {
                    preset.Effects.Add(effect);
                }
            }

            return preset;
        }

        private static ImageEffect? CreateEffectFromMapped(MappedEffect mapped)
        {
            if (string.IsNullOrWhiteSpace(mapped.TargetTypeName))
                return null;

            if (mapped.TargetTypeName == nameof(RotateImageEffect))
            {
                if (mapped.Properties.TryGetValue("Angle", out var angleValue))
                {
                    var angle = ReadSingle(angleValue, 0f);
                    return RotateImageEffect.Custom(angle);
                }
            }

            if (mapped.TargetTypeName == nameof(FlipImageEffect))
            {
                bool horizontal = mapped.Properties.TryGetValue("Horizontal", out var horizontalValue) && Convert.ToBoolean(horizontalValue);
                bool vertical = mapped.Properties.TryGetValue("Vertical", out var verticalValue) && Convert.ToBoolean(verticalValue);

                if (vertical && !horizontal)
                    return FlipImageEffect.Vertical;

                return FlipImageEffect.Horizontal;
            }

            if (mapped.TargetTypeName == nameof(ResizeImageEffect))
            {
                int width = mapped.Properties.TryGetValue("_width", out var widthValue) ? ReadInt(widthValue, 0) : 0;
                int height = mapped.Properties.TryGetValue("_height", out var heightValue) ? ReadInt(heightValue, 0) : 0;
                return new ResizeImageEffect(width, height);
            }

            var assembly = typeof(ImageEffect).Assembly;
            var type = assembly.GetTypes().FirstOrDefault(t => t.Name.Equals(mapped.TargetTypeName, StringComparison.Ordinal));
            if (type == null)
                return null;

            if (Activator.CreateInstance(type) is not ImageEffect effect)
                return null;

            ApplyMappedProperties(effect, mapped.Properties);
            return effect;
        }

        private static void ApplyMappedProperties(ImageEffect effect, Dictionary<string, object?> properties)
        {
            var type = effect.GetType();

            foreach (var pair in properties)
            {
                var property = type.GetProperty(pair.Key);
                if (property == null || !property.CanWrite)
                    continue;

                var converted = ConvertPropertyValue(pair.Value, property.PropertyType);
                property.SetValue(effect, converted);
            }
        }

        private static object? ConvertPropertyValue(object? value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsInstanceOfType(value))
                return value;

            if (value is Newtonsoft.Json.Linq.JToken token)
                return token.ToObject(targetType);

            if (targetType == typeof(SKColor))
            {
                if (value is SKColor color)
                    return color;
            }

            if (targetType.IsEnum)
            {
                if (value is string text)
                    return Enum.Parse(targetType, text, ignoreCase: true);

                return Enum.ToObject(targetType, value);
            }

            return Convert.ChangeType(value, targetType);
        }

        private static float ReadSingle(object? value, float fallback)
        {
            if (value == null)
                return fallback;

            if (value is Newtonsoft.Json.Linq.JToken token)
                return token.ToObject<float>();

            return Convert.ToSingle(value);
        }

        private static int ReadInt(object? value, int fallback)
        {
            if (value == null)
                return fallback;

            if (value is Newtonsoft.Json.Linq.JToken token)
                return token.ToObject<int>();

            return Convert.ToInt32(value);
        }

        private void AddPreset(ImageEffectPreset preset)
        {
            preset.Effects ??= new List<ImageEffect>();
            Presets.Add(preset);
            settings.ImageEffectPresets.Add(preset);
        }

        // TODO: Move logic, rename, duplicate, etc.
    }

    public class EffectCategory
    {
        public string Name { get; }
        public List<EffectType> Effects { get; }

        public EffectCategory(string name, params Type[] types)
        {
            Name = name;
            Effects = types.Select(t => new EffectType(t)).ToList();
        }
    }

    public class EffectType
    {
        public string Name { get; }
        public Type Type { get; }

        public EffectType(Type type)
        {
            Type = type;
            Name = type.GetDescription() ?? type.Name;
        }
    }
}

