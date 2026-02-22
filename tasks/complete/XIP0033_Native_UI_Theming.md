# XIP0033: Native UI Theming for Avalonia Mobile Projects

**Status**: Draft  
**Created**: 2026-02-17  
**Area**: Mobile / UI/UX  
**Targets**: `XerahS.Mobile.UI`, `XerahS.Mobile.Android`, `XerahS.Mobile.iOS`  
**Goal**: Implement platform-adaptive theming to match native iOS (Cupertino) and Android (Material Design 3) look-and-feel in Avalonia-based mobile projects.

---

## Executive Summary

This XIP outlines the implementation of **"Skin-Walker" Native UI Protocol** for XerahS Avalonia mobile projects. The system will automatically apply platform-specific visual styles at runtime:  
- **iOS**: Cupertino/Apple Design Language (San Francisco fonts, frosted glass, minimal borders)  
- **Android**: Material Design 3 (ripple effects, elevation, filled/outlined inputs)

**Key Constraint**: This is a **visual-only refactor**. Business logic, bindings, ViewModels, and data flow remain unchanged. Only XAML styles, resource dictionaries, and visual tree modifications.

**Note**: The MAUI mobile stack (`XerahS.Mobile.Maui`) already provides native UI rendering via platform-specific controls and does not require this theming work.

---

## Problem Statement

### Current State
The Avalonia mobile projects (`XerahS.Mobile.UI`) currently use the generic `FluentTheme`, which results in a **desktop-like UI** on mobile platforms. This creates:
- ❌ Poor mobile user experience (too dense, improper touch targets)
- ❌ Unfamiliar interaction patterns for iOS/Android users
- ❌ Visual inconsistency with platform conventions

### Target State
- ✅ **iOS users** see a Cupertino-styled app (matching iOS design language)
- ✅ **Android users** see a Material Design 3 app (matching Android design language)
- ✅ Zero business logic changes (same ViewModels, same commands, same bindings)
- ✅ Maintainable via shared XAML layout with platform-specific styles

---

## Architecture Overview

### Design Principles

1. **Runtime Theme Switching**  
   - Detect platform in `MobileApp.axaml.cs` via `OperatingSystem.IsAndroid()` / `OperatingSystem.IsIOS()`
   - Load platform-specific `ResourceDictionary` dynamically

2. **Adaptive Style Classes**  
   - Use XAML `Classes` to apply platform-specific styles conditionally
   - Example: `<Button Classes.ios="cupertino-btn" Classes.android="material-btn" />`

3. **Visual-Only Scope**  
   - **DO**: Modify colors, fonts, corner radii, shadows, padding, spacing
   - **DO NOT**: Change bindings, commands, event handlers, ViewModels, navigation logic

4. **Shared Layout, Platform Styles**  
   - Keep single XAML view files (e.g., `MobileUploadView.axaml`)
   - Apply platform-specific styles via merged resource dictionaries

---

## Implementation Phases

---

## Phase 1: Theme Infrastructure Setup

### 1.1 Create Theme Directory Structure

Create the following folder in `XerahS.Mobile.UI`:

```
XerahS.Mobile.UI/
  Themes/
    iOS.axaml                    # Cupertino-style resource dictionary
    Android.axaml                # Material Design 3 resource dictionary
    AdaptiveControls.axaml       # Shared adaptive control styles
```

### 1.2 Modify `MobileApp.axaml.cs` for Runtime Theme Loading

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/MobileApp.axaml.cs`

**Changes Required**:

```csharp
public override void Initialize()
{
    AvaloniaXamlLoader.Load(this);
    
    // Load platform-specific theme
    LoadPlatformTheme();
}

private void LoadPlatformTheme()
{
    ResourceDictionary? platformTheme = null;

    if (OperatingSystem.IsIOS())
    {
        // Load Cupertino theme
        platformTheme = new ResourceDictionary();
        platformTheme.Source = new Uri("avares://XerahS.Mobile.UI/Themes/iOS.axaml");
    }
    else if (OperatingSystem.IsAndroid())
    {
        // Load Material Design 3 theme
        platformTheme = new ResourceDictionary();
        platformTheme.Source = new Uri("avares://XerahS.Mobile.UI/Themes/Android.axaml");
    }
    else
    {
        // Fallback to Fluent (desktop/testing environments)
        // FluentTheme is already loaded in MobileApp.axaml
        return;
    }

    if (platformTheme != null)
    {
        Resources.MergedDictionaries.Add(platformTheme);
    }
}
```

**Dependencies**:
- None (uses existing Avalonia XAML loading)

---

## Phase 2: iOS Cupertino Theme Implementation

### 2.1 Create `iOS.axaml` Resource Dictionary

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/Themes/iOS.axaml`

