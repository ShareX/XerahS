# AI Instruction Set: Multi-Platform Mobile Feature Implementation

---

## 📋 Phase Overview

This task is structured into **2 distinct phases** designed for parallel agent execution:

### Phase 1: Feature Gap Analysis (Analysis Only - No Implementation)
**Agent Role:** Research & Analysis  
**Deliverable:** Comprehensive Gap Report (Markdown document)

**Tasks:**
1. Review mobile feature requirements and specifications
2. Catalog all required mobile features and capabilities
3. Assess existing XerahS mobile projects:
   - `XerahS.Mobile.Maui`
   - `XerahS.Mobile.Android`
   - `XerahS.Mobile.iOS`
   - `XerahS.Mobile.UI`
   - `XerahS.Core`
   - `XerahS.Platform.Mobile`
4. Categorize each feature:
   - ✅ **COMPLETE** - Full parity on both Android & iOS
   - ⚠️ **PARTIAL** - Exists but incomplete, or missing on one platform
   - ❌ **MISSING** - Does not exist on either platform
5. For each feature, document:
   - Android status (exists/missing/partial)
   - iOS status (exists/missing/partial)
   - Missing components (specific items)
   - Recommended implementation layers (with file paths)
   - Platform-specific vs shared logic breakdown

**Output Format:** Structured Gap Report with separate entries for each feature  
**No Code Changes:** This phase is read-only analysis

---

### Phase 2: Implementation Protocol (Coding Phase)
**Agent Role:** Implementation Engineer  
**Input:** Gap Report from Phase 1  
**Deliverable:** Working code implementing missing/partial features

**Three-Step Process:**

**Step 1: Logic Verification**
- Check for existing business logic in XerahS.Core/Services
- Reuse existing services/managers (NO duplication)
- Create new interfaces/implementations only if needed
- Register services in DI container
- Create ViewModels and Views

**Step 2: UI Design and Implementation**
- Develop modern, native UI using Avalonia XAML
- Create exceptional UX with platform-native patterns
- Apply platform-native styling (Material 3 for Android, Cupertino for iOS)
- Support gestures, responsive layouts, safe areas
- Implement skeleton states, pull-to-refresh, etc.

**Step 3: Code Quality Enforcement**
- Enforce async/await (no `.Result` or `.Wait()`)
- Extract strings to `.resx` files (no hard-coded text)
- Remove magic numbers (use constants)
- Enable nullable reference types
- Use ILogger (no Console.WriteLine)
- Add license headers
- Build must pass with 0 errors, 0 warnings

**Platform-Specific Implementation:**
- Android-only code → `XerahS.Mobile.Android/`
- iOS-only code → `XerahS.Mobile.iOS/`
- Shared mobile code → `XerahS.Platform.Mobile/`
- Business logic → `XerahS.Core/`
- Shared UI → `XerahS.Mobile.UI/`

---

### Execution Flow for Multi-Agent Workflow

1. **Phase 1 Agent:** Generates Gap Report (analysis document)
2. **Phase 2 Agent(s):** Reads Gap Report, implements features one-by-one
3. **Per-Feature Implementation:**
   - Pick one feature from Gap Report
   - Verify existing logic (Step 1)
   - Implement UI improvements (Step 2)
   - Enforce code quality (Step 3)
   - Build and test
   - Move to next feature
4. **Platform Coverage:** Each feature requires BOTH Android AND iOS implementations

**Critical Rules:**
- Phase 1 must complete before Phase 2 begins
- Phase 2 agents work on individual features (can parallelize)
- Never duplicate existing services/ViewModels
- Build must pass after each feature implementation

---

## Role
You act as a Senior .NET Mobile Architect specialising in .NET MAUI, Avalonia XAML, MVVM, cross-platform architecture, and platform-specific native implementations for Android and iOS.

## Objective

Develop a comprehensive, feature-rich mobile application using **Clean Architecture** principles with modular structure:
- Application layer - Main entry point, navigation, theme, onboarding
- Core layer - Common utilities, domain models, data access, shared UI components  
- Feature modules - Capture, annotation, upload, history, cloud storage explorer, settings

Your task is to architect and implement these capabilities **across ALL mobile platforms** within the XerahS solution structure.

### Target Projects:

**MAUI Application:**
- **XerahS.Mobile.Maui** - MAUI head project (cross-platform entry point)

**Platform-Specific Implementations:**
- **XerahS.Mobile.Android** - Android-native implementations (Java/Kotlin interop, Android APIs)
- **XerahS.Mobile.iOS** - iOS-native implementations (Objective-C/Swift interop, iOS APIs)
- **XerahS.Mobile.iOS.ShareExtension** - iOS Share Extension for receiving shared content

