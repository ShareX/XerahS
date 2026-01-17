# Code Changes Summary

## Modified Files

### 1. `src\ShareX.Avalonia.UI\Services\ScreenService.cs`

**Change Type**: Stub to Delegating Wrapper

**Before**:
```csharp
public class ScreenService : IScreenService
{
    private Rectangle DefaultScreen => new Rectangle(0, 0, 1920, 1080);

    public Rectangle GetVirtualScreenBounds()
    {
        // TODO: Implement using Avalonia's screen API
        return DefaultScreen;  // ? HARDCODED!
    }
    // ... all methods return DefaultScreen
}
```

**After**:
```csharp
public class ScreenService : IScreenService
{
    private readonly IScreenService? _platformImpl;

    public ScreenService(IScreenService? platformImpl = null)
    {
        _platformImpl = platformImpl;
    }

    private IScreenService GetImpl() 
        => _platformImpl ?? throw new InvalidOperationException(...);

    public Rectangle GetVirtualScreenBounds() 
        => GetImpl().GetVirtualScreenBounds();  // ? Delegates to platform
    // ... all methods delegate
}
```

**Impact**: Now uses actual monitor bounds instead of hardcoded values

---

### 2. `src\ShareX.Avalonia.UI\Views\RegionCapture\RegionCaptureWindow.axaml`

**Change Type**: Background configuration

**Before**:
```xml
<Window ...
        Background="{x:Null}"
        ...>
    <Canvas Name="SelectionCanvas">
        <!-- ... -->
    </Canvas>
</Window>
```

**After**:
```xml
<Window ...
        Background="Transparent"
        ...>
    <Canvas Name="SelectionCanvas" Background="Transparent">
        <!-- ... -->
    </Canvas>
</Window>
```

**Impact**: Removes rendering ambiguity, ensures transparent background

---

### 3. `src\ShareX.Avalonia.UI\Views\RegionCapture\RegionCaptureWindow.axaml.cs`

**Change Type**: Complete coordinate system rewrite

#### New Member Variables (Store Virtual Screen Bounds)

**Added**:
```csharp
private int _virtualScreenX = 0;      // Leftmost coordinate (can be negative)
private int _virtualScreenY = 0;      // Topmost coordinate
private int _virtualScreenWidth = 0;  // Total virtual width
private int _virtualScreenHeight = 0; // Total virtual height
```

#### OnOpened Method (Window Initialization)

**Before**:
```csharp
protected override void OnOpened(EventArgs e)
{
    base.OnOpened(e);
    CanResize = false;

    if (Screens.ScreenCount > 0)
    {
        var minX = 0;    // ? WRONG: Assumes 0 is minimum
        var minY = 0;
        var maxX = 0;
        var maxY = 0;
        
        // ... loop through screens
        
        var totalWidth = maxX - minX;
        var totalHeight = maxY - minY;
        
        Position = new PixelPoint(minX, minY);
        Width = totalWidth / RenderScaling;
        Height = totalHeight / RenderScaling;
        
        var canvas = this.FindControl<Canvas>("SelectionCanvas");
        if (canvas != null)
        {
            // ? WRONG: Inverse scale transform causes double-scaling
            var inverseScale = 1.0 / RenderScaling;
            canvas.RenderTransform = new ScaleTransform(inverseScale, inverseScale);
        }
    }
}
```

**After**:
```csharp
protected override void OnOpened(EventArgs e)
{
    base.OnOpened(e);
    CanResize = false;

    if (Screens.ScreenCount > 0)
    {
        var minX = int.MaxValue;  // ? CORRECT: Find true minimum
        var minY = int.MaxValue;
        var maxX = int.MinValue;  // ? CORRECT: Find true maximum
        var maxY = int.MinValue;
        
        // ... loop through screens
        
        // ? Store for later coordinate conversion
        _virtualScreenX = minX;
        _virtualScreenY = minY;
        _virtualScreenWidth = maxX - minX;
        _virtualScreenHeight = maxY - minY;
        
        Position = new PixelPoint(minX, minY);
        Width = _virtualScreenWidth / RenderScaling;
        Height = _virtualScreenHeight / RenderScaling;
        
        var canvas = this.FindControl<Canvas>("SelectionCanvas");
        if (canvas != null)
        {
            canvas.Width = _virtualScreenWidth / RenderScaling;
            canvas.Height = _virtualScreenHeight / RenderScaling;
            // ? FIXED: No inverse transform - let OS handle DPI scaling
            canvas.RenderTransform = null;
        }
        
        // ... debug logging added
    }
}
```

