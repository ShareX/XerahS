using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Editor.ImageEffects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ShareX.Ava.UI.ViewModels
{
    /// <summary>
    /// ViewModel wrapper for ImageEffect instances with observable properties
    /// </summary>
    public partial class EffectViewModel : ObservableObject
    {
        private readonly ImageEffect _effectInstance;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        public ImageEffect EffectInstance => _effectInstance;

        public EffectViewModel(ImageEffect effect)
        {
            _effectInstance = effect ?? throw new ArgumentNullException(nameof(effect));
            
            // Extract metadata from effect instance
            var type = effect.GetType();
            _name = type.Name;
            _category = DetermineCategory(type);
            _description = GetDescriptionFromAttribute(type);
        }

        /// <summary>
        /// Apply the effect to the given bitmap
        /// </summary>
        public SKBitmap Apply(SKBitmap bitmap)
        {
            return _effectInstance.Apply(bitmap);
        }

        /// <summary>
        /// Reset effect parameters to default values
        /// </summary>
        public void ResetParameters()
        {
            // Reset all properties to default via reflection (if needed)
            // For now, recreate instance with default constructor
            var type = _effectInstance.GetType();
            var newInstance = Activator.CreateInstance(type);
            
            // Copy properties from new instance to current instance
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanWrite && prop.CanRead)
                {
                    var value = prop.GetValue(newInstance);
                    prop.SetValue(_effectInstance, value);
                }
            }
        }

        /// <summary>
        /// Determine effect category from namespace
        /// </summary>
        private static string DetermineCategory(Type effectType)
        {
            var ns = effectType.Namespace ?? string.Empty;
            
            if (ns.Contains("Filters")) return "Filters";
            if (ns.Contains("Adjustments")) return "Adjustments";
            if (ns.Contains("Manipulations")) return "Manipulations";
            if (ns.Contains("Drawings")) return "Drawings";
            
            return "Other";
        }

        /// <summary>
        /// Get description from Description attribute
        /// </summary>
        private static string GetDescriptionFromAttribute(Type effectType)
        {
            var attr = effectType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return attr?.Description ?? "No description available";
        }
    }
}
