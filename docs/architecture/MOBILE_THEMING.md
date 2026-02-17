# Mobile Native Theming Architecture

## Overview

XerahS mobile Avalonia UI uses runtime adaptive theming so each platform follows native visual language without changing business logic.

- iOS: Cupertino-oriented visual styles
- Android: Material-oriented visual styles
- Desktop/fallback: Fluent defaults from `MobileApp.axaml`

## Runtime Theme Loading

Theme loading is implemented in `src/XerahS.Mobile.UI/MobileApp.axaml.cs`.

Initialization flow:

1. Load base app XAML (`AvaloniaXamlLoader.Load(this)`)
2. Load shared adaptive styles (`Themes/AdaptiveControls.axaml`)
3. Detect platform with `OperatingSystem.IsIOS()` / `OperatingSystem.IsAndroid()`
4. Load platform theme dictionary (`Themes/iOS.axaml` or `Themes/Android.axaml`)
5. Set `PlatformTag` and assign `Tag` to root views

The `Tag` value is used by selectors such as:

- `UserControl[Tag=ios] Button.adaptive-primary`
- `UserControl[Tag=android] Grid.adaptive-navbar`

## Theme Structure

Theme files:

- `src/XerahS.Mobile.UI/Themes/AdaptiveControls.axaml`
- `src/XerahS.Mobile.UI/Themes/iOS.axaml`
- `src/XerahS.Mobile.UI/Themes/Android.axaml`

Responsibilities:

- `AdaptiveControls.axaml`: platform-agnostic class contracts (`adaptive-primary`, `adaptive-input`, `adaptive-card`, ...)
- `iOS.axaml`: Cupertino styles, typography, touch sizing, and dark/light palette
- `Android.axaml`: Material styles, elevation, typography, and dark/light palette

## View Layer Contract

Views only use adaptive classes and keep bindings/commands unchanged.

Current refactored views:

- `src/XerahS.Mobile.UI/Views/MobileUploadView.axaml`
- `src/XerahS.Mobile.UI/Views/MobileHistoryView.axaml`
- `src/XerahS.Mobile.UI/Views/MobileSettingsView.axaml`
- `src/XerahS.Mobile.UI/Views/MobileAmazonS3ConfigView.axaml`
- `src/XerahS.Mobile.UI/Views/MobileCustomUploaderConfigView.axaml`

## Head Project Native Integration

Additional host-level styling is applied in head projects:

- Android: system bar adaptation in `src/XerahS.Mobile.Android/MainActivity.cs`
- Android theme defaults in `src/XerahS.Mobile.Android/Resources/values/styles.xml`
- iOS app tint defaults in `src/XerahS.Mobile.iOS/AppDelegate.cs`
- iOS Share Extension tint/appearance alignment in `src/XerahS.Mobile.iOS.ShareExtension/ShareViewController.cs`

## Extension Points

When adding a new control style:

1. Add an adaptive class in `AdaptiveControls.axaml`
2. Implement platform selectors in both `iOS.axaml` and `Android.axaml`
3. Use the new class in view XAML
4. Build all mobile targets
