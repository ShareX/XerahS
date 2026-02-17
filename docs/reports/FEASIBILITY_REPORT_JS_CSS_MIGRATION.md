# Feasibility Report: Migrating to JS/CSS Frontend with C# Backend

## I. Executive Summary

**Verdict:** **Highly Feasible** via **MAUI Blazor Hybrid**.

The existing `XerahS` solution is well-architected for this migration. The separation of concerns between `Core`/`ViewModels` and `UI` suggests that ~80% of the codebase (the business logic) can be preserved as-is.

**The Plan:**
1.  **Preserve** the pure C# backend (`Core`, `Services`, `Uploaders`, `ViewModels`).
2.  **Replace** the Avalonia UI layer (`Mobile.UI`, `Mobile.Android`, `Mobile.iOS`) with a **Blazor Hybrid** layer (`Mobile.Web`, `Mobile.Blazor`).
3.  **Refactor** abstractions to remove the hard dependency on Avalonia, allowing the backend to serve both the legacy Desktop app (Avalonia) and the new Mobile app (Blazor) if desired, or just move entirely to Blazor.

---

## II. Detailed Project Analysis

The following table analyzes **every single `.csproj` file** in `src/` (31 total), listed alphabetically.

| Project File (Alphabetical) | Status | Type | Action | Migration Analysis & Comments |
| :--- | :--- | :--- | :--- | :--- |
| `XerahS.AmazonS3.Plugin.csproj`<br>`Plugins\ShareX.AmazonS3.Plugin` | 🟢 | Plugin | **KEEP** | Standard library w/ `XerahS.Uploaders` dependency. Safe to keep. |
| `XerahS.Auto.Plugin.csproj`<br>`Plugins\ShareX.Auto.Plugin` | 🟢 | Plugin | **KEEP** | Standard library. Safe to keep. |
| `XerahS.GitHubGist.Plugin.csproj`<br>`Plugins\ShareX.GitHubGist.Plugin` | 🟢 | Plugin | **KEEP** | Standard library. Safe to keep. |
| `XerahS.Imgur.Plugin.csproj`<br>`Plugins\ShareX.Imgur.Plugin` | 🟢 | Plugin | **KEEP** | Standard library. Safe to keep. |
| `XerahS.Paste2.Plugin.csproj`<br>`Plugins\ShareX.Paste2.Plugin` | 🟢 | Plugin | **KEEP** | Standard library. Safe to keep. |
| `XerahS.App.csproj`<br>`XerahS.App` | ⚪ | Desktop Head | **IGNORE** | This is the *Desktop* entry point (Avalonia). It sits alongside the new Mobile projects. Ignored for mobile migration. |
| `XerahS.Audits.Tool.csproj`<br>`XerahS.Audits.Tool` | 🟢 | Tool | **KEEP** | Dev tool. No impact on mobile app. |
| `XerahS.Bootstrap.csproj`<br>`XerahS.Bootstrap` | 🟢 | Logic | **KEEP** | Dependency Injection setup. Reuse this logic in `MauiProgram.cs` to wire up services. |
| `XerahS.CLI.csproj`<br>`XerahS.CLI` | 🟢 | CLI | **KEEP** | Command-line interface. Independent of UI. |
| `XerahS.Common.csproj`<br>`XerahS.Common` | 🟢 | Library | **KEEP** | Core helpers/extensions. Used everywhere. Indispensable. |
| `XerahS.Core.csproj`<br>`XerahS.Core` | 🟢 | Library | **KEEP** | **The Brain.** Contains 100% of business logic. Must be preserved and referenced by the new Blazor app. |
| `XerahS.History.csproj`<br>`XerahS.History` | 🟢 | Library | **KEEP** | Database logic for history. UI-agnostic. |
| `XerahS.Indexer.csproj`<br>`XerahS.Indexer` | 🟢 | Library | **KEEP** | File indexing logic. UI-agnostic. |
| `XerahS.Media.csproj`<br>`XerahS.Media` | 🟢 | Library | **KEEP** | Image/Video processing (FFmpeg/Skia). Critical for functionality. |
| `XerahS.Mobile.Android.csproj`<br>`XerahS.Mobile.Android` | 🔴 | Mobile Head | **REPLACE** | **Action:** Create `XerahS.Mobile.Blazor` instead. This project currently bootstraps Avalonia on Android. |
| `XerahS.Mobile.Maui.csproj`<br>`XerahS.Mobile.Maui` | 🔴 | Mobile Head | **REPLACE** | **Action:** Consolidate into `XerahS.Mobile.Blazor`. This was likely an alternative experiment. |
| `XerahS.Mobile.UI.csproj`<br>`XerahS.Mobile.UI` | 🔴 | Mobile UI | **REPLACE** | **Action:** Create `XerahS.Mobile.Web` (Razor Class Lib). This is where all the Avalonia Views live; they must be rewritten as `.razor` + CSS. |
| `XerahS.Mobile.iOS.csproj`<br>`XerahS.Mobile.iOS` | 🔴 | Mobile Head | **REPLACE** | **Action:** Create `XerahS.Mobile.Blazor` (configured for iOS). Bootstraps Avalonia on iOS. |
| `XerahS.Mobile.iOS.ShareExtension.csproj`<br>`XerahS.Mobile.iOS.ShareExtension` | 🟡 | Extension | **REFACTOR** | Native iOS extension. logic should remain, but ensure it shares data/settings with the new bundle ID of the Blazor app. |
| `XerahS.Platform.Abstractions.csproj`<br>`XerahS.Platform.Abstractions` | 🟡 | Library | **REFACTOR** | **Crucial Step:** Remove `<PackageReference Include="Avalonia" />`. Check `CrossPlatformTypes.cs` and refactor any Avalonia-specific types to use `System.Drawing` or `SkiaSharp` primitives. |
| `XerahS.Platform.Linux.csproj`<br>`XerahS.Platform.Linux` | ⚪ | Desktop Lib | **IGNORE** | Linux-specific implementation. Not relevant for Mobile. |
| `XerahS.Platform.MacOS.csproj`<br>`XerahS.Platform.MacOS` | ⚪ | Desktop Lib | **IGNORE** | macOS-specific (Desktop) implementation. Not relevant for Mobile. |
| `XerahS.Platform.Mobile.csproj`<br>`XerahS.Platform.Mobile` | 🟡 | Mobile Lib | **REFACTOR** | Contains native Android/iOS service implementations (Clipboard, Toast, etc.). Extract the logic to use in the new MAUI Blazor project (or implementation of interfaces for it). |
| `XerahS.Platform.Windows.csproj`<br>`XerahS.Platform.Windows` | ⚪ | Desktop Lib | **IGNORE** | Windows-specific implementation. Not relevant for Mobile. |
| `XerahS.PluginExporter.csproj`<br>`XerahS.PluginExporter` | 🟢 | Tool | **KEEP** | Build tool. Safe. |
| `XerahS.RegionCapture.csproj`<br>`XerahS.RegionCapture` | 🔴 | Desktop Tool | **REWRITE** | **Action:** See "Region Capture Strategy" below. This project is heavily desktop-bound (Avalonia.Desktop, PInvoke). You need a new "Web Overlay" or "MAUI GraphicsView" solution for mobile region selection. |
| `XerahS.Services.csproj`<br>`XerahS.Services` | 🟢 | Library | **KEEP** | Pure C# service implementations. Reuse 100%. |
| `XerahS.Services.Abstractions.csproj`<br>`XerahS.Services.Abstractions` | 🟢 | Library | **KEEP** | Service interfaces. Reuse 100%. |
| `XerahS.UI.csproj`<br>`XerahS.UI` | ⚪ | Desktop UI | **IGNORE** | The main Desktop UI library (Avalonia). Ignored for mobile migration. |
| `XerahS.Uploaders.csproj`<br>`XerahS.Uploaders` | 🟢 | Library | **KEEP** | **Core Value.** Contains all uploader logic. UI-independent. |
| `XerahS.ViewModels.csproj`<br>`XerahS.ViewModels` | 🟢 | Library | **KEEP** | **Gold Mine.** Contains the presentation logic. You can bind your new Blazor components directly to these existing ViewModels (ReactiveUI). |

