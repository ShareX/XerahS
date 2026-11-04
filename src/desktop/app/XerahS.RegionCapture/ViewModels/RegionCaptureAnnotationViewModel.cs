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

using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Core.Abstractions;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Theming;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AnnotationBorderStyle = ShareX.ImageEditor.Core.Annotations.BorderStyle;
using ShareXStepTailStyle = ShareX.ImageEditor.Core.Annotations.StepTailStyle;

namespace XerahS.RegionCapture.ViewModels;

public partial class RegionCaptureAnnotationViewModel : ObservableObject, IAnnotationToolbarAdapter
{
    private static readonly IReadOnlyList<string> _availableFontFamilies = ["Segoe UI", "Arial", "Calibri", "Consolas", "Times New Roman"];
    private static readonly IReadOnlyList<ArrowStyle> _availableArrowStyles = Enum.GetValues<ArrowStyle>();
    private static readonly IReadOnlyList<CursorType> _availableCursorTypes = Enum.GetValues<CursorType>();
    private static readonly IReadOnlyList<int> _availableStepStartNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    private static readonly IReadOnlyList<AnnotationBorderStyle> _availableBorderStyles = Enum.GetValues<AnnotationBorderStyle>();
    private static readonly IReadOnlyList<StepType> _availableStepTypes = Enum.GetValues<StepType>();
    private static readonly IReadOnlyList<TextHorizontalAlignment> _availableTextHorizontalAlignments = Enum.GetValues<TextHorizontalAlignment>();

    private const float MinEffectStrength = 1;
    private const float MaxBlurStrength = 200;
    private const float MaxPixelateStrength = 200;
    private const float MaxMagnifyStrength = 10;
    private const float MaxSpotlightStrength = 100;

    private readonly EditorCore _editorCore;
    private ImageEditorOptions _options = new();
    private bool _isLoadingToolOptions;
    private Annotation? _selectedAnnotation;
    private bool _canUndo;
    private bool _canRedo;
    private bool _hasSelectedAnnotation;
    private bool _hasAnnotations;
    private readonly ObservableCollection<MenuItem> _recentImageMenuItems = new();
    private readonly ObservableCollection<string> _recentImageFiles = new();
    private readonly ObservableCollection<ToolbarCustomizationItemViewModel> _visibleToolbarItems = new();

    public RegionCaptureAnnotationViewModel()
    {
        _editorCore = new EditorCore();
        _editorCore.HistoryChanged += OnHistoryChanged;
        _editorCore.AnnotationsRestored += OnAnnotationsRestored;
        _editorCore.InvalidateRequested += OnInvalidateRequested;
        RecentImageMenuItems = new ReadOnlyObservableCollection<MenuItem>(_recentImageMenuItems);
        RecentImageFiles = new ReadOnlyObservableCollection<string>(_recentImageFiles);
        VisibleToolbarItems = new ReadOnlyObservableCollection<ToolbarCustomizationItemViewModel>(_visibleToolbarItems);
        InitializeVisibleToolbarItems();
        OpenRecentImageCommand = new RelayCommand<string?>(_ => { });
        OpenOptionsPanelCommand = new RelayCommand(() => { });
        NewImageCommand = new RelayCommand(() => { });
        OpenImageCommand = new RelayCommand(() => { });
        SaveCommand = new RelayCommand(() => { });
        SaveAsCommand = new RelayCommand(() => { });
        ExitEditorCommand = new RelayCommand(() => { });
    }

    public EditorCore EditorCore => _editorCore;

    public bool ImageEditorMode => false;

    public ReadOnlyObservableCollection<MenuItem> RecentImageMenuItems { get; }

    public ReadOnlyObservableCollection<string> RecentImageFiles { get; }

    /// <summary>
    /// Tool buttons for <see cref="ShareX.ImageEditor.Presentation.Controls.AnnotationToolbar"/>.
    /// Required because the shared toolbar binds <c>VisibleToolbarItems</c> via reflection
    /// (editor hosts get this from <c>MainViewModel</c>; region capture must supply its own).
    /// </summary>
    public ReadOnlyObservableCollection<ToolbarCustomizationItemViewModel> VisibleToolbarItems { get; }

    public bool HasRecentImageFiles => false;

    public ICommand OpenRecentImageCommand { get; }

    public ICommand OpenOptionsPanelCommand { get; }

    public ICommand NewImageCommand { get; }

    public ICommand OpenImageCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand SaveAsCommand { get; }

    public ICommand ExitEditorCommand { get; }

    public event Action? InvalidateRequested;

    public event Action? AnnotationsRestored;

    public void LoadOptions(ImageEditorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _isLoadingToolOptions = true;
        try
        {
            if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
            {
                LoadSelectedAnnotationOptions(SelectedAnnotation);
            }
            else
            {
                LoadOptionsForTool(ActiveTool);
            }

            UpdateToolOptionsVisibility();
        }
        finally
        {
            _isLoadingToolOptions = false;
        }
    }

    public void SaveOptions()
    {
    }

    [ObservableProperty]
    private EditorTool _activeTool = EditorTool.Select;

    partial void OnActiveToolChanged(EditorTool value)
    {
        _editorCore.ActiveTool = value;
        UpdateVisibleToolbarActiveStates();

        _isLoadingToolOptions = true;
        try
        {
            OnPropertyChanged(nameof(EffectStrengthMaximum));

            if (value == EditorTool.Select && SelectedAnnotation != null)
            {
                LoadSelectedAnnotationOptions(SelectedAnnotation);
            }
            else
            {
                LoadOptionsForTool(value);
            }

            UpdateToolOptionsVisibility();
        }
        finally
        {
            _isLoadingToolOptions = false;
        }
    }

