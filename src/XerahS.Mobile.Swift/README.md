# XerahS Mobile (Swift)

Native iOS app for XerahS, built with **Swift** and **SwiftUI**. Mirrors the feature set of the Android app ([XerahS.Mobile.Kt](../XerahS.Mobile.Kt)).

- **Language**: Swift 5
- **UI**: SwiftUI
- **Minimum**: iOS 17.0
- **Bundle ID**: `com.getsharex.xerahs.mobile`

## Features

- **Upload**: Queue files for upload; S3 or custom HTTP uploaders (same config as Android).
- **History**: SQLite-backed history (schema compatible with desktop); search, copy URL, delete.
- **Settings**: S3 credentials and region; custom uploaders (name, URL, form name, body).

## Building

Open `XerahSMobile.xcodeproj` in Xcode and build (⌘B) or run (⌘R) on a simulator or device. From the command line (macOS):

```bash
xcodebuild -project XerahSMobile.xcodeproj -scheme XerahSMobile -destination 'platform=iOS Simulator,name=iPhone 16' build
```

## Structure

- `XerahSMobile/` – app target (entry point, views, core and feature code)
- `XerahSMobile.xcodeproj/` – Xcode project

## Share from other apps

Receiving shared files from the system share sheet requires an **iOS Share Extension** (separate target) that writes shared items into a container the main app can read, then opens the main app with those paths. The main app already supports `pendingSharedPaths` for when it is opened with initial file URLs; a future Share Extension can populate this (e.g. via App Group or URL scheme).

Copyright (c) 2007-2026 ShareX Team.