**Cupertino Design Principles**:
- **Typography**: San Francisco font family (system default on iOS)
- **Spacing**: Generous padding (16-20px margins)
- **Colors**: iOS blue (`#007AFF`), system grays
- **Interactions**: Fade-on-press (no ripples)
- **Borders**: Minimal or none (rely on background fills)
- **Corner Radius**: 10-12px for containers, 8px for buttons
- **Blur Effects**: Frosted glass for navigation bars

**Key Styles**:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Color Palette -->
    <Color x:Key="iOSBlue">#007AFF</Color>
    <Color x:Key="iOSSystemGrayLight">#E5E5EA</Color>
    <Color x:Key="iOSSystemGrayDark">#1C1C1E</Color>
    <Color x:Key="iOSBackgroundLight">#F2F2F7</Color>
    <Color x:Key="iOSBackgroundDark">#000000</Color>

    <!-- Typography -->
    <FontFamily x:Key="iOSFont">San Francisco, -apple-system, BlinkMacSystemFont, Segoe UI</FontFamily>
    <FontWeight x:Key="iOSFontRegular">400</FontWeight>
    <FontWeight x:Key="iOSFontSemiBold">600</FontWeight>
    <FontWeight x:Key="iOSFontBold">700</FontWeight>

    <!-- Button Styles -->
    <Style Selector="Button.cupertino-primary">
        <Setter Property="Background" Value="{StaticResource iOSBlue}"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="CornerRadius" Value="10"/>
        <Setter Property="Padding" Value="16,12"/>
        <Setter Property="FontFamily" Value="{StaticResource iOSFont}"/>
        <Setter Property="FontWeight" Value="{StaticResource iOSFontSemiBold}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="MinHeight" Value="44"/>
    </Style>

    <Style Selector="Button.cupertino-primary:pressed">
        <Setter Property="Opacity" Value="0.6"/>
    </Style>

    <Style Selector="Button.cupertino-secondary">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Foreground" Value="{StaticResource iOSBlue}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="FontFamily" Value="{StaticResource iOSFont}"/>
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="MinHeight" Value="44"/>
    </Style>

    <Style Selector="Button.cupertino-secondary:pressed">
        <Setter Property="Opacity" Value="0.5"/>
    </Style>

    <!-- TextBox Styles -->
    <Style Selector="TextBox.cupertino-input">
        <Setter Property="Background" Value="{StaticResource iOSSystemGrayLight}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="CornerRadius" Value="10"/>
        <Setter Property="Padding" Value="12,10"/>
        <Setter Property="FontFamily" Value="{StaticResource iOSFont}"/>
        <Setter Property="MinHeight" Value="44"/>
    </Style>

    <!-- Navigation Bar Styles -->
    <Style Selector="Grid.cupertino-navbar">
        <Setter Property="Background" Value="#F8F8F8"/>
        <Setter Property="Height" Value="56"/>
        <!-- Future: Add ExperimentalAcrylicBorder for blur effect -->
    </Style>

    <Style Selector="TextBlock.cupertino-navbar-title">
        <Setter Property="FontFamily" Value="{StaticResource iOSFont}"/>
        <Setter Property="FontSize" Value="17"/>
        <Setter Property="FontWeight" Value="{StaticResource iOSFontSemiBold}"/>
        <Setter Property="HorizontalAlignment" Value="Center"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- List/ScrollViewer Styles -->
    <Style Selector="ScrollViewer.cupertino-scroll">
        <Setter Property="ScrollViewer.IsScrollInertiaEnabled" Value="True"/>
    </Style>

    <!-- Card/Panel Styles -->
    <Style Selector="Border.cupertino-card">
        <Setter Property="Background" Value="White"/>
        <Setter Property="CornerRadius" Value="12"/>
        <Setter Property="Padding" Value="16"/>
        <Setter Property="Margin" Value="16,8"/>
    </Style>

</ResourceDictionary>
```

### 2.2 iOS-Specific Enhancements (Optional Phase 2.5)

**Frosted Glass Navigation Bar** (using `ExperimentalAcrylicBorder`):

```xml
<!-- Advanced: Add to iOS.axaml when acrylic is stable -->
<Style Selector="ExperimentalAcrylicBorder.cupertino-navbar-blur">
    <Setter Property="Material">
        <Setter.Value>
            <ExperimentalAcrylicMaterial TintColor="#F8F8F8" TintOpacity="0.7" />
        </Setter.Value>
    </Setter>
</Style>
```

---

## Phase 3: Android Material Design 3 Theme Implementation

### 3.1 Add Material.Avalonia NuGet Package

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.UI.csproj`

Add package reference:

```xml
<PackageReference Include="Material.Avalonia" Version="3.7.4" />
<PackageReference Include="Material.Icons.Avalonia" Version="2.1.10" />
```