    [RelayCommand]
    private void SelectTool(EditorTool tool)
    {
        if (!IsToolSupportedInRegionCapture(tool))
        {
            return;
        }

        ActiveTool = tool;
    }

    public Annotation? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            if (!SetProperty(ref _selectedAnnotation, value))
            {
                return;
            }

            _isLoadingToolOptions = true;
            try
            {
                OnPropertyChanged(nameof(EffectStrengthMaximum));
                if (ActiveTool == EditorTool.Select && value != null)
                {
                    LoadSelectedAnnotationOptions(value);
                }

                UpdateToolOptionsVisibility();
            }
            finally
            {
                _isLoadingToolOptions = false;
            }
        }
    }

    [ObservableProperty]
    private string _selectedColor = "#FFEF4444";

    public string StrokeColor
    {
        get => SelectedColor;
        set => SelectedColor = value;
    }

    public IBrush SelectedColorBrush
    {
        get => new SolidColorBrush(HexToColor(SelectedColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                SelectedColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnSelectedColorChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedColorBrush));
        ApplyStrokeColor(value);
    }

    [ObservableProperty]
    private string _fillColor = "#00000000";

    public IBrush FillColorBrush
    {
        get => new SolidColorBrush(HexToColor(FillColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                FillColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnFillColorChanged(string value)
    {
        OnPropertyChanged(nameof(FillColorBrush));
        ApplyFillColor(value);
    }

    [ObservableProperty]
    private string _textColor = "#FFFAFAFA";

    public IBrush TextColorBrush
    {
        get => new SolidColorBrush(HexToColor(TextColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                TextColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnTextColorChanged(string value)
    {
        OnPropertyChanged(nameof(TextColorBrush));
        ApplyTextColor(value);
    }

    [ObservableProperty]
    private int _strokeWidth = 4;

    partial void OnStrokeWidthChanged(int value)
    {
        ApplyStrokeWidth(value);
    }

    [ObservableProperty]
    private int _cornerRadius = 4;

    partial void OnCornerRadiusChanged(int value)
    {
        int clamped = Math.Max(0, value);
        if (clamped != value)
        {
            CornerRadius = clamped;
            return;
        }

        ApplyCornerRadius(clamped);
    }

    [ObservableProperty]
    private float _fontSize = 48;

    partial void OnFontSizeChanged(float value)
    {
        ApplyFontSize(value);
    }

    [ObservableProperty]
    private string _selectedFontFamily = "Segoe UI";

    partial void OnSelectedFontFamilyChanged(string value)
    {
        string normalizedFontFamily = NormalizeFontFamily(value);
        if (!string.Equals(normalizedFontFamily, value, StringComparison.Ordinal))
        {
            SelectedFontFamily = normalizedFontFamily;
            return;
        }

        ApplySelectedFontFamily(normalizedFontFamily);
    }

    [ObservableProperty]
    private ArrowStyle _selectedArrowStyle = ArrowStyle.Classic;

    partial void OnSelectedArrowStyleChanged(ArrowStyle value)
    {
        ArrowStyle normalizedArrowStyle = NormalizeArrowStyle(value);
        if (normalizedArrowStyle != value)
        {
            SelectedArrowStyle = normalizedArrowStyle;
            return;
        }

        ApplySelectedArrowStyle(normalizedArrowStyle);
    }

    [ObservableProperty]
    private CursorType _selectedCursorType = CursorType.Default;

    partial void OnSelectedCursorTypeChanged(CursorType value)
    {
        CursorType normalizedCursorType = NormalizeCursorType(value);
        if (normalizedCursorType != value)
        {
            SelectedCursorType = normalizedCursorType;
            return;
        }

        ApplySelectedCursorType(normalizedCursorType);
    }

    [ObservableProperty]
    private AnnotationBorderStyle _selectedBorderStyle = AnnotationBorderStyle.Solid;

    partial void OnSelectedBorderStyleChanged(AnnotationBorderStyle value)
    {
        ApplySelectedBorderStyle(value);
    }

    [ObservableProperty]
    private StepType _selectedStepType = StepType.Numeric;

    partial void OnSelectedStepTypeChanged(StepType value)
    {
        ApplySelectedStepType(value);
    }

    [ObservableProperty]
    private TextHorizontalAlignment _selectedTextHorizontalAlignment = TextHorizontalAlignment.Center;

    partial void OnSelectedTextHorizontalAlignmentChanged(TextHorizontalAlignment value)
    {
        ApplySelectedTextHorizontalAlignment(value);
    }

    public IReadOnlyList<string> AvailableFontFamilies => _availableFontFamilies;

    public IReadOnlyList<ArrowStyle> AvailableArrowStyles => _availableArrowStyles;

    public IReadOnlyList<CursorType> AvailableCursorTypes => _availableCursorTypes;

    public IReadOnlyList<int> AvailableStepStartNumbers => _availableStepStartNumbers;

    public IReadOnlyList<AnnotationBorderStyle> AvailableBorderStyles => _availableBorderStyles;

    public IReadOnlyList<StepType> AvailableStepTypes => _availableStepTypes;

    public IReadOnlyList<TextHorizontalAlignment> AvailableTextHorizontalAlignments => _availableTextHorizontalAlignments;

    [ObservableProperty]
    private int _stepStartNumber = 1;

    partial void OnStepStartNumberChanged(int value)
    {
        int clamped = Math.Clamp(value, 1, 10);
        if (clamped != value)
        {
            StepStartNumber = clamped;
            return;
        }

        _editorCore.NumberCounter = clamped;
    }

    [ObservableProperty]
    private float _effectStrength = 15;

    partial void OnEffectStrengthChanged(float value)
    {
        float clamped = Math.Clamp(value, MinEffectStrength, EffectStrengthMaximum);
        if (Math.Abs(clamped - value) > float.Epsilon)
        {
            EffectStrength = clamped;
            return;
        }

        ApplyEffectStrength(clamped);
    }

    [ObservableProperty]
    private float _spotlightBlur;

    public float SpotlightBlurMaximum => MaxBlurStrength;

    partial void OnSpotlightBlurChanged(float value)
    {
        float clamped = Math.Clamp(value, 0, SpotlightBlurMaximum);
        if (Math.Abs(clamped - value) > float.Epsilon)
        {
            SpotlightBlur = clamped;
            return;
        }

        ApplySpotlightBlur(clamped);
    }

    [ObservableProperty]
    private bool _shadowEnabled;

    partial void OnShadowEnabledChanged(bool value)
    {
        ApplyShadowEnabled(value);
    }

    [ObservableProperty]
    private string _shadowColor = Annotation.DefaultShadowColorHex;

    public IBrush ShadowColorBrush
    {
        get => new SolidColorBrush(HexToColor(ShadowColor));
        set
        {
            if (value is SolidColorBrush solidBrush)
            {
                ShadowColor = ColorToHex(solidBrush.Color);
            }
        }
    }

    partial void OnShadowColorChanged(string value)
    {
        OnPropertyChanged(nameof(ShadowColorBrush));
        ApplyShadowDetail(annotation => annotation.ShadowColor = value, options => options.ShadowColorHex = value);
    }

    [ObservableProperty]
    private double _shadowBlurRadius = Annotation.DefaultShadowBlurRadius;

    partial void OnShadowBlurRadiusChanged(double value)
    {
        ApplyShadowDetail(annotation => annotation.ShadowBlurRadius = value, options => options.ShadowBlurRadius = value);
    }

    [ObservableProperty]
    private double _shadowOpacity = Annotation.DefaultShadowOpacity;

    partial void OnShadowOpacityChanged(double value)
    {
        ApplyShadowDetail(annotation => annotation.ShadowOpacity = value, options => options.ShadowOpacity = value);
    }

    [ObservableProperty]
    private double _shadowOffsetX = Annotation.DefaultShadowOffsetX;

    partial void OnShadowOffsetXChanged(double value)
    {
        ApplyShadowDetail(annotation => annotation.ShadowOffsetX = value, options => options.ShadowOffsetX = value);
    }

    [ObservableProperty]
    private double _shadowOffsetY = Annotation.DefaultShadowOffsetY;

    partial void OnShadowOffsetYChanged(double value)
    {
        ApplyShadowDetail(annotation => annotation.ShadowOffsetY = value, options => options.ShadowOffsetY = value);
    }

    [ObservableProperty]
    private bool _speechBalloonTail = true;

    partial void OnSpeechBalloonTailChanged(bool value)
    {
        ApplySpeechBalloonTail(value);
    }

    [ObservableProperty]
    private bool _effectEllipse;

    partial void OnEffectEllipseChanged(bool value)
    {
        ApplyEffectEllipse(value);
    }

    [ObservableProperty]
    private bool _textBold = true;

    partial void OnTextBoldChanged(bool value)
    {
        ApplyTextStyle(value, TextStyle.Bold);
    }

    [ObservableProperty]
    private bool _textItalic;

    partial void OnTextItalicChanged(bool value)
    {
        ApplyTextStyle(value, TextStyle.Italic);
    }

    [ObservableProperty]
    private ShareXStepTailStyle _tailStyle = ShareXStepTailStyle.Triangle;

    [RelayCommand]
    private void ToggleShadow()
    {
        ShadowEnabled = !ShadowEnabled;
    }

    [RelayCommand]
    private void ToggleTextBold()
    {
        TextBold = !TextBold;
    }

    [RelayCommand]
    private void ToggleTextItalic()
    {
        TextItalic = !TextItalic;
    }

    public float EffectStrengthMaximum => GetMaxEffectStrength(GetEffectiveToolForOptions());

    public bool ShowBorderColor => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
            or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Text or EditorTool.Step => true,
        _ => false
    };

    public bool ShowFillColor => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.Highlight => true,
        _ => false
    };

    public bool ShowTextColor => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowThickness => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
            or EditorTool.Freehand or EditorTool.SpeechBalloon or EditorTool.Step or EditorTool.Text => true,
        _ => false
    };

    public bool ShowFontSize => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.Step or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowFontFamily => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowArrowStyle => GetToolOptionsContext() switch
    {
        EditorTool.Arrow => true,
        _ => false
    };

    public bool ShowCursorType => GetToolOptionsContext() switch
    {
        EditorTool.Cursor => true,
        EditorTool.Select => SelectedAnnotation is CursorAnnotation,
        _ => false
    };

    public bool ShowStepStartNumber => ActiveTool == EditorTool.Step;

    public bool ShowStepType => GetToolOptionsContext() == EditorTool.Step;

    public bool ShowTextHorizontalAlignment => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowBorderStyle => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Freehand => true,
        _ => false
    };

    public bool ShowSpotlightBlur => GetToolOptionsContext() == EditorTool.Spotlight;

    public bool ShowEffectEllipse => GetToolOptionsContext() switch
    {
        EditorTool.Magnify or EditorTool.Spotlight => true,
        _ => false
    };

    public bool ShowSpeechBalloonTail => GetToolOptionsContext() switch
    {
        EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowTextItalic => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowCornerRadius => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.SpeechBalloon => true,
        _ => false
    };

    public bool ShowStrength => GetToolOptionsContext() switch
    {
        EditorTool.Blur or EditorTool.Pixelate or EditorTool.Magnify or EditorTool.Spotlight => true,
        _ => false
    };

    public bool ShowShadow => GetToolOptionsContext() switch
    {
        EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Arrow
            or EditorTool.Freehand or EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowTextStyle => GetToolOptionsContext() switch
    {
        EditorTool.Text or EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowTailStyle => GetToolOptionsContext() switch
    {
        EditorTool.SpeechBalloon or EditorTool.Step => true,
        _ => false
    };

    public bool ShowToolOptionsSeparator =>
        ShowBorderColor ||
        ShowFillColor ||
        ShowTextColor ||
        ShowThickness ||
        ShowFontSize ||
        ShowStepStartNumber ||
        ShowStepType ||
        ShowTextHorizontalAlignment ||
        ShowFontFamily ||
        ShowBorderStyle ||
        ShowArrowStyle ||
        ShowCursorType ||
        ShowCornerRadius ||
        ShowStrength ||
        ShowSpotlightBlur ||
        ShowEffectEllipse ||
        ShowTextStyle ||
        ShowShadow ||
        ShowSpeechBalloonTail ||
        ShowTailStyle;

    public bool ShowToolOptions => ShowToolOptionsSeparator;

    public bool ShowOptionsButton => false;

    public bool IsEffectsButtonActive => false;

    public string ActiveToolIcon => EditorIcons.ForTool(GetEffectiveDisplayTool());

    public string ActiveToolName => GetEffectiveDisplayTool() switch
    {
        EditorTool.Select => "Select",
        EditorTool.Rectangle => "Rectangle",
        EditorTool.Ellipse => "Ellipse",
        EditorTool.Line => "Line",
        EditorTool.Arrow => "Arrow",
        EditorTool.Freehand => "Freehand",
        EditorTool.Text => "Text",
        EditorTool.SpeechBalloon => "Speech Balloon",
        EditorTool.Step => "Step",
        EditorTool.Cursor => "Cursor",
        EditorTool.Blur => "Blur",
        EditorTool.Pixelate => "Pixelate",
        EditorTool.Magnify => "Magnify",
        EditorTool.Spotlight => "Spotlight",
        EditorTool.SmartEraser => "Smart Eraser",
        EditorTool.Highlight => "Highlight",
        EditorTool.Crop => "Crop",
        EditorTool.CutOut => "Cut Out",
        _ => "Select"
    };

    public bool CanUndo
    {
        get => _canUndo;
        private set => SetProperty(ref _canUndo, value);
    }

    public bool CanRedo
    {
        get => _canRedo;
        private set => SetProperty(ref _canRedo, value);
    }

    public bool HasSelection => HasSelectedAnnotation;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_editorCore.CanUndo)
        {
            _editorCore.Undo();
            SelectedAnnotation = _editorCore.SelectedAnnotation;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_editorCore.CanRedo)
        {
            _editorCore.Redo();
            SelectedAnnotation = _editorCore.SelectedAnnotation;
        }
    }

    public bool HasSelectedAnnotation
    {
        get => _hasSelectedAnnotation;
        set
        {
            if (SetProperty(ref _hasSelectedAnnotation, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                DeleteSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

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
        SelectedAnnotation = _editorCore.SelectedAnnotation;
        HasSelectedAnnotation = SelectedAnnotation != null;
        HasAnnotations = _editorCore.Annotations.Count > 0;
        RequestCanvasRefresh();
    }

    [RelayCommand(CanExecute = nameof(HasAnnotations))]
    private void ClearAnnotations()
    {
        _editorCore.ClearAll();
        _editorCore.NumberCounter = StepStartNumber;
        SelectedAnnotation = null;
        HasAnnotations = false;
        HasSelectedAnnotation = false;
        RequestCanvasRefresh();
    }

    private void OnHistoryChanged()
    {
        CanUndo = _editorCore.CanUndo;
        CanRedo = _editorCore.CanRedo;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnAnnotationsRestored()
    {
        SelectedAnnotation = _editorCore.SelectedAnnotation;
        HasAnnotations = _editorCore.Annotations.Count > 0;
        HasSelectedAnnotation = SelectedAnnotation != null;
        AnnotationsRestored?.Invoke();
    }

    private void OnInvalidateRequested()
    {
        InvalidateRequested?.Invoke();
    }

    public void LoadBackgroundImage(SKBitmap bitmap)
    {
        _editorCore.LoadImage(bitmap);
    }

    public string GetResolvedTextColor()
    {
        if (IsTransparent(TextColor))
        {
            TextColor = ColorToHex(_options.TextTextColor);
        }

        return TextColor;
    }

    public byte GetSpotlightDarkenOpacity()
    {
        return ConvertSpotlightStrengthToOpacity(EffectStrength);
    }

    private void ApplyStrokeColor(string colorHex)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is not BaseEffectAnnotation &&
                SelectedAnnotation is not SmartEraserAnnotation &&
                SelectedAnnotation is not ImageAnnotation)
            {
                SelectedAnnotation.StrokeColor = colorHex;
                RequestCanvasRefresh();
            }

            return;
        }

        Color color = HexToColor(colorHex);
        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepBorderColor = color;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonBorderColor = color;
                break;
            case EditorTool.Text:
                _options.TextBorderColor = color;
                break;
            default:
                _options.BorderColor = color;
                break;
        }

        _editorCore.StrokeColor = colorHex;
    }

    private void ApplyFillColor(string colorHex)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case NumberAnnotation number:
                    number.FillColor = colorHex;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.FillColor = colorHex;
                    break;
                case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                    rectangle.FillColor = colorHex;
                    break;
                case EllipseAnnotation ellipse:
                    ellipse.FillColor = colorHex;
                    break;
                case HighlightAnnotation highlight:
                    highlight.FillColor = colorHex;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        Color color = HexToColor(colorHex);
        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepFillColor = color;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonFillColor = color;
                break;
            case EditorTool.Highlight:
                _options.HighlightFillColor = color;
                break;
            default:
                _options.FillColor = color;
                break;
        }
    }

    private void ApplyTextColor(string colorHex)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case NumberAnnotation number:
                    number.TextColor = colorHex;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.TextColor = colorHex;
                    break;
                case TextAnnotation text:
                    text.TextColor = colorHex;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        Color color = HexToColor(colorHex);
        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepTextColor = color;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonTextColor = color;
                break;
            case EditorTool.Text:
                _options.TextTextColor = color;
                break;
        }
    }

    private void ApplyStrokeWidth(int value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is SmartEraserAnnotation ||
                SelectedAnnotation is BaseEffectAnnotation ||
                SelectedAnnotation is SpotlightAnnotation)
            {
                return;
            }

            SelectedAnnotation.StrokeWidth = value;
            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepThickness = value;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonThickness = value;
                break;
            case EditorTool.Text:
                _options.TextThickness = value;
                break;
            default:
                _options.Thickness = value;
                break;
        }

        _editorCore.StrokeWidth = value;
    }

    private void ApplyCornerRadius(int value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                    rectangle.CornerRadius = value;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.CornerRadius = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool is EditorTool.Rectangle or EditorTool.SpeechBalloon)
        {
            _options.CornerRadius = value;
        }
    }

    private void ApplyFontSize(float value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case NumberAnnotation number:
                    number.FontSize = value;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.FontSize = value;
                    break;
                case TextAnnotation text:
                    text.FontSize = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.Step:
                _options.StepFontSize = value;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonFontSize = value;
                break;
            case EditorTool.Text:
                _options.TextFontSize = value;
                break;
        }
    }

    private void ApplySelectedFontFamily(string fontFamily)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case SpeechBalloonAnnotation balloon:
                    balloon.FontFamily = fontFamily;
                    break;
                case TextAnnotation text:
                    text.FontFamily = fontFamily;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonFontFamily = fontFamily;
                break;
            case EditorTool.Text:
                _options.TextFontFamily = fontFamily;
                break;
        }
    }

    private void ApplySelectedArrowStyle(ArrowStyle arrowStyle)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation is ArrowAnnotation arrow)
        {
            arrow.Style = arrowStyle;
            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool == EditorTool.Arrow)
        {
            _options.ArrowStyle = arrowStyle;
        }
    }

    private void ApplySelectedCursorType(CursorType cursorType)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation is CursorAnnotation cursorAnnotation)
        {
            cursorAnnotation.CursorType = cursorType;
            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool == EditorTool.Cursor)
        {
            _options.CursorType = cursorType;
        }
    }

    private void ApplyEffectStrength(float value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case SpotlightAnnotation spotlight:
                    spotlight.DarkenOpacity = ConvertSpotlightStrengthToOpacity(value);
                    RequestCanvasRefresh();
                    return;
                case BaseEffectAnnotation effect:
                    effect.Amount = value;
                    if (_editorCore.SourceImage != null)
                    {
                        effect.UpdateEffect(_editorCore.SourceImage);
                    }

                    RequestCanvasRefresh();
                    return;
                default:
                    return;
            }
        }

        switch (ActiveTool)
        {
            case EditorTool.Blur:
                _options.BlurStrength = value;
                break;
            case EditorTool.Pixelate:
                _options.PixelateStrength = value;
                break;
            case EditorTool.Magnify:
                _options.MagnifierStrength = value;
                break;
            case EditorTool.Spotlight:
                _options.SpotlightStrength = value;
                break;
        }
    }

    private void ApplyShadowEnabled(bool value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is not BaseEffectAnnotation &&
                SelectedAnnotation is not SmartEraserAnnotation &&
                SelectedAnnotation is not SpotlightAnnotation)
            {
                SelectedAnnotation.ShadowEnabled = value;
                RequestCanvasRefresh();
            }

            return;
        }

        _options.Shadow = value;
    }

    private void ApplyTextStyle(bool value, TextStyle style)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case TextAnnotation selectedText:
                    if (style == TextStyle.Bold)
                    {
                        selectedText.IsBold = value;
                    }
                    else
                    {
                        selectedText.IsItalic = value;
                    }
                    break;
                case SpeechBalloonAnnotation balloon:
                    if (style == TextStyle.Bold)
                    {
                        balloon.IsBold = value;
                    }
                    else
                    {
                        balloon.IsItalic = value;
                    }
                    break;
                case NumberAnnotation number when style == TextStyle.Bold:
                    number.IsBold = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (style)
        {
            case TextStyle.Bold:
                switch (ActiveTool)
                {
                    case EditorTool.SpeechBalloon:
                        _options.SpeechBalloonTextBold = value;
                        break;
                    case EditorTool.Step:
                        _options.StepTextBold = value;
                        break;
                    default:
                        _options.TextBold = value;
                        break;
                }
                break;
            case TextStyle.Italic:
                if (ActiveTool == EditorTool.SpeechBalloon)
                {
                    _options.SpeechBalloonTextItalic = value;
                }
                else
                {
                    _options.TextItalic = value;
                }
                break;
        }
    }

    private void ApplySelectedBorderStyle(AnnotationBorderStyle value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                    rectangle.BorderStyle = value;
                    break;
                case EllipseAnnotation ellipse:
                    ellipse.BorderStyle = value;
                    break;
                case LineAnnotation line:
                    line.BorderStyle = value;
                    break;
                case FreehandAnnotation freehand:
                    freehand.BorderStyle = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool is EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Line or EditorTool.Freehand)
        {
            _options.BorderStyle = value;
        }
    }

    private void ApplySelectedStepType(StepType value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation is NumberAnnotation number)
        {
            number.StepType = value;
            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool == EditorTool.Step)
        {
            _options.StepType = value;
        }
    }

    private void ApplySelectedTextHorizontalAlignment(TextHorizontalAlignment value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case TextAnnotation text:
                    text.HorizontalAlignment = value;
                    break;
                case SpeechBalloonAnnotation balloon:
                    balloon.HorizontalAlignment = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.Text:
                _options.TextHorizontalAlignment = value;
                break;
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonTextHorizontalAlignment = value;
                break;
        }
    }

    private void ApplySpotlightBlur(float value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation is SpotlightAnnotation spotlight)
        {
            spotlight.BlurAmount = value;
            RequestCanvasRefresh();
            return;
        }

        if (ActiveTool == EditorTool.Spotlight)
        {
            _options.SpotlightBlur = value;
        }
    }

    private void ApplyShadowDetail(Action<Annotation> applyToAnnotation, Action<ImageEditorOptions> applyToOptions)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            if (SelectedAnnotation is not BaseEffectAnnotation &&
                SelectedAnnotation is not SmartEraserAnnotation &&
                SelectedAnnotation is not SpotlightAnnotation)
            {
                applyToAnnotation(SelectedAnnotation);
                RequestCanvasRefresh();
            }

            return;
        }

        applyToOptions(_options);
    }

    private void ApplySpeechBalloonTail(bool value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select && SelectedAnnotation != null)
        {
            switch (SelectedAnnotation)
            {
                case SpeechBalloonAnnotation balloon:
                    balloon.TailEnabled = value;
                    break;
                case NumberAnnotation number:
                    number.TailEnabled = value;
                    break;
                default:
                    return;
            }

            RequestCanvasRefresh();
            return;
        }

        switch (ActiveTool)
        {
            case EditorTool.SpeechBalloon:
                _options.SpeechBalloonTail = value;
                break;
            case EditorTool.Step:
                _options.StepTail = value;
                break;
        }
    }

    private void ApplyEffectEllipse(bool value)
    {
        if (_isLoadingToolOptions)
        {
            return;
        }

        if (ActiveTool == EditorTool.Select)
        {
            switch (SelectedAnnotation)
            {
                case MagnifyAnnotation magnify:
                    magnify.IsEllipse = value;
                    RequestCanvasRefresh();
                    return;
                case SpotlightAnnotation spotlight:
                    spotlight.IsEllipse = value;
                    RequestCanvasRefresh();
                    return;
            }
        }

        switch (ActiveTool)
        {
            case EditorTool.Magnify:
                _options.MagnifierEllipse = value;
                break;
            case EditorTool.Spotlight:
                _options.SpotlightEllipse = value;
                break;
        }
    }

    private void LoadOptionsForTool(EditorTool tool)
    {
        switch (tool)
        {
            case EditorTool.Rectangle:
            case EditorTool.Ellipse:
            case EditorTool.Line:
            case EditorTool.Arrow:
            case EditorTool.Freehand:
                SelectedColor = ColorToHex(_options.BorderColor);
                FillColor = ColorToHex(_options.FillColor);
                StrokeWidth = _options.Thickness;
                CornerRadius = _options.CornerRadius;
                ShadowEnabled = _options.Shadow;
                if (tool != EditorTool.Arrow)
                {
                    SelectedBorderStyle = _options.BorderStyle;
                }
                if (tool == EditorTool.Arrow)
                {
                    SelectedArrowStyle = NormalizeArrowStyle(_options.ArrowStyle);
                }
                break;
            case EditorTool.Cursor:
                SelectedCursorType = NormalizeCursorType(_options.CursorType);
                break;
            case EditorTool.Text:
                SelectedColor = ColorToHex(_options.TextBorderColor);
                TextColor = ColorToHex(_options.TextTextColor);
                StrokeWidth = _options.TextThickness;
                ShadowEnabled = _options.Shadow;
                FontSize = _options.TextFontSize;
                SelectedFontFamily = NormalizeFontFamily(_options.TextFontFamily);
                SelectedTextHorizontalAlignment = _options.TextHorizontalAlignment;
                TextBold = _options.TextBold;
                TextItalic = _options.TextItalic;
                break;
            case EditorTool.SpeechBalloon:
                SelectedColor = ColorToHex(_options.SpeechBalloonBorderColor);
                FillColor = ColorToHex(_options.SpeechBalloonFillColor);
                TextColor = ColorToHex(_options.SpeechBalloonTextColor);
                StrokeWidth = _options.SpeechBalloonThickness;
                CornerRadius = _options.CornerRadius;
                ShadowEnabled = _options.Shadow;
                FontSize = _options.SpeechBalloonFontSize;
                SelectedFontFamily = NormalizeFontFamily(_options.SpeechBalloonFontFamily);
                SelectedTextHorizontalAlignment = _options.SpeechBalloonTextHorizontalAlignment;
                TextBold = _options.SpeechBalloonTextBold;
                TextItalic = _options.SpeechBalloonTextItalic;
                SpeechBalloonTail = _options.SpeechBalloonTail;
                break;
            case EditorTool.Step:
                SelectedColor = ColorToHex(_options.StepBorderColor);
                FillColor = ColorToHex(_options.StepFillColor);
                TextColor = ColorToHex(_options.StepTextColor);
                StrokeWidth = _options.StepThickness;
                ShadowEnabled = _options.Shadow;
                FontSize = _options.StepFontSize;
                SelectedStepType = _options.StepType;
                TextBold = _options.StepTextBold;
                SpeechBalloonTail = _options.StepTail;
                break;
            case EditorTool.Highlight:
                FillColor = ColorToHex(_options.HighlightFillColor);
                break;
            case EditorTool.Blur:
                EffectStrength = _options.BlurStrength;
                break;
            case EditorTool.Pixelate:
                EffectStrength = _options.PixelateStrength;
                break;
            case EditorTool.Magnify:
                EffectStrength = _options.MagnifierStrength;
                EffectEllipse = _options.MagnifierEllipse;
                break;
            case EditorTool.Spotlight:
                EffectStrength = _options.SpotlightStrength;
                SpotlightBlur = _options.SpotlightBlur;
                EffectEllipse = _options.SpotlightEllipse;
                break;
        }

        ShadowColor = _options.ShadowColorHex;
        ShadowBlurRadius = _options.ShadowBlurRadius;
        ShadowOpacity = _options.ShadowOpacity;
        ShadowOffsetX = _options.ShadowOffsetX;
        ShadowOffsetY = _options.ShadowOffsetY;
    }

    private void LoadSelectedAnnotationOptions(Annotation annotation)
    {
        if (annotation is not ImageAnnotation &&
            annotation is not BaseEffectAnnotation &&
            annotation is not SmartEraserAnnotation)
        {
            SelectedColor = annotation.StrokeColor;
            StrokeWidth = (int)annotation.StrokeWidth;
            ShadowEnabled = annotation.ShadowEnabled;
            ShadowColor = annotation.ShadowColor;
            ShadowBlurRadius = annotation.ShadowBlurRadius;
            ShadowOpacity = annotation.ShadowOpacity;
            ShadowOffsetX = annotation.ShadowOffsetX;
            ShadowOffsetY = annotation.ShadowOffsetY;
        }

        switch (annotation)
        {
            case NumberAnnotation number:
                FontSize = number.FontSize;
                FillColor = number.FillColor;
                SelectedStepType = number.StepType;
                TextBold = number.IsBold;
                SpeechBalloonTail = number.TailEnabled;
                if (!string.IsNullOrWhiteSpace(number.TextColor))
                {
                    TextColor = number.TextColor;
                }
                break;
            case TextAnnotation text:
                FontSize = text.FontSize;
                SelectedFontFamily = NormalizeFontFamily(text.FontFamily);
                SelectedTextHorizontalAlignment = text.HorizontalAlignment;
                TextBold = text.IsBold;
                TextItalic = text.IsItalic;
                if (!string.IsNullOrWhiteSpace(text.TextColor))
                {
                    TextColor = text.TextColor;
                }
                break;
            case SpeechBalloonAnnotation balloon:
                FontSize = balloon.FontSize;
                SelectedFontFamily = NormalizeFontFamily(balloon.FontFamily);
                SelectedTextHorizontalAlignment = balloon.HorizontalAlignment;
                TextBold = balloon.IsBold;
                TextItalic = balloon.IsItalic;
                SpeechBalloonTail = balloon.TailEnabled;
                FillColor = balloon.FillColor;
                CornerRadius = balloon.CornerRadius;
                if (!string.IsNullOrWhiteSpace(balloon.TextColor))
                {
                    TextColor = balloon.TextColor;
                }
                break;
            case RectangleAnnotation rectangle when rectangle is not SmartEraserAnnotation:
                FillColor = rectangle.FillColor;
                CornerRadius = rectangle.CornerRadius;
                SelectedBorderStyle = rectangle.BorderStyle;
                break;
            case EllipseAnnotation ellipse:
                FillColor = ellipse.FillColor;
                SelectedBorderStyle = ellipse.BorderStyle;
                break;
            case LineAnnotation line:
                SelectedBorderStyle = line.BorderStyle;
                break;
            case FreehandAnnotation freehand:
                SelectedBorderStyle = freehand.BorderStyle;
                break;
            case ArrowAnnotation arrow:
                SelectedArrowStyle = NormalizeArrowStyle(arrow.Style);
                break;
            case CursorAnnotation cursorAnnotation:
                SelectedCursorType = NormalizeCursorType(cursorAnnotation.CursorType);
                break;
            case SpotlightAnnotation spotlight:
                EffectStrength = ConvertSpotlightOpacityToStrength(spotlight.DarkenOpacity);
                SpotlightBlur = spotlight.BlurAmount;
                EffectEllipse = spotlight.IsEllipse;
                break;
            case BaseEffectAnnotation effect:
                EffectStrength = effect.Amount;
                if (effect is MagnifyAnnotation)
                {
                    EffectEllipse = effect.IsEllipse;
                }
                if (effect is HighlightAnnotation highlight)
                {
                    FillColor = highlight.FillColor;
                }
                break;
        }
    }

    private void UpdateToolOptionsVisibility()
    {
        OnPropertyChanged(nameof(ShowBorderColor));
        OnPropertyChanged(nameof(ShowFillColor));
        OnPropertyChanged(nameof(ShowTextColor));
        OnPropertyChanged(nameof(ShowThickness));
        OnPropertyChanged(nameof(ShowFontSize));
        OnPropertyChanged(nameof(ShowStepStartNumber));
        OnPropertyChanged(nameof(ShowStepType));
        OnPropertyChanged(nameof(ShowTextHorizontalAlignment));
        OnPropertyChanged(nameof(ShowFontFamily));
        OnPropertyChanged(nameof(ShowBorderStyle));
        OnPropertyChanged(nameof(ShowArrowStyle));
        OnPropertyChanged(nameof(ShowCursorType));
        OnPropertyChanged(nameof(ShowCornerRadius));
        OnPropertyChanged(nameof(ShowStrength));
        OnPropertyChanged(nameof(ShowSpotlightBlur));
        OnPropertyChanged(nameof(ShowEffectEllipse));
        OnPropertyChanged(nameof(ShowShadow));
        OnPropertyChanged(nameof(ShowSpeechBalloonTail));
        OnPropertyChanged(nameof(ShowTextStyle));
        OnPropertyChanged(nameof(ShowTextItalic));
        OnPropertyChanged(nameof(ShowTailStyle));
        OnPropertyChanged(nameof(ShowToolOptionsSeparator));
        OnPropertyChanged(nameof(ActiveToolIcon));
        OnPropertyChanged(nameof(ActiveToolName));
        OnPropertyChanged(nameof(EffectStrengthMaximum));
        OnPropertyChanged(nameof(SpotlightBlurMaximum));
    }

    private EditorTool? GetToolOptionsContext()
    {
        return ActiveTool == EditorTool.Select ? SelectedAnnotation?.ToolType : ActiveTool;
    }

    private EditorTool GetEffectiveToolForOptions()
    {
        return GetToolOptionsContext() ?? ActiveTool;
    }

    private EditorTool GetEffectiveDisplayTool()
    {
        return GetToolOptionsContext() ?? EditorTool.Select;
    }

    private void RequestCanvasRefresh()
    {
        InvalidateRequested?.Invoke();
    }

    private static float GetMaxEffectStrength(EditorTool tool) => tool switch
    {
        EditorTool.Blur => MaxBlurStrength,
        EditorTool.Pixelate => MaxPixelateStrength,
        EditorTool.Magnify => MaxMagnifyStrength,
        EditorTool.Spotlight => MaxSpotlightStrength,
        _ => 30
    };

    private static byte ConvertSpotlightStrengthToOpacity(float strength)
    {
        return (byte)Math.Clamp(strength / MaxSpotlightStrength * 255, 0, 255);
    }

    private static float ConvertSpotlightOpacityToStrength(byte opacity)
    {
        return opacity / 255f * MaxSpotlightStrength;
    }

    private static bool IsTransparent(string colorHex)
    {
        return HexToColor(colorHex).A == 0;
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color HexToColor(string hex)
    {
        return Color.TryParse(hex, out Color parsedColor) ? parsedColor : Colors.Transparent;
    }

    private void InitializeVisibleToolbarItems()
    {
        // Region capture only needs drawing tools. File / Background / Effects are editor chrome.
        foreach (ToolbarCustomizationItemViewModel item in ToolbarCustomizationItemViewModel.CreateDefaultItems())
        {
            if (!item.Tool.HasValue || !IsToolSupportedInRegionCapture(item.Tool.Value))
            {
                continue;
            }

            item.IsActive = item.Tool.Value == ActiveTool;
            _visibleToolbarItems.Add(item);
        }
    }

    /// <summary>
    /// Emoji is not a canvas-drawn tool. In the editor it opens the emoji picker in the
    /// EditorView modal overlay and EditorView inserts the annotation. The region capture
    /// overlay has neither, so selecting it would leave a tool that does nothing.
    /// </summary>
    internal static bool IsToolSupportedInRegionCapture(EditorTool tool) => tool != EditorTool.Emoji;

    private void UpdateVisibleToolbarActiveStates()
    {
        foreach (ToolbarCustomizationItemViewModel item in _visibleToolbarItems)
        {
            item.IsActive = item.Tool.HasValue && item.Tool.Value == ActiveTool;
        }

        OnPropertyChanged(nameof(VisibleToolbarItems));
    }

    private static string NormalizeFontFamily(string? fontFamily)
    {
        return string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily;
    }

    private static ArrowStyle NormalizeArrowStyle(ArrowStyle arrowStyle)
    {
        return Enum.IsDefined(arrowStyle) ? arrowStyle : ArrowStyle.Classic;
    }

    private static CursorType NormalizeCursorType(CursorType cursorType)
    {
        return Enum.IsDefined(cursorType) ? cursorType : CursorType.Default;
    }

    private enum TextStyle
    {
        Bold,
        Italic
    }

    void IAnnotationToolbarAdapter.SelectTool(EditorTool tool) => SelectToolCommand.Execute(tool);

    void IAnnotationToolbarAdapter.Undo() => UndoCommand.Execute(null);

    void IAnnotationToolbarAdapter.Redo() => RedoCommand.Execute(null);

    void IAnnotationToolbarAdapter.DeleteSelection() => DeleteSelectedCommand.Execute(null);

    void IAnnotationToolbarAdapter.ClearSelection() => ClearAnnotationsCommand.Execute(null);
}