**Shared UI & ViewModels:**
- **XerahS.Mobile.UI** - Shared Avalonia XAML views/ViewModels (cross-platform UI)

**Services & Abstractions:**
- **XerahS.Platform.Mobile** - Mobile-common platform service implementations
- **XerahS.Core** - Business logic, models, managers (platform-agnostic)
- **XerahS.Services.Abstractions** - Service interfaces (IDialogService, IFileDialogService, INotificationService, etc.)
- **XerahS.Services** - Service implementations (cross-platform)
- **XerahS.Platform.Abstractions** - Platform service interfaces (IScreenCaptureService, IClipboardService, ISystemService, etc.)

### Multi-Platform Implementation Requirement

**CRITICAL:** Features must be implemented **natively** for EACH platform:

1. **Android** (`XerahS.Mobile.Android`):
   - Use Android-specific APIs (Storage Access Framework, MediaStore, WorkManager, etc.)
   - Follow Material Design 3 guidelines
   - Use Android platform services (Notifications, Background Services, etc.)

2. **iOS** (`XerahS.Mobile.iOS`):
   - Use iOS-specific APIs (Photos Framework, UIDocumentPickerViewController, BackgroundTasks, etc.)
   - Follow iOS Human Interface Guidelines
   - Use iOS platform services (Local Notifications, Background Fetch, etc.)

3. **MAUI** (`XerahS.Mobile.Maui`):
   - Coordinate platform-specific implementations
   - Use MAUI cross-platform APIs where appropriate
   - Delegate to platform-specific code via DI when native behavior is required

**Goal:** Each platform should feel **100% native** to its ecosystem while sharing business logic through `XerahS.Core` and `XerahS.Services`.

All implementations must be Equal or Better in architecture, performance and user experience.

---

## Critical Constraints

### 1. No Duplication Rule

Before creating any new class, you **MUST** inspect:

**Core & Services:**
- `XerahS.Core` - Business logic, models, managers, helpers
- `XerahS.Services` - Service implementations
- `XerahS.Services.Abstractions` - Service interface contracts

**Mobile-Specific:**
- `XerahS.Mobile.UI/ViewModels` - Existing ViewModels
- `XerahS.Mobile.UI/Views` - Existing Views
- `XerahS.Platform.Mobile` - Mobile platform service implementations
- `XerahS.Platform.Abstractions` - Platform service interface contracts

**Existing ViewModels (DO NOT DUPLICATE):**
- `IMobileUploaderConfig`
- `MobileAmazonS3ConfigViewModel`
- `MobileCustomUploaderConfigViewModel`
- `MobileSettingsViewModel`
- `MobileUploadViewModel`

**Existing Service Interfaces in XerahS.Services.Abstractions (DO NOT DUPLICATE):**
- `IDialogService`
- `IFileDialogService`
- `IImageEncoderService`
- `IImageEncoder`
- `INotificationService`

**Existing Service Interfaces in XerahS.Platform.Abstractions (DO NOT DUPLICATE):**
- `IClipboardService`
- `IDiagnosticService`
- `IFontService`
- `IInputService`
- `IOcrService`
- `IPlatformInfo`
- `IScreenCaptureService`
- `IScreenSampler`
- `IScreenService`
- `IScrollingCaptureService`
- `IShellIntegrationService`
- `ISystemService`
- `IThemeService`
- `IToastService`
- `IUIService`
- `IWindowService`

**Existing Mobile Platform Implementations in XerahS.Platform.Mobile (DO NOT DUPLICATE):**
- `MobileClipboardService`
- `MobileDiagnosticService`
- `MobileFontService`
- `MobileHotkeyService`
- `MobileInputService`
- `MobileNotificationService`
- `MobileScreenCaptureService`
- `MobileScreenService`
- `MobileSystemService`
- `MobileWindowService`

**Android-Specific Implementations in XerahS.Mobile.Android (DO NOT DUPLICATE):**
- `AndroidClipboardService`
- `MainActivity`

**iOS-Specific Implementations (check before creating):**
- Check `XerahS.Mobile.iOS` for existing iOS platform implementations
- Check `XerahS.Mobile.iOS.ShareExtension` for share extension code

**Architecture Rule:**
All business logic belongs in `XerahS.Core` or `XerahS.Services`.
The UI layer (`XerahS.Mobile.UI`) must remain thin - ViewModels only orchestrate services.
Platform-specific code belongs in platform projects (`XerahS.Mobile.Android`, `XerahS.Mobile.iOS`).

---

## Required Architecture

All features must follow:

- **MVVM Pattern**
- **Constructor-based Dependency Injection**
- **ObservableObject** from `CommunityToolkit.Mvvm`
- **AsyncRelayCommand** / **RelayCommand** for commands
- **No code-behind business logic** (Views/Pages should have minimal or no code-behind)
- **No static service access** (No ServiceLocator pattern)
- **No tightly coupled page logic**