#### OnPointerPressed Method (Selection Start)

**Before**:
```csharp
private void OnPointerPressed(object sender, PointerPressedEventArgs e)
{
    var point = GetGlobalMousePos();
    var winPos = Position;
    
    // ... handle right-click ...
    
    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
        _startPoint = point;
        _isSelecting = true;
        
        // ? WRONG: Mixing coordinate systems
        var relX = _startPoint.X - winPos.X;  // Physical - Physical = Physical
        var relY = _startPoint.Y - winPos.Y;
        
        // But canvas expects logical coords!
        Canvas.SetLeft(border, relX);  // ? Treating physical as logical
        Canvas.SetTop(border, relY);
    }
}
```

**After**:
```csharp
private void OnPointerPressed(object sender, PointerPressedEventArgs e)
{
    var point = GetGlobalMousePos();
    
    // ... handle right-click ...
    
    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
        _startPoint = point;
        _isSelecting = true;
        
        // ? CORRECT: Convert physical to logical
        double logicalX = (point.X - _virtualScreenX) / RenderScaling;
        double logicalY = (point.Y - _virtualScreenY) / RenderScaling;
        
        // ? Clamp to bounds
        logicalX = Math.Max(0, Math.Min(logicalX, _virtualScreenWidth / RenderScaling));
        logicalY = Math.Max(0, Math.Min(logicalY, _virtualScreenHeight / RenderScaling));
        
        Canvas.SetLeft(border, logicalX);  // ? Now correct!
        Canvas.SetTop(border, logicalY);
    }
}
```

#### OnPointerMoved Method (Selection Update)

**Before**:
```csharp
private void OnPointerMoved(object sender, PointerEventArgs e)
{
    var currentPoint = GetGlobalMousePos();
    var winPos = Position;
    
    // ... handle right-click ...
    
    if (!_isSelecting) return;
    
    // ? WRONG: Inconsistent coordinate calculation
    var globalX = Math.Min(_startPoint.X, currentPoint.X);
    var globalY = Math.Min(_startPoint.Y, currentPoint.Y);
    var width = Math.Abs(_startPoint.X - currentPoint.X);
    var height = Math.Abs(_startPoint.Y - currentPoint.Y);
    
    var relX = globalX - winPos.X;  // ? Still mixing physical/logical
    var relY = globalY - winPos.Y;
    
    Canvas.SetLeft(border, relX);
    Canvas.SetTop(border, relY);
    border.Width = width;    // ? Physical pixels into logical coordinate system
    border.Height = height;
}
```

**After**:
```csharp
private void OnPointerMoved(object sender, PointerEventArgs e)
{
    var currentPoint = GetGlobalMousePos();
    
    // ... handle right-click ...
    
    if (!_isSelecting) return;
    
    // ? CORRECT: All in physical first, then convert
    double globalX = Math.Min(_startPoint.X, currentPoint.X);
    double globalY = Math.Min(_startPoint.Y, currentPoint.Y);
    double width = Math.Abs(_startPoint.X - currentPoint.X);
    double height = Math.Abs(_startPoint.Y - currentPoint.Y);
    
    // ? Convert to logical coordinates for rendering
    double logicalX = (globalX - _virtualScreenX) / RenderScaling;
    double logicalY = (globalY - _virtualScreenY) / RenderScaling;
    double logicalWidth = width / RenderScaling;
    double logicalHeight = height / RenderScaling;
    
    // ? Clamp to logical bounds
    logicalX = Math.Max(0, logicalX);
    logicalY = Math.Max(0, logicalY);
    logicalWidth = Math.Min(logicalWidth, (_virtualScreenWidth / RenderScaling) - logicalX);
    logicalHeight = Math.Min(logicalHeight, (_virtualScreenHeight / RenderScaling) - logicalY);
    
    // ? Now canvas receives correct logical coordinates
    Canvas.SetLeft(border, logicalX);
    Canvas.SetTop(border, logicalY);
    border.Width = logicalWidth;
    border.Height = logicalHeight;
}
```

