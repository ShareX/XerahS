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

**Share with XerahS** is implemented: you can select a photo or PDF (or other file) in Photos/Files, tap **Share**, choose **XerahS**, and the app will open and upload the file(s) and show the URL(s).

- A **Share Extension** target (`ShareExtension`) receives shared items from the system share sheet, copies them into the app group container (`group.com.getsharex.xerahs`), stores paths in shared UserDefaults, and opens the main app via `xerahs://share`.
- The main app reads pending paths on launch and when opened via that URL, passes them to the Upload screen, which enqueues and uploads them (S3 or custom uploader).

Copyright (c) 2007-2026 ShareX Team.
