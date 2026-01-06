using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core;
using ShareX.Editor;
using ShareX.Editor.ImageEffects;
using ShareX.Editor.ImageEffects.Adjustments;
using ShareX.Editor.ImageEffects.Drawings;
using ShareX.Editor.ImageEffects.Manipulations;
using ShareX.Editor.Extensions;

namespace ShareX.Ava.UI.ViewModels
{
    public partial class ImageEffectsViewModel : ViewModelBase
    {
        private TaskSettingsImage settings;

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