**Note**: Verify compatible version with Avalonia 11.3.12. If version conflicts occur, use the latest compatible version.

### 3.2 Create `Android.axaml` Resource Dictionary

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/Themes/Android.axaml`

**Material Design 3 Principles**:
- **Typography**: Roboto font family
- **Elevation**: Shadows for depth (cards at 2dp, FABs at 6dp)
- **Ripple Effects**: Touch feedback on all interactive elements
- **Colors**: Dynamic color system (primary, secondary, surface)
- **Corner Radius**: 12-16px for containers, 24px for buttons (pill shape)
- **Navigation**: Left-aligned titles, hamburger menu icon

**Key Styles**:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:material="clr-namespace:Material.Styles;assembly=Material.Styles">

    <!-- Import Material.Avalonia Base Theme -->
    <ResourceDictionary.MergedDictionaries>
        <material:MaterialTheme BaseTheme="Light" PrimaryColor="DeepPurple" SecondaryColor="Teal"/>
    </ResourceDictionary.MergedDictionaries>

    <!-- XerahS Brand Colors (override Material defaults) -->
    <Color x:Key="MaterialPrimaryColor">#6366F1</Color> <!-- Indigo-500 (XerahS brand) -->
    <Color x:Key="MaterialSecondaryColor">#10B981</Color> <!-- Emerald-500 -->
    <Color x:Key="MaterialSurfaceColor">#FFFFFF</Color>
    <Color x:Key="MaterialBackgroundColor">#F5F5F5</Color>

    <!-- Button Styles -->
    <Style Selector="Button.material-primary">
        <Setter Property="Theme" Value="{StaticResource MaterialFlatButton}"/>
        <Setter Property="Background" Value="{StaticResource MaterialPrimaryColor}"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="CornerRadius" Value="24"/>
        <Setter Property="Padding" Value="24,12"/>
        <Setter Property="MinHeight" Value="48"/>
        <Setter Property="FontFamily" Value="Roboto, Segoe UI"/>
        <Setter Property="FontWeight" Value="500"/>
    </Style>

    <Style Selector="Button.material-outlined">
        <Setter Property="Theme" Value="{StaticResource MaterialOutlineButton}"/>
        <Setter Property="BorderBrush" Value="{StaticResource MaterialPrimaryColor}"/>
        <Setter Property="Foreground" Value="{StaticResource MaterialPrimaryColor}"/>
        <Setter Property="CornerRadius" Value="24"/>
        <Setter Property="Padding" Value="24,12"/>
        <Setter Property="MinHeight" Value="48"/>
    </Style>

    <!-- TextBox Styles -->
    <Style Selector="TextBox.material-filled">
        <Setter Property="Theme" Value="{StaticResource MaterialFilledTextBox}"/>
        <Setter Property="MinHeight" Value="56"/>
    </Style>

    <Style Selector="TextBox.material-outlined">
        <Setter Property="Theme" Value="{StaticResource MaterialOutlineTextBox}"/>
        <Setter Property="MinHeight" Value="56"/>
        <Setter Property="CornerRadius" Value="4"/>
    </Style>

    <!-- Navigation Bar Styles -->
    <Style Selector="Grid.material-appbar">
        <Setter Property="Background" Value="{StaticResource MaterialSurfaceColor}"/>
        <Setter Property="Height" Value="56"/>
    </Style>

    <Style Selector="TextBlock.material-appbar-title">
        <Setter Property="FontFamily" Value="Roboto, Segoe UI"/>
        <Setter Property="FontSize" Value="20"/>
        <Setter Property="FontWeight" Value="500"/>
        <Setter Property="HorizontalAlignment" Value="Left"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
        <Setter Property="Margin" Value="56,0,0,0"/> <!-- Space for hamburger icon -->
    </Style>

    <!-- Card Styles -->
    <Style Selector="Border.material-card">
        <Setter Property="Background" Value="{StaticResource MaterialSurfaceColor}"/>
        <Setter Property="CornerRadius" Value="12"/>
        <Setter Property="Padding" Value="16"/>
        <Setter Property="Margin" Value="16,8"/>
        <Setter Property="BoxShadow" Value="0 2 4 0 #40000000"/> <!-- 2dp elevation -->
    </Style>

    <!-- Elevated Card (for emphasis) -->
    <Style Selector="Border.material-card-elevated">
        <Setter Property="Background" Value="{StaticResource MaterialSurfaceColor}"/>
        <Setter Property="CornerRadius" Value="12"/>
        <Setter Property="Padding" Value="16"/>
        <Setter Property="Margin" Value="16,8"/>
        <Setter Property="BoxShadow" Value="0 4 8 0 #40000000"/> <!-- 4dp elevation -->
    </Style>

</ResourceDictionary>
```

