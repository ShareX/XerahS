# Building XerahS Mobile with xtool

[xtool](https://xtool.sh) builds, signs and installs the iOS app from Linux or Windows without Xcode.
`XerahSMobile.xcodeproj` stays the source of truth for Xcode and App Store builds; `Package.swift`
and `xtool.yml` in `src/mobile/ios` describe the same app for xtool.

## Prerequisites

- Swift 6.4 toolchain and xtool, set up per <https://xtool.sh/documentation/xtooldocs/installation-linux>.
- `xtool setup` done (Apple ID login plus the Darwin SDK built from Xcode 27's `Xcode.xip`).
- An iPhone connected over USB and trusted (`xtool devices` lists it).

## Build and install

```bash
cd src/mobile/ios
XtoolSupport/xtool-dev.sh              # build, sign, install and launch on the device
XtoolSupport/xtool-dev.sh build --ipa  # only build xtool/XerahSMobile.ipa
```

`xtool-dev.sh` first runs `generate-info-plists.py`, which resolves the `$(...)` build settings in
the Xcode Info.plists (version and build number come from `project.pbxproj`) into
`XtoolSupport/.generated/`, because xtool copies Info.plist values verbatim.

## Differences from Xcode builds

- xtool signs with `XTL-<id>.`-prefixed bundle IDs (`XTL-7C5AS6VPUH.com.xerahs.xerahs.mobile` on
  the paid team), so an xtool build installs next to the App Store or Xcode build. xtool reuses the
  team's `group.com.xerahs.xerahs` App Group when it exists (so both installs share settings and
  history) and signs both targets with the `<team>.<app bundle ID>` keychain group.
  `AppIdentifiers.swift` resolves both at runtime instead of hardcoding them.
- Asset catalogs are not compiled. The app icon comes from `AppIcon.png`, and `Logo.png` is
  copied into the bundle root so `Image("Logo")` still resolves. The accent color falls back to
  the system default.
- The Share Extension also compiles some app sources, and SwiftPM does not allow a file in two
  targets, so its target is `ShareExtensionSources/`: symlinks to every file in the
  ShareExtension Sources build phase of `project.pbxproj`. Keep the two in step.