**Project Structure:**

ViewModels location:
- `XerahS.Mobile.UI/ViewModels/`

Views location (Avalonia XAML):
- `XerahS.Mobile.UI/Views/`

Service implementations:
- `XerahS.Services/` (for cross-platform services)
- `XerahS.Platform.Mobile/` (for mobile-common platform services)
- `XerahS.Mobile.Android/` (for Android-only implementations)
- `XerahS.Mobile.iOS/` (for iOS-only implementations)

Interface definitions:
- `XerahS.Services.Abstractions/` (IDialogService, IFileDialogService, INotificationService, etc.)
- `XerahS.Platform.Abstractions/` (IClipboardService, IScreenCaptureService, ISystemService, etc.)

Business Logic & Models:
- `XerahS.Core/Models/`
- `XerahS.Core/Managers/`
- `XerahS.Core/Helpers/`
- `XerahS.Core/Services/` (core business services)

---

## UI Framework Adaptation

### XerahS Mobile Stack

XerahS Mobile uses a **hybrid architecture**:
1. **Avalonia XAML** (`XerahS.Mobile.UI`) - Shared UI components and ViewModels
2. **MAUI** (`XerahS.Mobile.Maui`) - Application head for Android/iOS
3. **Platform-Specific Projects** - Native implementations for Android and iOS

### Modern Mobile UI Patterns

Contemporary mobile applications leverage:
- **Declarative UI** frameworks for modern, reactive interfaces
- **Material 3** design system for Android
- **Component-based navigation** patterns
- **Dependency injection** for testable, maintainable code
- **Async/await patterns** for responsive operations

### Implementation Guidelines

When implementing mobile UI with Avalonia XAML:

**Mobile Pattern → Avalonia XAML Mapping:**
- Composable components → `UserControl` or data-bound `ContentControl`
- Scrollable lists → `ListBox` or `ItemsRepeater` with virtualization
- Horizontal galleries → Horizontal `ItemsRepeater` or `ItemsControl`
- Text input fields → `TextBox` with styled border
- Action buttons → `Button` with Material-inspired style
- Card containers → `Border` with `CornerRadius` and `BoxShadow`
- App scaffold → Avalonia `Window` or `UserControl` with custom header
- Alert dialogs → Custom styled `Window` or use `IDialogService`
- Bottom sheets → Popup or custom overlay control
- Reactive state → ViewModel properties with `ObservableProperty`
- View dependencies → Constructor injection of ViewModel

**Navigation:**
- Modern mobile apps use route-based navigation patterns
- XerahS.Mobile.UI should use ViewModel-first navigation with services
- Implement `INavigationService` abstraction for platform-agnostic navigation

**Platform-Adaptive Styling:**

For MAUI (`XerahS.Mobile.Maui`):
- Use `ContentPage` as base
- Use `CollectionView` instead of ListView for performance
- Use Shell navigation where appropriate
- Apply platform-specific styles via `OnPlatform` markup:
  ```xml
  <Label>
      <Label.FontSize>
          <OnPlatform x:TypeArguments="x:Double">
              <On Platform="iOS" Value="17" />
              <On Platform="Android" Value="16" />
          </OnPlatform>
      </Label.FontSize>
  </Label>
  ```
- Use platform-specific handlers/renderers for native controls when needed

For Avalonia (`XerahS.Mobile.UI`):
- Use styles that adapt to mobile form factors
- Keep touch targets ≥ 44pt (iOS) / 48dp (Android)
- Use responsive layouts with `Grid` and adaptive `ColumnDefinitions`
- Apply platform-aware styling (Material 3 on Android, Cupertino on iOS)

**Design Principles:**

UI must follow **Native Look and Feel** for each platform:

**Android:**
- Material Design 3 components (Cards, FABs, Bottom Sheets, Navigation Drawer)
- Elevation and shadows
- Ripple effects on touch
- Bottom navigation bar (not tab bar)
- Hamburger menu / Navigation Drawer
- Floating Action Button for primary actions
- Snackbars for temporary messages

**iOS:**
- iOS Human Interface Guidelines
- Cupertino-style controls (UINavigationBar, UITabBar)
- SF Symbols for icons (or equivalent)
- Native iOS transitions (push/pop, modal present)
- Bottom tab bar (not top tabs)
- Pull-to-refresh with native spinner
- Action Sheets for contextual actions
- Swipe-back gesture support

**Common Mobile UX:**
- **Avoid** generic desktop-looking layouts with fixed widths
- Support portrait and landscape orientations
- Handle safe areas (notches, rounded corners, gesture bars)
- Scale font sizes appropriately (use system font scaling)
- Use dynamic spacing (not hard-coded pixel values)
- Support dark mode with system theme detection
- Use platform-specific icons and typography