### 3.3 Material Icons Integration

**Use Case**: Back arrow, hamburger menu, settings icon

```xml
<!-- Example usage in views -->
<material:MaterialIcon Kind="ArrowBack" Width="24" Height="24"/>
<material:MaterialIcon Kind="Menu" Width="24" Height="24"/>
<material:MaterialIcon Kind="Settings" Width="24" Height="24"/>
```

---

## Phase 4: Adaptive Controls and View Refactoring

### 4.1 Create Adaptive Control Template Helpers

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/Themes/AdaptiveControls.axaml`

This resource dictionary provides PLATFORM-AGNOSTIC style class names that automatically map to the correct platform theme.

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Adaptive Primary Button -->
    <Style Selector="Button.adaptive-primary">
        <!-- iOS: Apply cupertino-primary when on iOS -->
        <Setter Property="Classes" Value="cupertino-primary"/>
    </Style>

    <!-- Override for Android (Material takes precedence) -->
    <Style Selector="Button.adaptive-primary[Tag=android]">
        <Setter Property="Classes" Value="material-primary"/>
    </Style>

    <!-- Adaptive Secondary Button -->
    <Style Selector="Button.adaptive-secondary">
        <Setter Property="Classes" Value="cupertino-secondary"/>
    </Style>

    <Style Selector="Button.adaptive-secondary[Tag=android]">
        <Setter Property="Classes" Value="material-outlined"/>
    </Style>

    <!-- Adaptive TextBox -->
    <Style Selector="TextBox.adaptive-input">
        <Setter Property="Classes" Value="cupertino-input"/>
    </Style>

    <Style Selector="TextBox.adaptive-input[Tag=android]">
        <Setter Property="Classes" Value="material-filled"/>
    </Style>

    <!-- Adaptive Navigation Bar -->
    <Style Selector="Grid.adaptive-navbar">
        <Setter Property="Classes" Value="cupertino-navbar"/>
    </Style>

    <Style Selector="Grid.adaptive-navbar[Tag=android]">
        <Setter Property="Classes" Value="material-appbar"/>
    </Style>

</ResourceDictionary>
```

**Alternative Approach** (Runtime Class Injection):

Modify `MobileApp.axaml.cs` to set a global platform tag:

```csharp
private void LoadPlatformTheme()
{
    string platformTag = "desktop";
    
    if (OperatingSystem.IsIOS())
    {
        platformTheme = /* ... load iOS.axaml ... */;
        platformTag = "ios";
    }
    else if (OperatingSystem.IsAndroid())
    {
        platformTheme = /* ... load Android.axaml ... */;
        platformTag = "android";
    }

    Resources.MergedDictionaries.Add(platformTheme);
    Resources["PlatformTag"] = platformTag;
}
```

Then in XAML, use bindings:

```xml
<Button Classes.ios="cupertino-primary" 
        Classes.android="material-primary"
        Content="Upload" />
```

Avalonia's `Classes` property supports conditional application based on boolean properties or attached properties.

### 4.2 Refactor `MobileUploadView.axaml` (Proof of Concept)

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/Views/MobileUploadView.axaml`

**Current State** (Generic Fluent):
```xml
<Button Grid.Column="1"
        Content="History"
        Command="{Binding OpenHistoryCommand}"
        Background="Transparent"
        Margin="4,0"
        ToolTip.Tip="History"/>
```

**Refactored (Adaptive)**:
```xml
<Button Grid.Column="1"
        Content="History"
        Command="{Binding OpenHistoryCommand}"
        Classes="adaptive-secondary"
        Margin="4,0"
        ToolTip.Tip="History"/>
```

**Navigation Bar Refactor**:

Before:
```xml
<Grid Grid.Row="0"
      Background="{DynamicResource SystemControlBackgroundChromeMediumBrush}"
      Height="56"
      ColumnDefinitions="*, Auto, Auto">
    <TextBlock Grid.Column="0" Text="XerahS" FontSize="20" FontWeight="Bold" ... />
</Grid>
```

After:
```xml
<Grid Grid.Row="0"
      Classes="adaptive-navbar"
      ColumnDefinitions="*, Auto, Auto">
    <TextBlock Grid.Column="0" 
               Text="XerahS" 
               Classes="adaptive-navbar-title" />
    <!-- iOS: Centered title with SF font -->
    <!-- Android: Left-aligned title with Roboto font -->
</Grid>
```

**Additional Adaptive Style Classes to Define**:
```xml
<!-- In AdaptiveControls.axaml -->
<Style Selector="TextBlock.adaptive-navbar-title">
    <Setter Property="Classes" Value="cupertino-navbar-title"/>
</Style>

