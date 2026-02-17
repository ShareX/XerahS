# Mobile Styling Guide

## Purpose

Use adaptive classes for mobile views so the same XAML layout renders with platform-native styles on iOS and Android.

## Core Rules

- Do not hardcode platform look in view XAML.
- Keep bindings, commands, and view model wiring unchanged.
- Apply adaptive classes only.

## Adaptive Class Reference

Use these classes in mobile views:

- `adaptive-navbar`: top header container
- `adaptive-navbar-title`: header title text
- `adaptive-primary`: primary action button
- `adaptive-secondary`: secondary action button
- `adaptive-input`: `TextBox`/`ComboBox` input style
- `adaptive-card`: grouped card container
- `adaptive-scroll`: inertial scroll behavior

Example:

```xml
<Grid Classes="adaptive-navbar">
    <TextBlock Classes="adaptive-navbar-title" Text="XerahS"/>
    <Button Classes="adaptive-secondary" Content="History"/>
</Grid>

<Button Classes="adaptive-primary" Content="Upload"/>
<TextBox Classes="adaptive-input"/>
<Border Classes="adaptive-card"/>
```

## Platform Resolution

`MobileApp` assigns a `Tag` value to each root view:

- `ios`
- `android`
- `desktop` (fallback)

Theme selectors use this to apply platform-specific rules:

- `UserControl[Tag=ios] ...`
- `UserControl[Tag=android] ...`

## Adding a New Adaptive Style

1. Add a base adaptive style contract in `src/XerahS.Mobile.UI/Themes/AdaptiveControls.axaml`
2. Add iOS implementation in `src/XerahS.Mobile.UI/Themes/iOS.axaml`
3. Add Android implementation in `src/XerahS.Mobile.UI/Themes/Android.axaml`
4. Update views to use the new class
5. Build:
   - `dotnet build src/XerahS.Mobile.UI/XerahS.Mobile.UI.csproj`
   - `dotnet build src/XerahS.Mobile.iOS/XerahS.Mobile.iOS.csproj`
   - `dotnet build src/XerahS.Mobile.iOS.ShareExtension/XerahS.Mobile.iOS.ShareExtension.csproj`
   - `dotnet build src/XerahS.Mobile.Android/XerahS.Mobile.Android.csproj`

## Accessibility Baseline

- Minimum tap size: iOS 44, Android 48
- Keep button labels explicit (no icon-only controls unless labeled)
- Keep text contrast readable in both light and dark palettes

## Dark Mode

`iOS.axaml` and `Android.axaml` use `ThemeDictionaries` for light/dark colors.

When adding colors:

- Add matching keys to both `Light` and `Dark` dictionaries
- Use `DynamicResource` for brushes tied to those keys
- Avoid hardcoded light-only colors in adaptive styles
