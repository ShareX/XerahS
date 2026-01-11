using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using ShareX.Editor.Extensions;
using ShareX.Editor.ImageEffects;
using ShareX.Editor.ImageEffects.Adjustments;
using ShareX.Editor.ImageEffects.Drawings;
using ShareX.Editor.ImageEffects.Manipulations;
using SkiaSharp;
using System.Collections.ObjectModel;

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

        public List<EffectCategory> AvailableEffects { get; private set; }

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
            AvailableEffects = new List<EffectCategory>
            {
                new EffectCategory("Drawings", typeof(DrawBackground), typeof(DrawBackgroundImage), typeof(DrawBorder), typeof(DrawCheckerboard), typeof(DrawImage), typeof(DrawText)),
                new EffectCategory("Manipulations", typeof(AutoCrop), typeof(Canvas), typeof(Crop), typeof(Flip), typeof(ForceProportions), typeof(Resize), typeof(Rotate), typeof(RoundedCorners), typeof(Scale), typeof(Skew)),
                new EffectCategory("Adjustments", typeof(Alpha), typeof(BlackWhite), typeof(Brightness), typeof(MatrixColor), typeof(Colorize), typeof(Contrast), typeof(Gamma), typeof(Grayscale), typeof(Hue), typeof(Inverse), typeof(Polaroid), typeof(Saturation), typeof(Sepia)),
                new EffectCategory("Filters", typeof(Blur), typeof(ColorDepth), typeof(MatrixConvolution), typeof(EdgeDetect), typeof(Emboss), typeof(GaussianBlur), typeof(Glow), typeof(MeanRemoval), typeof(Outline), typeof(Pixelate), typeof(Reflection), typeof(RGBSplit), typeof(Shadow), typeof(Sharpen), typeof(Slice), typeof(Smooth), typeof(TornEdge), typeof(WaveEdge))
            };
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
                        if (effect.Enabled)
                        {
                            var processed = effect.Apply(result);
                            if (processed != result)
                            {
                                result.Dispose();
                                result = processed;
                            }
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
            Presets.Add(preset);
            settings.ImageEffectPresets.Add(preset);
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

