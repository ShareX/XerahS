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

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor;
using ShareX.ImageEditor.Annotations;
using SkiaSharp;

namespace XerahS.RegionCapture.ViewModels;

/// <summary>
/// ViewModel for annotation toolbar in region capture overlay.
/// Provides commands and properties that the AnnotationToolbar expects.
/// XIP-0023: Integrates with EditorCore for annotation management.
/// </summary>
public partial class RegionCaptureAnnotationViewModel : ObservableObject
{
    private readonly EditorCore _editorCore;
    private EditorOptions? _options;

    public RegionCaptureAnnotationViewModel()
    {
        _editorCore = new EditorCore();
        _editorCore.HistoryChanged += OnHistoryChanged;
        _editorCore.AnnotationsRestored += OnAnnotationsRestored;
        _editorCore.InvalidateRequested += OnInvalidateRequested;
    }

    /// <summary>
    /// Loads editor options from settings into the ViewModel.
    /// Call this after construction to restore saved preferences.
    /// </summary>
    public void LoadOptions(EditorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        SelectedColor = ColorToHex(options.BorderColor);
        FillColor = ColorToHex(options.FillColor);
        StrokeWidth = options.Thickness;
        FontSize = options.FontSize;
        ShadowEnabled = options.Shadow;
    }

    /// <summary>
    /// Saves the current ViewModel state back to the options.
    /// Call this before closing to persist user preferences.
    /// </summary>
    public void SaveOptions()
    {
        if (_options == null) return;

        _options.BorderColor = HexToColor(SelectedColor);
        _options.FillColor = HexToColor(FillColor);
        _options.Thickness = StrokeWidth;
        _options.FontSize = FontSize;
        _options.Shadow = ShadowEnabled;
    }