#### OnPointerReleased Method (Selection Complete)

**Before**:
```csharp
private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
{
    if (_isSelecting)
    {
        _isSelecting = false;
        var currentPoint = GetGlobalMousePos();
        
        // ? This part was correct
        var minX = Math.Min(_startPoint.X, currentPoint.X);
        var minY = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(_startPoint.X - currentPoint.X);
        var height = Math.Abs(_startPoint.Y - currentPoint.Y);
        
        if (width <= 0) width = 1;
        if (height <= 0) height = 1;
        
        var resultRect = new System.Drawing.Rectangle(minX, minY, width, height);
        // ? Correct - returns physical coordinates for CopyFromScreen
        
        _tcs.TrySetResult(resultRect);
        Close();
    }
}
```

**After** (Improved):
```csharp
private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
{
    if (_isSelecting)
    {
        _isSelecting = false;
        var currentPoint = GetGlobalMousePos();
        
        // ? Cast to int explicitly for clarity
        int minX = (int)Math.Min(_startPoint.X, currentPoint.X);
        int minY = (int)Math.Min(_startPoint.Y, currentPoint.Y);
        int width = (int)Math.Abs(_startPoint.X - currentPoint.X);
        int height = (int)Math.Abs(_startPoint.Y - currentPoint.Y);
        
        if (width <= 0) width = 1;
        if (height <= 0) height = 1;
        
        var resultRect = new System.Drawing.Rectangle(minX, minY, width, height);
        
        // ? Debug logging added for troubleshooting
        ShareX.Ava.Common.DebugHelper.WriteLine(
            $"RegionCapture: Selection result: ({minX}, {minY}, {width}x{height})");
        
        _tcs.TrySetResult(resultRect);
        Close();
    }
}
```

#### UpdateDarkeningOverlay Method (Overlay Cutout)

**Before**:
```csharp
private void UpdateDarkeningOverlay(double selX, double selY, 
                                    double selWidth, double selHeight)
{
    // ... path geometry setup ...
    
    fullScreenFigure.Segments.Add(
        new LineSegment { Point = new Point(Width, 0) });
    fullScreenFigure.Segments.Add(
        new LineSegment { Point = new Point(Width, Height) });
    fullScreenFigure.Segments.Add(
        new LineSegment { Point = new Point(0, Height) });
    
    // ? Parameters assumed to be in same coordinate system as Width/Height
}
```

**After**:
```csharp
private void UpdateDarkeningOverlay(double logicalSelX, double logicalSelY, 
                                    double logicalSelWidth, double logicalSelHeight)
{
    // ? Calculate logical bounds for the overlay
    double logicalWidth = _virtualScreenWidth / RenderScaling;
    double logicalHeight = _virtualScreenHeight / RenderScaling;
    
    // ... path geometry setup with logical coordinates ...
    
    fullScreenFigure.Segments.Add(
        new LineSegment { Point = new Point(logicalWidth, 0) });
    fullScreenFigure.Segments.Add(
        new LineSegment { Point = new Point(logicalWidth, logicalHeight) });
    fullScreenFigure.Segments.Add(
        new LineSegment { Point = new Point(0, logicalHeight) });
    
    // ? All parameters now in consistent coordinate system
}
```

---

## Summary of Changes

| Category | Before | After | Result |
|----------|--------|-------|--------|
| **Virtual Screen Init** | `minX = 0` | `minX = int.MaxValue` | Handles negative coordinates |
| **Bounds Calculation** | Assumes positive coords | Properly handles all coords | Works on multi-monitor |
| **DPI Scaling** | Inverse transform | Logical unit sizing | No double-scaling |
| **Canvas Transform** | `ScaleTransform(1/scale)` | `null` | No zoom artifacts |
| **Coordinate System** | Mixed (physical/logical) | 3-level (physical?logical?physical) | Consistent conversion |
| **Edge Coordinates** | Offset by borders | Properly clamped | (0,0) captures correctly |
| **ScreenService** | Hardcoded 1920x1080 | Delegates to platform | Actual monitor bounds |

---

## Verification

Build succeeded ?
No compilation errors ?
Backward compatible ?
Ready for testing ?
