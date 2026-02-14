### How to pronounce XerahS?

**Q: How to pronounce XerahS?**

**A:** XerahS is a pronounced **zeh raks** with *xe* as in Xerox and *rax* as in the ending sound of relax.

---

### How is XerahS different from ShareX?

**Q: How is XerahS different from ShareX?**

**A:** XerahS is a cross-platform rewrite reimagined for modern workflows and AI-assisted development.

| Feature | XerahS | ShareX |
| :--- | :--- | :--- |
| **Operating System** | **Windows, Linux, macOS** | Windows Only |
| **Development** | **Agentic Coding** (AI-First) | Traditional Manual Coding |
| **Architecture** | **Plugin-based** (Destinations) | Monolithic |
| **UI Framework** | **Avalonia UI** (Modern XAML) | **Windows Forms** (Legacy) |
| **Core Framework** | **.NET 10** | .NET Framework 4.8 |
| **Rendering** | GPU Accelerated (SkiaSharp) | CPU Based (GDI+) |
| **Workflows** | Zero-Inheritance (Isolated) | Inheritance-based (Profiles) |
| **Philosophy** | Modern, Experimental, Community-Driven | Feature-Rich, Battle-Tested, Stable |

---

### How is image rendering handled? Is it hardware accelerated?

**Q: "Is rendering used for the image editor hardware accelerated? I had issues with image editors struggling to keep stable 60FPS on high res monitors."**

**A:** Yes, XerahS uses **Avalonia UI**, which is fully hardware-accelerated via Skia/Direct2D/Metal depending on your OS.

*   **No more GDI+ Bottlenecks:** We do not use legacy Windows GDI rendering.
*   **Optimized Pipeline:** As of January 2026, we have refactored the image pipeline to use direct memory copies (`memcpy`) instead of expensive stream-based encoding.
    *   *Before:* Interactions could trigger costly PNG encode/decode cycles.
    *   *After:* Interactions manipulate raw pixel memory directly.
*   **Result:** Large 4K+ images render and update instantly without stalling the UI thread, maintaining a smooth 60+ FPS even on high-resolution displays.

---

### Why do workflows have unique settings instead of inheriting from a Default?

**Q: Why are there no "Default Settings" for workflows like in ShareX?**

**A:** XerahS adopts a "Zero Inheritance" policy for workflows to reduce cognitive load and configuration errors.

*   **Simplicity:** Each workflow is a self-contained unit. You never need to look elsewhere to understand what a specific hotkey or task will do.
*   **No "Inheritance" Confusion:** In the legacy model, users often struggled to remember which setting was inherited from "Default," which was overridden by a "Profile," and which was specific to a "Task."
*   **Explicit Configuration:** All settings are unique to the workflow. Changing a setting in one workflow is guaranteed to never affect another workflow.

---

### macOS: Why don't global hotkeys work?

**Q: Hotkeys never fire on macOS. What should I check?**

**A:** macOS requires Accessibility permission for global key monitoring. Open `System Settings` → `Privacy & Security` → `Accessibility` and allow the ShareX Ava app (bundle or `dotnet run`). Restart the app after granting permission. Hotkeys are powered by SharpHook; without Accessibility, it cannot receive events.

---

### Why use SkiaSharp instead of `System.Drawing` or Avalonia's native `Bitmap`?

**Q: Why are we using SkiaSharp? The original ShareX didn't need it.**

**A:** ShareX (WinForms) and XerahS represent two different eras of .NET development. The decision to use **SkiaSharp** is driven by cross-platform compatibility, performance, and maintainability.

#### 1. The "Old Way" (Legacy ShareX)

Original ShareX relies on `System.Drawing` (GDI+) and extensive custom C# algorithms for image processing.
To achieve performance, it often bypasses GDI+ limitations by using **Unsafe Code**:

*   **Raw Pointer Arithmetic:** It uses `LockBits` and `unsafe` blocks to maniuplate pixels directly in memory.
*   **Manual Algorithms:** Effects like Blur or Edge Detection are implemented as complex $O(N^2)$ loops iterating over pointers.
*   **Maintenance Burden:** This requires maintaining thousands of lines of low-level C# math that is hard to debug and optimize.
*   **Platform Limit:** `System.Drawing` is Windows-only (GDI+).

#### 2. The "New Way" (XerahS)

XerahS is a cross-platform application (Windows, Linux, macOS) running on .NET 8+.
We use **SkiaSharp**, which is a .NET binding for Google's Skia graphics engine (the same engine used by Chrome, Android, and Flutter).

*   **Hardware Acceleration:** Skia automatically utilizes SIMD (AVX2/Neon) and GPU acceleration where possible.
*   **High-Level API:** Complex effects are one-liners.
    *   *Legacy:* 50 lines of unsafe pointer loops for a Box Blur.
    *   *SkiaSharp:* `paint.ImageFilter = SKImageFilter.CreateBlur(radius, radius);`
*   **Headless Processing:** SkiaSharp allows robust image manipulation (`SKCanvas`, `SKBitmap`) without requiring a UI thread or Dispatcher, making it ideal for background "After Capture" tasks.
*   **True Cross-Platform:** It renders identically on all supported operating systems.

Use `SkiaSharp` for **editing, processing, and saving** images.

---

### Why does a "Share this screen" dialog appear on Linux?

**Q: On Linux (Wayland), why do I see a "Share this screen" dialog every time I try to capture a region?**

**A:** This happens if you have disabled **"Use modern capture"** in settings but are running on a secure Wayland compositor (like GNOME 42+ or KDE Plasma 6).

*   **Modern Capture (Enabled):** Uses the system's native screenshot UI. It is secure and doesn't require extra permissions, but lacks ShareX's custom crosshair/magnifier.
*   **Legacy Capture (Disabled):** Uses ShareX's custom `OverlayWindow`. To draw this overlay on top of your screen, the app must first **capture the entire specific screen** to use as a background.
    *   **Security Block:** Most modern Wayland systems block apps from silently capturing the screen.
    *   **The Dialog:** The OS forces you to explicitly allow the capture via the portal dialog.
*   **Solution (Smart Fallback):** ShareX detects if silent capture is blocked. Instead of showing a broken overlay or forcing you to click through dialogs, it will **automatically fall back to the system's native region tool** (Portal). This ensures you can always take a screenshot without complex workarounds.

If you prefer the native tool, you can enable **"Use modern capture"** to skip the check entirely.