<Style Selector="TextBlock.adaptive-navbar-title[Tag=android]">
    <Setter Property="Classes" Value="material-appbar-title"/>
</Style>
```

### 4.3 Full View Refactoring Checklist

Apply adaptive styles to all existing mobile views:

- [x] `MobileUploadView.axaml` - Primary upload interface
- [x] `MobileHistoryView.axaml` - Upload history list
- [x] `MobileSettingsView.axaml` - Settings page
- [x] `MobileAmazonS3ConfigView.axaml` - S3 uploader config
- [x] `MobileCustomUploaderConfigView.axaml` - Custom uploader config

**For Each View**:
1. Replace hardcoded button styles with `adaptive-primary` / `adaptive-secondary`
2. Replace TextBox styles with `adaptive-input`
3. Replace navigation bars with `adaptive-navbar` Grid
4. Wrap content cards in `Border.adaptive-card`
5. Test on iOS simulator and Android emulator

---

## Phase 5: Testing and Validation

### 5.1 Platform-Specific Test Plan

**iOS Testing** (requires macOS with Xcode):
1. Build `XerahS.Mobile.iOS` project
2. Deploy to iOS Simulator (iPhone 15 Pro recommended)
3. Verify:
   - [ ] San Francisco font rendering
   - [ ] 44px minimum touch targets
   - [ ] Cupertino button fade-on-press (opacity 0.6)
   - [ ] No visible borders on input fields
   - [ ] Navigation bar background (future: blur effect)
   - [ ] Centered navigation title

**Android Testing** (requires Android SDK):
1. Build `XerahS.Mobile.Android` project
2. Deploy to Android Emulator (Pixel 7 API 34 recommended)
3. Verify:
   - [ ] Roboto font rendering
   - [ ] Material ripple effects on buttons
   - [ ] Elevation shadows on cards (2dp/4dp)
   - [ ] Filled or outlined TextBox styles
   - [ ] Left-aligned navigation title
   - [ ] Material icons rendering correctly

### 5.2 Cross-Platform Consistency Validation

**Test Scenarios**:
1. **Upload Flow**: Share image → Configure settings → Upload → View result
   - Verify visual consistency within each platform
   - Ensure no layout breaks, clipping, or overflow issues

2. **Navigation**: Switch between Upload, History, Settings views
   - Verify navigation bar adapts correctly
   - Ensure back buttons use platform conventions (chevron on iOS, arrow on Android)

3. **Text Input**: Enter long text in TextBox fields
   - Verify scrolling behavior
   - Ensure keyboard dismissal is native

4. **Dark Mode** (Future Phase):
   - iOS: Verify `iOSSystemGrayDark` / `iOSBackgroundDark` colors activate in dark mode
   - Android: Verify Material Theme switches to dark palette

### 5.3 Accessibility Testing

**iOS VoiceOver**:
- [ ] All buttons have accessible labels
- [ ] Minimum 44x44pt touch targets
- [ ] Contrast ratios meet WCAG AA standards

**Android TalkBack**:
- [ ] All buttons have `ContentDescription` (via Avalonia `AutomationProperties.Name`)
- [ ] Minimum 48x48dp touch targets
- [ ] Ripple effects are perceivable

---

## Phase 6: Advanced Enhancements (Optional)

### 6.1 iOS-Specific Advanced Features

**Frosted Glass Navigation Bar**:
- Implement `ExperimentalAcrylicBorder` when Avalonia Mobile supports it
- Fallback to semi-transparent background on unsupported versions

**Haptic Feedback**:
- Add platform service in `XerahS.Platform.iOS`:
  ```csharp
  public interface IHapticService
  {
      void SelectionChanged();
      void ImpactOccurred(HapticImpactStyle style);
      void NotificationOccurred(HapticNotificationType type);
  }
  ```
- Trigger on button presses via attached behavior

**SF Symbols Integration** (if Avalonia.iOS supports):
- Replace generic icons with SF Symbols
- Ensure proper sizing and weight matching

### 6.2 Android-Specific Advanced Features

**Material You Dynamic Colors** (Android 12+):
- Query system accent color via platform service
- Apply to `MaterialPrimaryColor` dynamically

**Predictive Back Gesture** (Android 13+):
- Implement navigation transitions that preview previous screen during back swipe

**Edge-to-Edge Layout**:
- Extend app content behind status/navigation bars
- Use window insets for proper padding

### 6.3 Shared Enhancements

**Smooth Scroll**:
- Enable inertia scrolling on both platforms:
  ```xml
  <ScrollViewer ScrollViewer.IsScrollInertiaEnabled="True">
  ```

**Animated Transitions**:
- Add page transitions (slide on Android, push on iOS):
  ```csharp
  // In navigation logic
  if (OperatingSystem.IsIOS())
      transition = new SlideTransition { Duration = TimeSpan.FromMilliseconds(350) };
  else if (OperatingSystem.IsAndroid())
      transition = new MaterialSlideTransition();
  ```

**Pull-to-Refresh** (for History view):
- Implement custom `PullToRefreshView` control
- Use Material spinner on Android, iOS spinner on iOS

---

## Dependencies and Prerequisites

### NuGet Packages

| Package | Version | Targets | Purpose |
|---------|---------|---------|---------|
| `Avalonia` | 11.3.12 | All | Core UI framework |
| `Avalonia.Themes.Fluent` | 11.3.12 | All | Fallback theme |
| `Material.Avalonia` | 3.7.4 | Android | Material Design 3 styles |
| `Material.Icons.Avalonia` | 2.1.10 | Android | Material icon pack |
| `SkiaSharp` | 2.88.9 | All | Rendering (already included) |

**Important**: Do NOT upgrade SkiaSharp to 3.x per XerahS build constraints.

### Build Configuration

Ensure `XerahS.Mobile.UI.csproj` continues to target `net10.0`:
```xml
<TargetFramework>net10.0</TargetFramework>
```

No changes to head projects (`XerahS.Mobile.iOS`, `XerahS.Mobile.Android`) required beyond consuming the updated UI library.

---

## Implementation Roadmap

### Sprint 1: Foundation (3-5 days)
- [x] Create `Themes/` folder structure
- [x] Implement runtime theme loading in `MobileApp.axaml.cs`
- [x] Create `iOS.axaml` with Cupertino base styles
- [x] Create `Android.axaml` with Material base styles (basic)
- [ ] Test theme loading on both platforms

### Sprint 2: iOS Refinement (3-4 days)
- [x] Complete all Cupertino control styles (buttons, inputs, cards)
- [x] Implement navigation bar styles
- [ ] Test on iOS Simulator
- [x] Fix layout/spacing issues
- [ ] Validate with iOS HIG (Human Interface Guidelines)

### Sprint 3: Android Refinement (3-4 days)
- [x] Add `Material.Avalonia` NuGet package
- [x] Complete Material Design 3 control styles
- [ ] Implement ripple effects
- [ ] Test on Android Emulator
- [ ] Validate with Material Design 3 guidelines

### Sprint 4: View Refactoring (5-6 days)
- [x] Create `AdaptiveControls.axaml`
- [x] Refactor `MobileUploadView.axaml` (proof of concept)
- [x] Refactor `MobileHistoryView.axaml`
- [x] Refactor `MobileSettingsView.axaml`
- [x] Refactor config views (S3, Custom Uploader)
- [ ] Cross-platform smoke testing

### Sprint 5: Testing & Polish (3-4 days)
- [ ] Comprehensive platform-specific testing
- [ ] Accessibility testing (VoiceOver, TalkBack)
- [x] Dark mode validation (if implemented)
- [ ] Performance profiling (ensure no regressions)
- [x] Documentation updates

### Sprint 6: Advanced Features (Optional, 4-6 days)
- [ ] Implement frosted glass on iOS (if Avalonia Mobile supports)
- [ ] Add haptic feedback on iOS
- [ ] Implement Material You dynamic colors on Android
- [ ] Add smooth animations/transitions
- [ ] Pull-to-refresh for History view

**Total Estimated Effort**: 18-29 days (excluding optional Sprint 6)

---

## Success Criteria

### Functional Requirements
- ✅ **iOS users** see Cupertino-styled UI (San Francisco font, rounded corners, fade effects)
- ✅ **Android users** see Material Design 3 UI (Roboto font, ripples, elevation)
- ✅ All existing functionality remains intact (no regressions in ViewModels, commands, bindings)
- ✅ App builds successfully with `dotnet build` (0 errors, 0 warnings)

### Visual Quality Requirements
- ✅ **iOS**: Matches Apple HIG for button sizing (44pt min), spacing, typography
- ✅ **Android**: Matches Material Design 3 for touch targets (48dp min), elevation, colors
- ✅ **Consistency**: Each platform's UI is internally consistent (no mixing of styles)

### Performance Requirements
- ✅ Theme loading adds < 100ms to app startup time
- ✅ No frame drops during scrolling or transitions
- ✅ Memory usage increase < 5MB from theme resources

### Accessibility Requirements
- ✅ All interactive elements have minimum touch target sizes (44pt iOS, 48dp Android)
- ✅ Color contrast ratios meet WCAG AA (4.5:1 for text, 3:1 for UI elements)
- ✅ Screen reader support (VoiceOver, TalkBack) remains functional

---

## Risks and Mitigations

### Risk 1: Avalonia Mobile Theme Support Immaturity
**Description**: Avalonia's mobile support is newer than desktop, some advanced features (acrylic blur, platform-specific controls) may not work.

**Mitigation**:
- Test early on real devices/simulators
- Have fallback styles (e.g., solid background if blur unsupported)
- Document unsupported features for future implementation

### Risk 2: Material.Avalonia Version Conflicts
**Description**: `Material.Avalonia` may not be compatible with Avalonia 11.3.12 or may conflict with SkiaSharp 2.88.9.

**Mitigation**:
- Test package installation immediately in Sprint 1
- If conflicts occur, manually implement Material styles without the library (more work, but feasible)
- Check Material.Avalonia GitHub for compatible version matrix

### Risk 3: Layout Breaks on Small Screens
**Description**: Adaptive styles may cause clipping or overflow on small devices (e.g., iPhone SE, small Android phones).

**Mitigation**:
- Test on smallest supported screen sizes (4" iOS, 5" Android)
- Use `MinWidth`/`MaxWidth` constraints
- Implement responsive font scaling

### Risk 4: Dark Mode Color Mismatches
**Description**: iOS/Android dark modes may render colors incorrectly or be hard to read.

**Mitigation**:
- Define separate light/dark color palettes in `iOS.axaml` and `Android.axaml`
- Use `{ThemeResource}` bindings that auto-switch on mode change
- Manual testing in both modes from day 1

### Risk 5: Business Logic Coupling Discovery
**Description**: During refactoring, we may discover that some XAML logic is tightly coupled to Fluent-specific behaviors.

**Mitigation**:
- Phase 4 starts with ONE view (`MobileUploadView`) as proof of concept
- If major issues found, re-architect before proceeding to other views
- Keep ViewModels untouched (strict separation of concerns)

---

## Deliverables

### Code Deliverables
1. `src/mobile-experimental/XerahS.Mobile.Ava/Themes/iOS.axaml` - Complete Cupertino resource dictionary
2. `src/mobile-experimental/XerahS.Mobile.Ava/Themes/Android.axaml` - Complete Material Design 3 resource dictionary
3. `src/mobile-experimental/XerahS.Mobile.Ava/Themes/AdaptiveControls.axaml` - Platform-agnostic adaptive styles
4. `src/mobile-experimental/XerahS.Mobile.Ava/MobileApp.axaml.cs` - Runtime theme loading logic
5. Refactored XAML views (5 files):
   - `MobileUploadView.axaml`
   - `MobileHistoryView.axaml`
   - `MobileSettingsView.axaml`
   - `MobileAmazonS3ConfigView.axaml`
   - `MobileCustomUploaderConfigView.axaml`

### Documentation Deliverables
1. `docs/architecture/MOBILE_THEMING.md` - Architecture guide for native theming system
2. `docs/development/MOBILE_STYLING_GUIDE.md` - Developer guide for using adaptive styles
3. Updated `README_DEVELOPERS.md` - Add section on mobile theme development

### Testing Deliverables
1. iOS test report (screenshots, accessibility audit)
2. Android test report (screenshots, accessibility audit)
3. Performance baseline comparison (startup time, memory, frame rate)

---

## References

### Design System Guidelines
- **iOS**: [Apple Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/)
- **Android**: [Material Design 3](https://m3.material.io/)

### Avalonia Documentation
- [Avalonia Styling](https://docs.avaloniaui.net/docs/guides/styles-and-resources)
- [Avalonia Resource Dictionaries](https://docs.avaloniaui.net/docs/guides/styles-and-resources/resources)
- [Avalonia Mobile](https://docs.avaloniaui.net/docs/guides/platforms/mobile)

### Material.Avalonia
- [GitHub Repository](https://github.com/AvaloniaCommunity/Material.Avalonia)
- [Demo App](https://github.com/AvaloniaCommunity/Material.Avalonia/tree/main/Material.Demo)

### Typography
- **iOS**: San Francisco Font ([Apple Fonts](https://developer.apple.com/fonts/))
- **Android**: Roboto Font ([Google Fonts](https://fonts.google.com/specimen/Roboto))

---

## Appendix A: Platform Detection Utilities

If needed, create a helper class for cleaner platform checks:

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/Helpers/PlatformHelper.cs`