---

## iOS-Specific Implementation Guidance

### iOS Platform APIs to Leverage

**Photo & Media Access:**
- **PHPickerViewController** - Modern Photo Picker (iOS 14+)
- **PHPhotoLibrary** - Photos framework for gallery access
- **UIImagePickerController** - Legacy picker (fallback)
- **AVFoundation** - Camera capture

**File Management:**
- **UIDocumentPickerViewController** - File picker
- **UIDocumentBrowserViewController** - Document browser
- **FileManager** - File system operations

**Background Tasks:**
- **BGTaskScheduler** - Background task scheduling (iOS 13+)
- **BGAppRefreshTask** - Background app refresh
- **BGProcessingTask** - Long-running background tasks
- **URLSession background configuration** - Background uploads

**Notifications:**
- **UNUserNotificationCenter** - Local & remote notifications
- **UNNotificationServiceExtension** - Notification service extension
- **UNNotificationContentExtension** - Custom notification UI

**Sharing:**
- **UIActivityViewController** - Share sheet
- **Share Extension** (`XerahS.Mobile.iOS.ShareExtension`) - Receive shared content
- **App Groups** - Share data between app and extension

**Security:**
- **Keychain Services** - Secure credential storage
- **LocalAuthentication** - Face ID / Touch ID
- **Data Protection API** - File encryption

**UI Components:**
- **UINavigationController** - Navigation stack
- **UITabBarController** - Bottom tab bar
- **UITableView** / **UICollectionView** - Lists and grids
- **UIContextMenuInteraction** - Context menus (3D Touch / Haptic Touch)

### iOS Platform Considerations

**App Lifecycle:**
- Handle `applicationDidEnterBackground` and `applicationWillEnterForeground`
- Save state before backgrounding (iOS can terminate at any time)
- Use Background Modes capability for background uploads
- Register background tasks in `Info.plist`

**Permissions:**
- Request permissions with usage descriptions in `Info.plist`:
  - `NSPhotoLibraryUsageDescription` - Photo library access
  - `NSCameraUsageDescription` - Camera access
  - `NSPhotoLibraryAddUsageDescription` - Save to photo library
- Handle permission denial gracefully
- Show permission prompts at appropriate times (not on launch)

**Data Sharing (App & Extensions):**
- Configure App Groups: `group.com.xerahs.mobile`
- Use `NSUserDefaults` with suite name for shared preferences
- Share files via shared container: `FileManager.default.containerURL(forSecurityApplicationGroupIdentifier:)`

**Background Uploads:**
- Use `NSURLSession` with background configuration
- Handle upload completion in `application(_:handleEventsForBackgroundURLSession:)`
- Background uploads continue even if app is terminated

**iOS Share Extension Workflow:**
1. User shares image from Photos/Safari/etc.
2. iOS launches `XerahS.Mobile.iOS.ShareExtension`
3. Extension receives `NSExtensionItem` with attachments
4. Process image, show minimal UI (thumbnail, destination picker)
5. Save to shared container for main app to upload
6. Or perform upload directly if using background URLSession
7. Complete extension with success/failure

**Safe Area Handling:**
- Use `SafeAreaInsets` in MAUI
- Respect `view.safeAreaLayoutGuide` in native iOS code
- Account for notch, home indicator, status bar

### iOS vs Android Feature Mapping

| Feature | Android Implementation | iOS Implementation |
|---------|------------------------|-------------------|
| Photo Picker | Photo Picker API (API 29+) | PHPickerViewController (iOS 14+) |
| Background Upload | WorkManager | BGTaskScheduler + URLSession |
| Notifications | NotificationManager | UNUserNotificationCenter |
| Share Receive | Intent Filter | Share Extension |
| Secure Storage | EncryptedSharedPreferences + Keystore | Keychain Services |
| Biometric Auth | BiometricPrompt | LocalAuthentication (Face ID/Touch ID) |
| File Picker | Storage Access Framework | UIDocumentPickerViewController |
| Background Service | Foreground Service | Background Modes + BGTaskScheduler |
| Gesture Navigation | Navigation Component | UINavigationController |
| Bottom Nav | BottomNavigationView | UITabBarController |

---

## Phase 1: Feature Assessment and Analysis

### Task

1. **Review mobile feature requirements:**
   - Assess required functionality across feature modules
   - Focus on: capture, annotation, upload, history, cloud storage, settings

2. **Identify every distinct functional requirement** for the mobile application.

**Key Mobile Features Required:**

**Core Features:**
- Image Browser (Modern Photo Picker integration)
- Annotation Editor (freehand, shapes, arrows, text, blur, numbered steps)
- Crop Tool (draggable handles, grid overlay, pre/post annotation)
- Annotation Selection (tap-to-select, delete, per-annotation opacity)
- HSV Color Picker (hue/saturation canvas, brightness slider, hex input)
- Canvas Zoom/Pan (pinch-to-zoom, two-finger pan)

