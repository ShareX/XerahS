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

Use `SkiaSharp` for **editing, processing, and saving** images.

### How is image rendering handled? Is it hardware accelerated?

**Q: "Is rendering used for the image editor hardware accelerated? I had issues with image editors struggling to keep stable 60FPS on high res monitors."**

**A:** Yes, ShareX.Avalonia uses **Avalonia UI**, which is fully hardware-accelerated via Skia/Direct2D/Metal depending on your OS.

*   **No more GDI+ Bottlenecks:** We do not use legacy Windows GDI rendering.
*   **Optimized Pipeline:** As of January 2026, we have refactored the image pipeline to use direct memory copies (`memcpy`) instead of expensive stream-based encoding.
    *   *Before:* Interactions could trigger costly PNG encode/decode cycles.
    *   *After:* Interactions manipulate raw pixel memory directly.
*   **Result:** Large 4K+ images render and update instantly without stalling the UI thread, maintaining a smooth 60+ FPS even on high-resolution displays.

---

### macOS: Why don't global hotkeys work?

**Q: Hotkeys never fire on macOS. What should I check?**

**A:** macOS requires Accessibility permission for global key monitoring. Open `System Settings` → `Privacy & Security` → `Accessibility` and allow the ShareX Ava app (bundle or `dotnet run`). Restart the app after granting permission. Hotkeys are powered by SharpHook; without Accessibility, it cannot receive events.