```csharp
namespace XerahS.Mobile.UI.Helpers;

public static class PlatformHelper
{
    public static bool IsIOS => OperatingSystem.IsIOS();
    public static bool IsAndroid => OperatingSystem.IsAndroid();
    public static bool IsMobile => IsIOS || IsAndroid;
    
    public static string PlatformName
    {
        get
        {
            if (IsIOS) return "iOS";
            if (IsAndroid) return "Android";
            return "Desktop";
        }
    }
}
```

Usage in `MobileApp.axaml.cs`:
```csharp
if (PlatformHelper.IsIOS)
{
    // Load iOS theme
}
```

---

## Appendix B: Example Refactored View (Full)

**File**: `src/mobile-experimental/XerahS.Mobile.Ava/Views/MobileUploadView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:XerahS.Mobile.UI.ViewModels"
             x:Class="XerahS.Mobile.UI.Views.MobileUploadView"
             x:DataType="vm:MobileUploadViewModel">

    <Grid RowDefinitions="Auto, *">

        <!-- Adaptive Navigation Bar -->
        <Grid Grid.Row="0"
              Classes="adaptive-navbar"
              ColumnDefinitions="*, Auto, Auto">

            <TextBlock Grid.Column="0"
                       Text="XerahS"
                       Classes="adaptive-navbar-title"/>

            <Button Grid.Column="1"
                    Content="History"
                    Command="{Binding OpenHistoryCommand}"
                    Classes="adaptive-secondary"
                    Margin="4,0"/>

            <Button Grid.Column="2"
                    Content="Settings"
                    Command="{Binding OpenSettingsCommand}"
                    Classes="adaptive-secondary"
                    Margin="4,0"/>
        </Grid>

        <!-- Main Content -->
        <ScrollViewer Grid.Row="1" Classes="adaptive-scroll">
            <StackPanel Margin="20" Spacing="16">

                <!-- Status Card -->
                <Border Classes="adaptive-card">
                    <StackPanel Spacing="8">
                        <TextBlock Text="Status"
                                   FontSize="14"
                                   Opacity="0.7"/>
                        <TextBlock Text="{Binding StatusText}"
                                   FontSize="16"
                                   FontWeight="SemiBold"/>
                    </StackPanel>
                </Border>

                <!-- Upload Button -->
                <Button Content="Upload File"
                        Command="{Binding PickAndUploadCommand}"
                        Classes="adaptive-primary"
                        HorizontalAlignment="Stretch"/>

                <!-- Recent Uploads -->
                <Border Classes="adaptive-card">
                    <StackPanel Spacing="8">
                        <TextBlock Text="Recent Uploads"
                                   FontSize="14"
                                   Opacity="0.7"/>
                        <ItemsControl ItemsSource="{Binding RecentUploads}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding Url}"
                                               TextTrimming="CharacterEllipsis"/>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </Border>

            </StackPanel>
        </ScrollViewer>

    </Grid>

</UserControl>
```