**Upload & Destinations:**
- Multi-Image Upload (batch selection, progress tracking)
- Multi-Destination Upload (S3, Imgur, FTP, SFTP, local save)
- Image Quality Controls (JPEG compression, max dimension resize)
- Connection Testing (test S3/FTP/SFTP from settings)
- Custom File Naming (pattern-based: `{original}`, `{date}`, `{time}`, `{timestamp}`, `{random}`)
- Background Upload Worker (Background task integration)
- Auto-Copy URL (clipboard after successful upload)

**S3 Explorer:**
- Browse, search, preview, download, delete files in S3 bucket
- Folder navigation with breadcrumbs
- List/Grid views with image thumbnails
- Sorting (name/size/date/type)
- Create folders, rename, move files
- Bucket Stats (file type breakdown, age distribution, storage growth charts, monthly cost calculator)

**Organization Features:**
- Albums & Tags (organize uploads, assign multiple tags)
- Filter history by album or tag
- Manage albums and tags from history screen

**History & Management:**
- Upload History (searchable with thumbnails)
- Date filters
- Album/Tag filtering
- Swipe-to-delete
- Fullscreen image preview with pinch-to-zoom

**Settings & Customization:**
- Theme Options (System, Light, Dark with multiple color themes)
- OLED pure black mode
- Biometric Lock (app-wide or credential screens only)
- Settings Backup (export/import as JSON including credentials)

**App Features:**
- In-App Updates (check versions, changelog, download from GitHub Releases)
- Share Intent (receive shared images from other apps)
- Onboarding (3-page walkthrough on first launch)

**Platform-Specific Features:**

| Feature Category | Android Implementation | iOS Implementation |
|------------------|------------------------|--------------------|
| **Image Selection** | Photo Picker API | PHPickerViewController |
| **Share Receive** | Intent Filter (manifest) | Share Extension |
| **Background Upload** | WorkManager | BGTaskScheduler + URLSession |
| **Permissions** | Runtime permissions | Info.plist + runtime requests |
| **Foreground Service** | Foreground Service | Background modes |
| **Secure Storage** | EncryptedSharedPreferences + Keystore | Keychain Services |
| **Biometric Auth** | BiometricPrompt | LocalAuthentication |
| **Deep Linking** | Intent filters | Universal Links + URL schemes |
| **Notifications** | NotificationManager | UNUserNotificationCenter |
| **File Access** | Storage Access Framework | UIDocumentPickerViewController |

3. **Assess these capabilities against existing XerahS mobile projects:**
   - `XerahS.Mobile.Maui`
   - `XerahS.Mobile.UI`
   - `XerahS.Mobile.Android`
   - `XerahS.Mobile.iOS`
   - `XerahS.Mobile.iOS.ShareExtension`
   - `XerahS.Core`
   - `XerahS.Platform.Mobile`

4. **Categorise each feature** as:
   - ✅ **COMPLETE** - Feature exists with full parity on BOTH Android and iOS
   - ⚠️ **PARTIAL** - Feature exists but incomplete, or missing on one platform (Android or iOS)
   - ❌ **MISSING** - Feature does not exist on either platform

### Required Output Format

Produce a structured **Feature Assessment Report** in this format:

