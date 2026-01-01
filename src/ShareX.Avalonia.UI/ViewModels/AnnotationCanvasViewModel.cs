using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Ava.Annotations.Models;

namespace ShareX.Ava.UI.ViewModels;

public partial class AnnotationCanvasViewModel : ObservableObject
{
    public ObservableCollection<Annotation> Annotations { get; }

    private Annotation? _selectedAnnotation;
    public Annotation? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set => SetProperty(ref _selectedAnnotation, value);
    }

    private EditorTool _activeTool = EditorTool.Select;
    public EditorTool ActiveTool
    {
        get => _activeTool;
        set => SetProperty(ref _activeTool, value);
    }

    private string _strokeColor = "#EF4444";
    public string StrokeColor
    {
        get => _strokeColor;
        set => SetProperty(ref _strokeColor, value);
    }

    private double _strokeWidth = 4;
    public double StrokeWidth
    {
        get => _strokeWidth;
        set => SetProperty(ref _strokeWidth, value);
    }

    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    public AnnotationCanvasViewModel()
    {
        Annotations = new ObservableCollection<Annotation>();
        PushState();
    }

    public void AddAnnotation(Annotation annotation)
    {
        Annotations.Add(annotation);
        SelectedAnnotation = annotation;
        PushState();
        _redoStack.Clear();
    }

    public void RemoveSelected()
    {
        if (SelectedAnnotation == null) return;
        Annotations.Remove(SelectedAnnotation);
        SelectedAnnotation = null;
        PushState();
        _redoStack.Clear();
    }

    public void Clear()
    {
        Annotations.Clear();
        SelectedAnnotation = null;
        PushState();
        _redoStack.Clear();
    }

    public void CommitCurrentState()
    {
        PushState();
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count <= 1) return; // keep current state
        var current = _undoStack.Pop();
        _redoStack.Push(current);
        var prev = _undoStack.Peek();
        RestoreState(prev);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var next = _redoStack.Pop();
        _undoStack.Push(next);
        RestoreState(next);
    }

    private void PushState()
    {
        var json = JsonSerializer.Serialize(Annotations, new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true
        });
        _undoStack.Push(json);
    }

    private void RestoreState(string json)
    {
        var restored = JsonSerializer.Deserialize<ObservableCollection<Annotation>>(json, new JsonSerializerOptions
        {
            IncludeFields = true
        });
        if (restored == null) return;
        Annotations.Clear();
        foreach (var a in restored)
        {
            Annotations.Add(a);
        }
        SelectedAnnotation = null;
    }
}