**Result**:
- On iOS: Centered "XerahS" title, blue text buttons, rounded cards, fade-on-press
- On Android: Left-aligned "XerahS" title, material buttons with ripples, elevated cards

---

## Appendix C: Color Palette Reference

### iOS Colors (Light Mode)
| Name | Hex | Usage |
|------|-----|-------|
| Blue | `#007AFF` | Primary actions, links |
| System Gray (Light) | `#E5E5EA` | Input backgrounds |
| System Gray (Dark) | `#1C1C1E` | Text on light background |
| Background | `#F2F2F7` | Screen background |
| White | `#FFFFFF` | Card backgrounds |

### iOS Colors (Dark Mode)
| Name | Hex | Usage |
|------|-----|-------|
| Blue | `#0A84FF` | Primary actions (slightly lighter) |
| System Gray (Light) | `#3A3A3C` | Input backgrounds |
| System Gray (Dark) | `#E5E5EA` | Text on dark background |
| Background | `#000000` | Screen background |
| Elevated Background | `#1C1C1E` | Card backgrounds |

### Android Material 3 Colors (Light Mode)
| Name | Hex | Usage |
|------|-----|-------|
| Primary | `#6366F1` | XerahS brand (Indigo-500) |
| Secondary | `#10B981` | Secondary actions (Emerald-500) |
| Surface | `#FFFFFF` | Cards, sheets |
| Background | `#F5F5F5` | Screen background |
| On Primary | `#FFFFFF` | Text on primary color |

### Android Material 3 Colors (Dark Mode)
| Name | Hex | Usage |
|------|-----|-------|
| Primary | `#A5B4FC` | Lighter indigo for dark mode |
| Secondary | `#34D399` | Lighter emerald for dark mode |
| Surface | `#1E1E1E` | Cards, sheets |
| Background | `#121212` | Screen background |
| On Primary | `#000000` | Text on primary color |

---

## Change Log

| Date | Change | Reason |
|------|--------|--------|
| 2026-02-17 | Initial XIP0033 draft created | Implement native theming for Avalonia mobile |

---

## Approval & Sign-off

**Prepared By**: GitHub Copilot (AI Agent)  
**Review Status**: Pending  
**Next Steps**: Review by XerahS maintainers, approval, Sprint 1 kickoff

---

**End of XIP0033**