```markdown
### Feature: Photo Browser / Image Picker
**Feature Area:** Image capture and selection
**Current Status:** ⚠️ PARTIAL  
**Android Status:** ✅ Exists (basic implementation)
**iOS Status:** ❌ Missing
**Missing Components:**  
- **Android:** Modern Photo Picker API integration (currently uses legacy picker)
- **iOS:** PHPickerViewController implementation (no photo picker at all)
- **Shared:** Multi-select support
- **Shared:** Permission request workflow
**Recommended Implementation Layer:**  
- **Interface:** `XerahS.Platform.Abstractions/IPhotosService.cs`
  ```csharp
  Task<List<PhotoAsset>> PickPhotosAsync(int maxCount);
  Task<bool> RequestPhotoLibraryPermissionAsync();
  ```
- **Android Impl:** `XerahS.Mobile.Android/AndroidPhotosService.cs` (use Photo Picker API)
- **iOS Impl:** `XerahS.Mobile.iOS/iOSPhotosService.cs` (use PHPickerViewController)
- **ViewModel:** `XerahS.Mobile.UI/ViewModels/PhotoBrowserViewModel.cs`
- **View:** `XerahS.Mobile.UI/Views/PhotoBrowserPage.axaml`

---

### Feature: Background Upload Worker
**Feature Area:** Background task processing
**Current Status:** ❌ MISSING  
**Android Status:** ❌ Missing
**iOS Status:** ❌ Missing
**Missing Components:**
- **Android:** WorkManager-based background upload worker
- **iOS:** BGTaskScheduler + URLSession background upload
- **Shared:** Upload queue manager in XerahS.Core
- **Shared:** Upload progress tracking
- **Shared:** Retry logic for failed uploads
**Recommended Implementation Layer:**
- **Interface:** `XerahS.Core/Services/IUploadQueueService.cs`
- **Core Logic:** `XerahS.Core/Managers/UploadQueueManager.cs`
- **Android Impl:** `XerahS.Mobile.Android/Workers/BackgroundUploadWorker.cs` (WorkManager)
- **iOS Impl:** `XerahS.Mobile.iOS/BackgroundTasks/UploadBackgroundTask.cs` (BGTaskScheduler)
- **iOS Background:** `XerahS.Mobile.iOS/Services/BackgroundUploadService.cs` (URLSession)
- **ViewModel:** `XerahS.Mobile.UI/ViewModels/UploadQueueViewModel.cs`

---

### Feature: Share Intent / Share Extension
**Feature Area:** Inter-app content sharing
**Current Status:** ⚠️ PARTIAL  
**Android Status:** ⚠️ Partial (intent filter exists but not fully handled)
**iOS Status:** ✅ Exists (`XerahS.Mobile.iOS.ShareExtension` project exists)
**Missing Components:**
- **Android:** Proper intent handling in MainActivity for shared images
- **Android:** Support for multiple shared items
- **Shared:** Shared content processing service
**Recommended Implementation Layer:**
- **Interface:** `XerahS.Platform.Abstractions/IShareReceiver.cs`
- **Android Impl:** `XerahS.Mobile.Android/MainActivity.cs` (override OnNewIntent)
- **iOS Impl:** `XerahS.Mobile.iOS.ShareExtension/ShareViewController.cs` (already exists, verify implementation)
- **Shared Service:** `XerahS.Core/Services/SharedContentHandler.cs`
```

**Repeat for EVERY feature discovered.**

**Key Points:**
- For each feature, specify **both** Android and iOS status separately
- Provide **platform-specific implementation paths** for Android AND iOS
- Identify **shared business logic** that goes in XerahS.Core
- Include code snippets for new interfaces when helpful

**DO NOT IMPLEMENT ANYTHING YET** - This phase is analysis only.

---

## Phase 2: Implementation Protocol

For each ❌ **MISSING** or ⚠️ **PARTIAL** feature:

### Step 1: Logic Verification

**Determine whether equivalent business logic already exists:**

**Check these locations IN ORDER:**

1. **XerahS.Core** (`C:\Users\liveu\source\repos\ShareX Team\XerahS\src\XerahS.Core`)
   - `/Models/` - Data models, DTOs
   - `/Managers/` - Business logic managers
   - `/Services/` - Core business services
   - `/Helpers/` - Utility classes
   - `/Uploaders/` - Upload destination logic (S3, FTP, Imgur, etc.)

2. **XerahS.Services** (`C:\Users\liveu\source\repos\ShareX Team\XerahS\src\XerahS.Services`)
   - Service implementations for cross-platform features

3. **XerahS.Platform.Mobile** (`C:\Users\liveu\source\repos\ShareX Team\XerahS\src\XerahS.Platform.Mobile`)
   - Mobile-common platform service implementations

4. **XerahS.Mobile.UI/ViewModels** (`C:\Users\liveu\source\repos\ShareX Team\XerahS\src\XerahS.Mobile.UI\ViewModels`)
   - Existing mobile ViewModels

**If equivalent logic EXISTS:**
- ✅ **Reuse** the existing service/manager/ViewModel
- Bind the View/Page to the existing ViewModel
- Extend the interface/implementation if Android needs additional methods
- **DO NOT** create duplicate classes

**If equivalent logic DOES NOT EXIST:**

**A. Create the Interface:**
- **Platform service?** → Add to `XerahS.Platform.Abstractions/I[ServiceName].cs`
- **App service?** → Add to `XerahS.Services.Abstractions/I[ServiceName].cs`

**B. Create the Implementation:**
- **Cross-platform logic?** → `XerahS.Services/[ServiceName].cs`
- **Mobile-common logic (shared by Android & iOS)?** → `XerahS.Platform.Mobile/Mobile[ServiceName].cs`
- **Android-only logic (Java/Kotlin interop, Android APIs)?** → `XerahS.Mobile.Android/Android[ServiceName].cs`
  - Examples: WorkManager, MediaStore, Storage Access Framework, Foreground Services
- **iOS-only logic (Objective-C/Swift interop, iOS APIs)?** → `XerahS.Mobile.iOS/iOS[ServiceName].cs`
  - Examples: Photos Framework, BackgroundTasks, UserNotifications, Share Extension

