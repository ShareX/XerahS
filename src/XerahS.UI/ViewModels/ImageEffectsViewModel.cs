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
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ShareX.Editor;
using ShareX.Editor.Extensions;
using ShareX.Editor.ImageEffects;
using ShareX.Editor.ImageEffects.Manipulations;
using SkiaSharp;
using System;
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
        private EditorCore editorCore;
        private SKBitmap? sourcePreviewBitmap;
        private const int PreviewSize = 256;
        private bool isSyncSuspended;

        private bool canUndo;
        public bool CanUndo
        {
            get => canUndo;
            private set => SetProperty(ref canUndo, value);
        }

        private bool canRedo;
        public bool CanRedo
        {
            get => canRedo;
            private set => SetProperty(ref canRedo, value);
        }

        private string name = "New preset";
        public string Name
        {
            get => name;
            set
            {
                if (SetProperty(ref name, value))
                {
                    SyncToSettings();
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

        public ImageEffectsViewModel(TaskSettingsImage settings, EditorCore editorCore)
        {
            this.settings = settings;
            this.editorCore = editorCore;

            InitializeAvailableEffects();
            GeneratePreviewImage();

            var preset = settings.ImageEffectsPreset ?? ImageEffectPreset.GetDefaultPreset();
            isSyncSuspended = true;
            try
            {
                Name = string.IsNullOrWhiteSpace(preset.Name) ? "Preset" : preset.Name;
                editorCore.LoadEffects(preset.Effects ?? new List<ImageEffect>());
                SyncFromCore();
            }
            finally
            {
                isSyncSuspended = false;
                SyncToSettings();
            }
            UpdatePreview();

            editorCore.EffectsChanged += OnEffectsChanged;
            editorCore.HistoryChanged += OnHistoryChanged;
            Effects.CollectionChanged += (s, e) => SyncToSettings();
        }

        private void OnEffectsChanged()
        {
            isSyncSuspended = true;
            try
            {
                SyncFromCore();
            }
            finally
            {
                isSyncSuspended = false;
                SyncToSettings();
            }
            UpdatePreview();
        }

        private void OnHistoryChanged()
        {
            CanUndo = editorCore.CanUndo;
            CanRedo = editorCore.CanRedo;
        }

        private void SyncFromCore()
        {
            Effects.Clear();
            foreach (var effect in editorCore.Effects)
            {
                Effects.Add(effect);
            }
            SelectedEffect = Effects.FirstOrDefault();
        }

        private void SyncToSettings()
        {
            if (isSyncSuspended)
                return;

            if (settings.ImageEffectsPreset == null)
            {
                settings.ImageEffectsPreset = new ImageEffectPreset();
            }

            var preset = settings.ImageEffectsPreset;
            preset.Name = Name;
            preset.Effects = Effects.ToList();
        }

        private void ApplyPreset(ImageEffectPreset preset, bool updatePreview)
        {
            var effects = preset.Effects ?? new List<ImageEffect>();
            isSyncSuspended = true;
            try
            {
                Name = string.IsNullOrWhiteSpace(preset.Name) ? "Preset" : preset.Name;
            }
            finally
            {
                isSyncSuspended = false;
            }
            editorCore.SetEffects(effects);
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
            sourcePreviewBitmap = null;

            try
            {
                sourcePreviewBitmap = new SKBitmap(PreviewSize, PreviewSize);
                using var canvas = new SKCanvas(sourcePreviewBitmap);

                using var bgPaint = new SKPaint { Color = SKColors.White };
                canvas.DrawRect(0, 0, PreviewSize, PreviewSize, bgPaint);

                using var paint = new SKPaint
                {
                    Color = new SKColor(70, 130, 180),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                // Draw an 'F' shape or similar asymmetric pattern
                float padding = PreviewSize * 0.2f;
                float width = PreviewSize * 0.6f;
                float height = PreviewSize * 0.6f;
                float thickness = width * 0.25f;

                // Vertical bar
                canvas.DrawRect(padding, padding, thickness, height, paint);
                // Top horizontal bar
                canvas.DrawRect(padding, padding, width, thickness, paint);
                // Middle horizontal bar
                canvas.DrawRect(padding, padding + height * 0.4f, width * 0.7f, thickness, paint);

                using var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 20,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Left
                };
                canvas.DrawText("Preview", padding, PreviewSize - padding / 2, textPaint);
            }
            catch
            {
                // Ignore errors
            }
        }

        public void UpdatePreview()
        {
            if (sourcePreviewBitmap == null) return;

            SKBitmap result = sourcePreviewBitmap.Copy();

            try
            {
                foreach (var effect in Effects)
                {
                    if (!effect.IsEnabled) continue;

                    var processed = effect.Apply(result);
                    if (processed != result)
                    {
                        result.Dispose();
                        result = processed;
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

        [RelayCommand]
        public void Undo()
        {
            editorCore.Undo();
        }

        [RelayCommand]
        public void Redo()
        {
            editorCore.Redo();
        }

        [RelayCommand]
        public void ToggleEffect(ImageEffect? effect)
        {
            if (effect == null) return;
            editorCore.ToggleEffect(effect);
        }

        [RelayCommand]
        public void AddEffect(Type effectType)
        {
            if (Activator.CreateInstance(effectType) is ImageEffect effect)
            {
                editorCore.AddEffect(effect);
                SelectedEffect = Effects.LastOrDefault();
            }
        }

        [RelayCommand]
        public void RemoveEffect()
        {
            if (SelectedEffect != null)
            {
                editorCore.RemoveEffect(SelectedEffect);
                SelectedEffect = Effects.FirstOrDefault();
            }
        }

        [RelayCommand]
        public async Task SavePresetAsync()
        {
            if (Effects == null)
                return;

            var topLevel = GetMainWindow();
            if (topLevel?.StorageProvider == null)
            {
                DebugHelper.WriteLine("[ImageEffects] Unable to open save picker (no window).");
                return;
            }

            var options = new FilePickerSaveOptions
            {
                Title = "Save Image Effects Preset",
                SuggestedFileName = string.IsNullOrWhiteSpace(Name) ? "Preset.xsie" : $"{Name}.xsie",
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

            var enabledEffects = Effects.Where(e => e.IsEnabled).ToList();
            var presetToSave = new ImageEffectPreset
            {
                Name = Name,
                Effects = enabledEffects
            };

            try
            {
                if (extension == ".sxie")
                {
                    var result = LegacyImageEffectExporter.ExportSxieFile(filePath, presetToSave.Name, presetToSave.Effects);
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

                    ImageEffectPresetSerializer.SaveXsieFile(filePath, presetToSave);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to save image effects preset.");
            }
        }

        [RelayCommand]
        public async Task ImportEffectsAsync()
        {
            var preset = await LoadPresetFromPickerAsync("Import Image Effects");
            if (preset == null)
                return;

            ApplyPreset(preset, updatePreview: true);
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

        private async Task<ImageEffectPreset?> LoadPresetFromPickerAsync(string title)
        {
            var topLevel = GetMainWindow();
            if (topLevel?.StorageProvider == null)
            {
                DebugHelper.WriteLine("[ImageEffects] Unable to open file picker (no window).");
                return null;
            }

            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Effects Preset") { Patterns = new[] { "*.xsie", "*.sxie" } }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
                return null;

            var filePath = files[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                var preset = extension == ".sxie"
                    ? LoadLegacyPreset(filePath)
                    : ImageEffectPresetSerializer.LoadXsieFile(filePath);

                if (preset != null)
                {
                    preset.Name = Path.GetFileNameWithoutExtension(filePath);
                }

                return preset;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load image effects preset.");
                return null;
            }
        }

        private static Window? GetMainWindow()
        {
            return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
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

            string? name = null;
            try
            {
                if (Activator.CreateInstance(type) is ShareX.Editor.ImageEffects.ImageEffect effect)
                {
                    name = effect.Name;
                }
            }
            catch
            {
            }

            Name = name ?? ShareX.Editor.Extensions.TypeExtensions.GetDescription(type) ?? type.Name;
        }
    }
}
