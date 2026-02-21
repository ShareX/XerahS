# XerahS Mobile (Swift)

Native iOS app for XerahS, built with **Swift** and **SwiftUI**. Mirrors the feature set of the Android app ([XerahS.Mobile.Kt](../XerahS.Mobile.Kt)).

- **Language**: Swift 5
- **UI**: SwiftUI
- **Minimum**: iOS 17.0
- **Bundle ID**: `com.getsharex.xerahs.mobile`

## Building

Open `XerahSMobile.xcodeproj` in Xcode and build (⌘B) or run (⌘R) on a simulator or device. From the command line (macOS):

```bash
xcodebuild -project XerahSMobile.xcodeproj -scheme XerahSMobile -destination 'platform=iOS Simulator,name=iPhone 16' build
```

## Structure

- `XerahSMobile/` – app target (entry point, views, core and feature code)
- `XerahSMobile.xcodeproj/` – Xcode project

Copyright (c) 2007-2026 ShareX Team.
