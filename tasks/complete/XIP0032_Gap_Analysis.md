# XerahS Mobile Feature Assessment (XIP0032)

**Generated:** 2026-02-17
**Scope:** Mobile Application Feature Requirements  
**Targets:**
1.  **MAUI Stack:** `XerahS.Mobile.Maui` (Native Android/iOS via MAUI)
2.  **Avalonia Stack:** `XerahS.Mobile.UI` + `XerahS.Mobile.Android/iOS` (Native Android/iOS via Avalonia)

---

## 📋 Executive Summary

This assessment identifies the feature requirements for a comprehensive, modern mobile application. The goal is to implement these capabilities across **two parallel mobile stacks** in the `XerahS` solution: a MAUI-based app and an Avalonia-based app. Both stacks are first-class implementations and must achieve feature parity with each other.

**Key Required High-Level Features (Both Stacks):**
1.  **Visual Editors:** Annotation Editor, Crop Tool.
2.  **File Management:** Cloud Storage Explorer, Upload History.
3.  **Background Processing:** Robust background upload workers.
4.  **Native Platform Integration:** Share Intent receiving, Modern Photo Picker.

---

## 🔍 Detailed Feature Assessment

### 1. Photo Browser / Image Picker

**Feature Area:** Image capture and gallery selection

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ⚠️ PARTIAL | Uses `FilePicker` (generic), needs `MediaPicker` or native platform integration. |
| **Avalonia** | ⚠️ PARTIAL | Likely uses `IStorageProvider`, needs native Photo Picker integration. |

**Missing Components:**
-   **MAUI:** Integration with modern media pickers: `PickVisualMedia` (Android) and `PHPickerViewController` (iOS).
-   **Avalonia:** Native platform service for media picking (not generic file selection).
-   **Permissions:** Photo library permission request workflow.
-   **Multi-select:** Batch selection support.

**Recommended Implementation:**
-   **Interfaces:**
    -   `XerahS.Platform.Abstractions/IPhotosService.cs`
-   **Platform Implementations:**
    -   Android: `XerahS.Mobile.Android/Services/AndroidPhotoService.cs`
    -   iOS: `XerahS.Mobile.iOS/Services/iOSPhotoService.cs`
-   **ViewModels:**
    -   MAUI: `XerahS.Mobile.Maui/ViewModels/PhotoBrowserViewModel.cs`
    -   Avalonia: `XerahS.Mobile.UI/ViewModels/PhotoBrowserViewModel.cs`

---

### 2. Annotation Editor

**Feature Area:** Canvas-based image markup with drawing tools

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ❌ MISSING | No `GraphicsView` based editor implementation. |
| **Avalonia** | ❌ MISSING | No `Canvas` based editor implementation. |

**Missing Components:**
-   **MAUI:** `Microsoft.Maui.Graphics` based drawing canvas and tool renderers.
-   **Avalonia:** Skia/Canvas based drawing control and tool renderers.
-   **Shared:** Annotation geometry logic (potentially extractable from ImageEditor modules).

**Recommended Implementation:**
-   **MAUI UI:** `XerahS.Mobile.Maui/Controls/EditorCanvas.xaml`
-   **Avalonia UI:** `XerahS.Mobile.UI/Controls/EditorCanvas.axaml`
-   **Logic:** Implement annotation rendering and tool logic in respective ViewModels/Controls.

---

### 3. Crop Tool

**Feature Area:** Image cropping with draggable overlay

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ❌ MISSING | No crop overlay or logic. |
| **Avalonia** | ❌ MISSING | No crop overlay or logic. |

**Recommended Implementation:**
-   **MAUI UI:** `XerahS.Mobile.Maui/Controls/CropOverlay.xaml`
-   **Avalonia UI:** `XerahS.Mobile.UI/Controls/CropOverlay.axaml`

---

### 4. Background Upload Worker

**Feature Area:** Background task processing for uploads

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ❌ MISSING | No `WorkManager` / `BGTaskScheduler` integration. |
| **Avalonia** | ❌ MISSING | No `WorkManager` / `BGTaskScheduler` integration. |