---


---

## III. Frontend Capability Matrix by OS

The following table outlines how the proposed **Blazor Hybrid** architecture supports each platform.

| Platform | Capability | Technology Stack | Status / Notes |
| :--- | :--- | :--- | :--- |
| **Android** | ✅ **Full Support** | **MAUI Blazor** | **Primary Target.** Uses Android System WebView. Full access to native APIs via .NET. |
| **iOS** | ✅ **Full Support** | **MAUI Blazor** | **Primary Target.** Uses `WKWebView`. Full access to native APIs via .NET. |
| **Windows** | ✅ **Full Support** | **MAUI Blazor (WinUI 3)** | **Optional.** The new stack *can* fully replace the Avalonia Desktop app if desired, using WebView2 (Edge Chromium). |
| **macOS** | ✅ **Full Support** | **MAUI Blazor (Catalyst)** | **Optional.** The new stack *can* fully replace the Avalonia Desktop app if desired, using `WKWebView`. |
| **Linux** | ⚠️ **Partial** | **Photino** or **Avalonia Hybrid** | **Complex.** MAUI has no official Linux support. To run the new HTML/CSS UI on Linux, we would need to host the Blazor components inside a **Photino** shell or embed a `BlazorWebView` within the existing **Avalonia** app. |

## IV. New Architecture Diagram

```mermaid
graph TD
    subgraph "Frontend (Blazor Hybrid)"
        Web["XerahS.Mobile.Web (Razor Class Lib)"] --> HTML["HTML/CSS/JS (Theming)"]
        BlazorApp["XerahS.Mobile.Blazor (MAUI Host)"] --> Web
    end

    subgraph "Backend (C#)"
        Web -.-> ViewModels["XerahS.ViewModels (ReactiveUI)"]
        ViewModels --> Core["XerahS.Core"]
        Core --> Services["XerahS.Services"]
        Core --> Uploaders["XerahS.Uploaders (ShareX Libs)"]
    end

    subgraph "Platform"
        BlazorApp --> Android["Android Native APIs"]
        BlazorApp --> iOS["iOS Native APIs"]
        Core --> Abstractions["XerahS.Platform.Abstractions"]
    end
```

## V. Next Steps

1.  **Refactor**: Edit `XerahS.Platform.Abstractions.csproj` to remove the Avalonia dependency.
2.  **Initialize**: Create the new `XerahS.Mobile.Blazor` and `XerahS.Mobile.Web` projects.
3.  **Proof of Concept**: Port the `MainViewModel` to a simple Blazor page to verify the binding loop.
