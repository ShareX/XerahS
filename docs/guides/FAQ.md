# ShareX.Avalonia FAQ

## Architectural Decisions

### Why use SkiaSharp instead of `System.Drawing` or Avalonia's native `Bitmap`?

**Q: Why are we using SkiaSharp? The original ShareX didn't need it.**

**A:** ShareX (WinForms) and ShareX.Avalonia represent two different eras of .NET development. The decision to use **SkiaSharp** is driven by cross-platform compatibility, performance, and maintainability.

#### 1. The "Old Way" (Legacy ShareX)

Original ShareX relies on `System.Drawing` (GDI+) and extensive custom C# algorithms for image processing.
To achieve performance, it often bypasses GDI+ limitations by using **Unsafe Code**:

*   **Raw Pointer Arithmetic:** It uses `LockBits` and `unsafe` blocks to maniuplate pixels directly in memory.
*   **Manual Algorithms:** Effects like Blur or Edge Detection are implemented as complex $O(N^2)$ loops iterating over pointers.
*   **Maintenance Burden:** This requires maintaining thousands of lines of low-level C# math that is hard to debug and optimize.
*   **Platform Limit:** `System.Drawing` is Windows-only (GDI+).

#### 2. The "New Way" (ShareX.Avalonia)

ShareX.Avalonia is a cross-platform application (Windows, Linux, macOS) running on .NET 8+.
We use **SkiaSharp**, which is a .NET binding for Google's Skia graphics engine (the same engine used by Chrome, Android, and Flutter).

*   **Hardware Acceleration:** Skia automatically utilizes SIMD (AVX2/Neon) and GPU acceleration where possible.
*   **High-Level API:** Complex effects are one-liners.
    *   *Legacy:* 50 lines of unsafe pointer loops for a Box Blur.
    *   *SkiaSharp:* `paint.ImageFilter = SKImageFilter.CreateBlur(radius, radius);`
*   **Headless Processing:** SkiaSharp allows robust image manipulation (`SKCanvas`, `SKBitmap`) without requiring a UI thread or Dispatcher, making it ideal for background "After Capture" tasks.
*   **True Cross-Platform:** It renders identically on all supported operating systems.

Use `Avalonia.Media.Bitmap` for **displaying** images in the UI.
Use `SkiaSharp` for **editing, processing, and saving** images.