    /// <summary>
    /// Converts an Avalonia Color to hex string format.
    /// </summary>
    private static string ColorToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>
    /// Converts a hex string to Avalonia Color.
    /// </summary>
    private static Color HexToColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7)
            return Colors.Transparent;

        try
        {
            var a = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            var r = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);
            return Color.FromArgb(a, r, g, b);
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    /// <summary>
    /// The underlying EditorCore instance for annotation management.
    /// </summary>
    public EditorCore EditorCore => _editorCore;

    /// <summary>
    /// Event raised when the canvas needs to be redrawn.
    /// </summary>
    public event Action? InvalidateRequested;

    /// <summary>
    /// Event raised when annotations are restored from undo/redo.
    /// </summary>
    public event Action? AnnotationsRestored;

    #region Tool Selection

    [ObservableProperty]
    private EditorTool _activeTool = EditorTool.Select;

    partial void OnActiveToolChanged(EditorTool value)
    {
        _editorCore.ActiveTool = value;
        UpdateToolOptionsVisibility();
    }

    [RelayCommand]
    private void SelectTool(EditorTool tool)
    {
        ActiveTool = tool;
    }

    #endregion

    #region Tool Options Visibility

    private bool _showBorderColor;
    public bool ShowBorderColor
    {
        get => _showBorderColor;
        private set => SetProperty(ref _showBorderColor, value);
    }

    private bool _showFillColor;
    public bool ShowFillColor
    {
        get => _showFillColor;
        private set => SetProperty(ref _showFillColor, value);
    }

    private bool _showThickness;
    public bool ShowThickness
    {
        get => _showThickness;
        private set => SetProperty(ref _showThickness, value);
    }

    private bool _showFontSize;
    public bool ShowFontSize
    {
        get => _showFontSize;
        private set => SetProperty(ref _showFontSize, value);
    }

    private bool _showStrength;
    public bool ShowStrength
    {
        get => _showStrength;
        private set => SetProperty(ref _showStrength, value);
    }

    private bool _showShadow;
    public bool ShowShadow
    {
        get => _showShadow;
        private set => SetProperty(ref _showShadow, value);
    }

    private void UpdateToolOptionsVisibility()
    {
        ShowBorderColor = ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                or EditorTool.Freehand or EditorTool.Highlight or EditorTool.Text
                or EditorTool.SpeechBalloon or EditorTool.Step => true,
            _ => false
        };

        ShowFillColor = ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.SpeechBalloon or EditorTool.Step => true,
            _ => false
        };

        ShowThickness = ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.SmartEraser => true,
            _ => false
        };

        ShowFontSize = ActiveTool switch
        {
            EditorTool.Text or EditorTool.Step => true,
            _ => false
        };

        ShowStrength = ActiveTool switch
        {
            EditorTool.Blur or EditorTool.Pixelate or EditorTool.Magnify or EditorTool.Spotlight => true,
            _ => false
        };

        ShowShadow = ActiveTool switch
        {
            EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
                or EditorTool.Freehand or EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
            _ => false
        };
    }

    #endregion

    #region Colors and Stroke

    private string _selectedColor = "#ef4444";
    public string SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (SetProperty(ref _selectedColor, value))
            {
                _editorCore.StrokeColor = value;
                OnPropertyChanged(nameof(SelectedColorBrush));
            }
        }
    }

    public IBrush SelectedColorBrush
    {
        get => new SolidColorBrush(Color.Parse(_selectedColor));
        set
        {
            if (value is SolidColorBrush brush)
            {
                SelectedColor = $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
            }
        }
    }

    private string _fillColor = "#00000000";
    public string FillColor
    {
        get => _fillColor;
        set
        {
            if (SetProperty(ref _fillColor, value))
            {
                // FillColor is stored locally, not in EditorCore
                OnPropertyChanged(nameof(FillColorBrush));
            }
        }
    }

    public IBrush FillColorBrush
    {
        get => new SolidColorBrush(Color.Parse(_fillColor));
        set
        {
            if (value is SolidColorBrush brush)
            {
                FillColor = $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
            }
        }
    }

    [ObservableProperty]
    private int _strokeWidth = 3;

    partial void OnStrokeWidthChanged(int value)
    {
        _editorCore.StrokeWidth = value;
    }

    [ObservableProperty]
    private float _fontSize = 24;

    // FontSize is stored locally, not in EditorCore

    [ObservableProperty]
    private float _effectStrength = 15;

    // EffectStrength is stored locally, not in EditorCore

    [ObservableProperty]
    private bool _shadowEnabled = false;

    // ShadowEnabled is stored locally, not in EditorCore

    #endregion

    #region History Commands

    private bool _canUndo;
    public bool CanUndo
    {
        get => _canUndo;
        private set => SetProperty(ref _canUndo, value);
    }

    private bool _canRedo;
    public bool CanRedo
    {
        get => _canRedo;
        private set => SetProperty(ref _canRedo, value);
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_editorCore.CanUndo)
        {
            _editorCore.Undo();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_editorCore.CanRedo)
        {
            _editorCore.Redo();
        }
    }

    private void OnHistoryChanged()
    {
        CanUndo = _editorCore.CanUndo;
        CanRedo = _editorCore.CanRedo;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Annotation Management

    private bool _hasSelectedAnnotation;
    public bool HasSelectedAnnotation
    {
        get => _hasSelectedAnnotation;
        set
        {
            if (SetProperty(ref _hasSelectedAnnotation, value))
            {
                DeleteSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _hasAnnotations;
    public bool HasAnnotations
    {
        get => _hasAnnotations;
        set
        {
            if (SetProperty(ref _hasAnnotations, value))
            {
                ClearAnnotationsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedAnnotation))]
    private void DeleteSelected()
    {
        _editorCore.DeleteSelected();
        HasSelectedAnnotation = false;
        HasAnnotations = _editorCore.Annotations.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(HasAnnotations))]
    private void ClearAnnotations()
    {
        _editorCore.ClearAll();
        HasAnnotations = false;
        HasSelectedAnnotation = false;
    }

    #endregion

    #region Event Handlers

    private void OnAnnotationsRestored()
    {
        HasAnnotations = _editorCore.Annotations.Count > 0;
        AnnotationsRestored?.Invoke();
    }

    private void OnInvalidateRequested()
    {
        InvalidateRequested?.Invoke();
    }

    #endregion

    #region Image Loading

    /// <summary>
    /// Loads the background image into EditorCore for annotation rendering.
    /// </summary>
    public void LoadBackgroundImage(SKBitmap bitmap)
    {
        _editorCore.LoadImage(bitmap);
    }

    #endregion
}