**Missing Components:**
-   **Shared Interface:** `IBackgroundJobService`
-   **Android (Both):** `androidx.work.WorkManager` wrapper.
-   **iOS (Both):** `BGTaskScheduler` wrapper.

**Recommended Implementation:**
-   **Interface:** `XerahS.Platform.Abstractions/IBackgroundJobService.cs`
-   **Android Impl:** `XerahS.Platform.Mobile/Android/AndroidBackgroundJobService.cs` (Could be shared if nicely abstracted, otherwise separate).
-   **iOS Impl:** `XerahS.Platform.Mobile/iOS/iOSBackgroundJobService.cs` (Could be shared).

---

### 5. Cloud Storage Explorer

**Feature Area:** S3 bucket browsing and file management

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ❌ MISSING | No UI for browsing buckets. |
| **Avalonia** | ❌ MISSING | No UI for browsing buckets. |

**Recommended Implementation:**
-   **MAUI:** `XerahS.Mobile.Maui/Views/S3ExplorerPage.xaml` + ViewModel.
-   **Avalonia:** `XerahS.Mobile.UI/Views/S3ExplorerView.axaml` + ViewModel.
-   **Shared Logic:** S3 service integration from XerahS.Core.

---

### 6. Upload History

**Feature Area:** Historical upload tracking and management

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ❌ MISSING | No History UI. |
| **Avalonia** | ❌ MISSING | No History UI. |

**Recommended Implementation:**
-   **MAUI:** `XerahS.Mobile.Maui/Views/HistoryPage.xaml` + ViewModel.
-   **Avalonia:** `XerahS.Mobile.UI/Views/HistoryView.axaml` + ViewModel.
-   **Shared Logic:** `RecentTaskManager` (existing in XerahS.Core).

---

### 7. Share Extension / Receive Intent

**Feature Area:** Inter-app content sharing integration

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ⚠️ PARTIAL | Needs `MauiProgram.cs` / `MainActivity` intent handling. |
| **Avalonia** | ⚠️ PARTIAL | Needs `MainActivity` intent handling. |

**Missing Components:**
-   **Android (Both):** `OnNewIntent` handling in `MainActivity.cs` to parse shared media URI.
-   **iOS (Both):** Share Extension target exists, needs to verify it communicates with main app (App Groups).

**Recommended Implementation:**
-   **Interface:** `XerahS.Platform.Abstractions/IShareReceiverService.cs`
-   **MAUI:** Implement in `XerahS.Mobile.Maui/Platforms/Android/MainActivity.cs`.
-   **Avalonia:** Implement in `XerahS.Mobile.Android/MainActivity.cs`.

---

### 8. Settings & Configuration

**Feature Area:** Application settings and preferences

| Stack | Status | Notes |
| :--- | :--- | :--- |
| **MAUI** | ⚠️ PARTIAL | Basic settings UI likely missing or minimal. |
| **Avalonia** | ⚠️ PARTIAL | `MobileSettingsView` exists, partially implemented. |

**Recommended Implementation:**
-   **MAUI:** Implement comprehensive settings UI in MAUI XAML.
-   **Avalonia:** Enhance existing `MobileSettingsView` with additional configuration options.

---

## 📊 Summary Statistics

| Feature | MAUI Stack | Avalonia Stack |
| :--- | :--- | :--- |
| **Photo Browser** | ⚠️ PARTIAL | ⚠️ PARTIAL |
| **Annotation Editor** | ❌ MISSING | ❌ MISSING |
| **Crop Tool** | ❌ MISSING | ❌ MISSING |
| **Background Upload** | ❌ MISSING | ❌ MISSING |
| **S3 Explorer** | ❌ MISSING | ❌ MISSING |
| **History** | ❌ MISSING | ❌ MISSING |
| **Share Intent** | ⚠️ PARTIAL | ⚠️ PARTIAL |
| **Settings** | ⚠️ PARTIAL | ⚠️ PARTIAL |

**Next Steps (Phase 2):**
Implement features systematically for both stacks, ensuring feature parity and native platform integration.
1.  **Photo Browser** (Foundation)
2.  **Share Intent Handling** (Platform Integration)
3.  **Background Upload Workers** (Reliability)
4.  **History & Settings** (User Management)