**C. Register in DI:**
- Locate the DI registration code (typically in `MauiProgram.cs` or startup file)
- Register the service with appropriate lifetime (Singleton/Transient/Scoped)

**D. Create ViewModel (if needed):**
- Add to `XerahS.Mobile.UI/ViewModels/[Feature]ViewModel.cs`
- Inherit from `ObservableObject` (CommunityToolkit.Mvvm)
- Use constructor injection for dependencies
- Use `[ObservableProperty]` for bindable properties
- Use `[RelayCommand]` or `AsyncRelayCommand` for commands

**E. Create View (if needed):**
- Add to `XerahS.Mobile.UI/Views/[Feature]Page.axaml` and `.axaml.cs`
- Bind to the ViewModel
- Use data binding, **no business logic in code-behind**

**CRITICAL RULE:**  
Never implement business logic directly in:
- Views (`.axaml.cs` code-behind)
- Platform-specific projects (unless it's platform-specific API calls)
- ViewModels (ViewModels should **orchestrate**, not **implement** business logic)

### Step 2: UI Design and Implementation

**Design and develop modern mobile UI interfaces** for XerahS.

#### Mobile UI Pattern → XerahS XAML Mapping

**Modern Mobile UI → Avalonia XAML Control:**

| Modern Mobile Pattern | XerahS (Avalonia XAML) | Notes |
|---------------------------|------------------------|-------|
| Scrollable list | `ListBox` with virtualization | Use `VirtualizingStackPanel` |
| Horizontal gallery | Horizontal `ItemsRepeater` | For galleries |
| Text input field | `TextBox` with styled `Border` | Material-inspired style |
| Card container | `Border` with `CornerRadius`, `BoxShadow` | Use theme colors |
| Floating action button | Custom styled `Button` with circular shape | Position in Canvas or Grid |
| Modal dialog | Custom `Window` or `IDialogService` | Prefer service abstraction |
| Bottom sheet | `Popup` or custom overlay | Animate from bottom |
| App scaffold with header | `Grid` with header row | Or custom `TitleBar` control |
| Dropdown menu | `ComboBox` or `ContextMenu` | Native feel |
| Toggle switch | `ToggleSwitch` | With Material styling |
| Checkbox | `CheckBox` | Styled for mobile |
| Radio button | `RadioButton` | Grouped in `StackPanel` |
| Progress bar | `ProgressBar` | Determinate progress |
| Loading spinner | Custom circular progress or spinner | For indeterminate loading |
| Async image | `Image` with bitmap source | Async loading via ViewModel |
| Reactive state | ViewModel `[ObservableProperty]` | Reactive binding |
| View injection | Constructor injection | DI-based |

#### UI Excellence Guidelines

**Create exceptional user experiences:**

| Starting Point | Enhanced Implementation |
|------------------------|-------------------|
| Basic list view | `CollectionView`/`ListBox` + skeleton loading + pull-to-refresh |
| Simple progress indicator | Non-blocking overlay with semi-transparent background |
| Fixed layout dimensions | Adaptive spacing using theme resources and responsive Grid |
| Basic list items | Rich item template with icons, secondary text, swipe actions |
| Static colors | Theme-aware colors (Light/Dark/HighContrast) |
| Simple buttons | Styled button with hover/pressed states, icons, ripple effect |

**Mobile UX Best Practices:**
- Touch targets ≥ 44pt (about 48dp on Android)
- Use finger-friendly spacing (not mouse-precise)
- Support gestures: swipe-to-delete, pull-to-refresh, pinch-to-zoom
- Show loading states during async operations
- Provide visual feedback on interactions (ripple, highlight)
- Handle empty states gracefully (empty list message + action)
- Support landscape and portrait orientations
- Handle safe areas (notches, rounded corners, gesture bars)

### Step 3: Code Quality Enforcement

**Enforce strict coding standards:**

#### Async/Await
- ✅ **Always use `async`/`await`** for I/O operations
- ❌ **NEVER use `.Result` or `.Wait()`** (causes deadlocks)
- ✅ Use `ConfigureAwait(false)` in library code (not ViewModels)
- ✅ Use `AsyncRelayCommand` for async command handlers

**Example:**
```csharp
// ❌ BAD
public void UploadImage()
{
    _uploadService.UploadAsync(image).Wait(); // Deadlock risk!
}

// ✅ GOOD
[RelayCommand]
private async Task UploadImageAsync()
{
    await _uploadService.UploadAsync(image);
}
```

#### Strings & Localization
- ✅ Extract all user-facing strings to `.resx` resource files
- ❌ **NO hard-coded strings** in Views or ViewModels
- ✅ Use resource keys: `Resources.Strings.Upload_Success`
- ✅ Support localization from the start

#### Magic Numbers
- ❌ **NO magic numbers** in code
- ✅ Define constants or use theme resources
- ✅ Use named values for spacing, sizes, timeouts

**Example:**
```csharp
// ❌ BAD
var maxWidth = 1920;
await Task.Delay(5000);

// ✅ GOOD
private const int MaxImageWidth = 1920;
private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(5);

var maxWidth = MaxImageWidth;
await Task.Delay(UploadTimeout);
```

#### Nullable Reference Types
- ✅ **Enable NRT** in all projects: `<Nullable>enable</Nullable>`
- ✅ Use `?` for nullable types: `string? optionalName`
- ✅ Use null-coalescing: `name ?? "Default"`
- ❌ Avoid suppression (`!`) except when absolutely necessary

#### Logging
- ✅ Use `ILogger<T>` for logging (Microsoft.Extensions.Logging)
- ✅ Log at appropriate levels: Trace, Debug, Information, Warning, Error, Critical
- ❌ **NO `Console.WriteLine`** in production code
- ✅ Include context in log messages

**Example:**
```csharp
_logger.LogInformation("Uploading image {FileName} to {Destination}", fileName, destination);
_logger.LogError(ex, "Failed to upload image {FileName}", fileName);
```

#### Models & DTOs
- ✅ Use **immutable DTOs** with `record` types where appropriate
- ❌ **NO duplicate model definitions** between projects
- ✅ Share models via `XerahS.Core/Models/`
- ✅ Use `required` keyword for mandatory properties (.NET 7+)

**Example:**
```csharp
// Immutable DTO
public record UploadResult(string Url, long FileSize, DateTime UploadedAt);

// Model with required properties
public class UploadSettings
{
    public required string Destination { get; init; }
    public int? MaxWidth { get; init; }
}
```

#### Dependency Injection
- ✅ **Constructor injection only** (no service locator)
- ✅ Inject interfaces, not concrete types
- ✅ Register services in DI container
- ❌ **NO static service access** or global state

#### XerahS-Specific Rules
- ✅ Follow strict nullability (see `CODING_STANDARDS.md`)
- ✅ Add **license headers** to all `.cs` files (see `CODING_STANDARDS.md`)
- ✅ `TreatWarningsAsErrors` must remain **enabled**
- ✅ Target framework: `net10.0-windows10.0.26100.0`
- ✅ Build must pass with **0 errors, 0 warnings** before push

---

## Execution Flow

1. **Review mobile application requirements and architecture**
2. **Generate the full Feature Assessment Report** evaluating current XerahS mobile implementation
3. **Identify platform-specific requirements** for each feature:
   - What requires Android-specific implementation?
   - What requires iOS-specific implementation?
   - What can be shared via XerahS.Core?
4. **Wait for explicit confirmation** before implementing the first feature

Do not begin implementation until instructed.

---

## Platform-Specific Implementation Patterns

### Pattern 1: Fully Cross-Platform Feature
**Example:** Image compression settings
- Interface: `XerahS.Services.Abstractions/IImageCompressionService.cs`
- Implementation: `XerahS.Services/ImageCompressionService.cs`
- ViewModel: `XerahS.Mobile.UI/ViewModels/ImageSettingsViewModel.cs`
- View: `XerahS.Mobile.UI/Views/ImageSettingsPage.axaml`

### Pattern 2: Platform-Specific with Shared Interface
**Example:** Photo picker
- Interface: `XerahS.Platform.Abstractions/IPhotosService.cs`
- Android: `XerahS.Mobile.Android/AndroidPhotosService.cs` (uses Android Photo Picker API)
- iOS: `XerahS.Mobile.iOS/iOSPhotosService.cs` (uses PHPickerViewController)
- ViewModel: `XerahS.Mobile.UI/ViewModels/PhotoBrowserViewModel.cs` (uses IPhotosService)

### Pattern 3: Platform-Specific Feature
**Example:** Android WorkManager upload worker
- Android: `XerahS.Mobile.Android/Workers/UploadWorker.cs`
- iOS equivalent: `XerahS.Mobile.iOS/BackgroundTasks/UploadBackgroundTask.cs`
- Shared upload logic: `XerahS.Core/Services/UploadManager.cs`

### Pattern 4: iOS Share Extension
**Example:** Receive shared images
- Extension: `XerahS.Mobile.iOS.ShareExtension/ShareViewController.cs`
- Android equivalent: Intent filter in `MainActivity.cs`
- Shared processing: `XerahS.Core/Services/SharedContentHandler.cs`

---

## Important Notes

- Focus on **native implementations** for both Android and iOS
- Shared business logic goes in `XerahS.Core`
- Platform-specific UI patterns should be respected (Material vs Cupertino)


