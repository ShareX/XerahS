using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;
using System.ComponentModel;
using System.Reflection;

namespace XerahS.UI.Controls
{
    public partial class PropertyGrid : UserControl
    {
        public static readonly StyledProperty<object?> SelectedObjectProperty =
            AvaloniaProperty.Register<PropertyGrid, object?>(nameof(SelectedObject));

        public object? SelectedObject
        {
            get => GetValue(SelectedObjectProperty);
            set => SetValue(SelectedObjectProperty, value);
        }

        public event EventHandler? PropertyValueChanged;

        public PropertyGrid()
        {
            InitializeComponent();
            this.GetObservable(SelectedObjectProperty).Subscribe(new SimpleObserver<object?>(OnSelectedObjectChanged));
        }

        private class SimpleObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            public SimpleObserver(Action<T> onNext) => _onNext = onNext;
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(T value) => _onNext(value);
        }

        private void OnSelectedObjectChanged(object? obj)
        {
            PropertiesPanel.Children.Clear();

            if (obj == null) return;

            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.CanRead)
                .Where(p =>
                {
                    var attr = p.GetCustomAttribute<BrowsableAttribute>();
                    return attr == null || attr.Browsable;
                })
                .OrderBy(p =>
                {
                    // Basic ordering, maybe by MetadataToken or Category later
                    return p.Name;
                });

            foreach (var prop in properties)
            {
                var row = CreatePropertyRow(obj, prop);
                if (row != null)
                {
                    PropertiesPanel.Children.Add(row);
                }
            }
        }

        private Control? CreatePropertyRow(object obj, PropertyInfo prop)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("140, *"),
                Classes = { "propertyRow" }
            };

            // Label
            var label = new TextBlock
            {
                Text = GetDisplayName(prop),
                Classes = { "propertyName" }
            };
            ToolTip.SetTip(label, GetDescription(prop));
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // Editor
            var editor = CreateEditor(obj, prop);
            if (editor == null) return null; // Skip unsupported types for now

            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);

            return grid;
        }

        private Control? CreateEditor(object obj, PropertyInfo prop)
        {
            var type = prop.PropertyType;
            var binding = new Binding(prop.Name)
            {
                Source = obj,
                Mode = BindingMode.TwoWay
            };

            if (type == typeof(bool))
            {
                var checkBox = new CheckBox();
                checkBox.Bind(CheckBox.IsCheckedProperty, binding);
                checkBox.IsCheckedChanged += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return checkBox;
            }
            if (type.IsEnum)
            {
                var comboBox = new ComboBox();
                comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                comboBox.ItemsSource = Enum.GetValues(type);
                comboBox.Bind(ComboBox.SelectedItemProperty, binding);
                comboBox.SelectionChanged += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return comboBox;
            }
            if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            {
                var nud = new NumericUpDown();
                nud.Increment = 1;
                nud.Bind(NumericUpDown.ValueProperty, binding);
                nud.ValueChanged += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return nud;
            }
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                var nud = new NumericUpDown();
                nud.Increment = 0.1m;
                nud.FormatString = "0.00";
                nud.Bind(NumericUpDown.ValueProperty, binding);
                nud.ValueChanged += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return nud;
            }
            if (type == typeof(string))
            {
                var textBox = new TextBox();
                textBox.Bind(TextBox.TextProperty, binding);
                textBox.LostFocus += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return textBox;
            }
            if (type == typeof(System.Drawing.Color))
            {
                var textBox = new TextBox();
                binding.Converter = new ColorStringConverter();
                textBox.Bind(TextBox.TextProperty, binding);
                textBox.LostFocus += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return textBox;
            }
            if (type == typeof(SKColor))
            {
                var textBox = new TextBox();
                binding.Converter = new SKColorStringConverter();
                textBox.Bind(TextBox.TextProperty, binding);
                textBox.LostFocus += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return textBox;
            }

            // Fallback for complex types?
            if (type.IsClass && type != typeof(string))
            {
                // Nested expandable? Too complex for now.
                return new TextBlock { Text = $"({type.Name})", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
            }

            return new TextBox { Text = $"(Unsupported {type.Name})", IsReadOnly = true };
        }

        private string GetDisplayName(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<DisplayNameAttribute>();
            return attr?.DisplayName ?? prop.Name;
        }

        private string GetDescription(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? "";
        }

        private class ColorStringConverter : Avalonia.Data.Converters.IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is System.Drawing.Color c)
                {
                    if (c.IsNamedColor) return c.Name;
                    return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
                return value?.ToString();
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is string s)
                {
                    try
                    {
                        if (s.StartsWith("#"))
                        {
                            return System.Drawing.ColorTranslator.FromHtml(s);
                        }
                        return System.Drawing.Color.FromName(s);
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }
                return BindingOperations.DoNothing;
            }
        }

        private class SKColorStringConverter : Avalonia.Data.Converters.IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is SKColor c)
                {
                    return c.ToString();
                }
                return value?.ToString();
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is string s)
                {
                    if (SKColor.TryParse(s, out var color))
                    {
                        return color;
                    }
                }
                return BindingOperations.DoNothing;
            }
        }
    }
}
