---
name: mobile-experimental-ios-parity
description: Compare or implement experimental MAUI/Avalonia mobile parity with src/mobile/ios. Excludes native Android.
metadata:
  keywords:
    - mobile-experimental
    - avalonia
    - maui
    - ios
    - android
    - parity
    - native feel
    - share extension
    - sxcu
    - xsdc
---

# Mobile Experimental/iOS Parity

Use this skill when `src/mobile-experimental` must match `src/mobile/ios` feature-wise while keeping Avalonia, MAUI, Android, and iOS platform heads idiomatic.

Feature parity means the same user outcome, compatible persisted data, comparable error handling, and platform-native presentation. It does not mean copying SwiftUI into Avalonia/MAUI or forcing identical visual chrome.

## Non-negotiables

- Treat `src/mobile/ios` as the behavioral contract.
- Keep experimental code native to its stack:
  - Avalonia: `.axaml`, compiled bindings, `avares://` assets, platform services from `TopLevel`, Android/iOS platform heads where needed.
  - MAUI: XAML pages, Shell navigation, `FilePicker`, `Launcher`, platform heads where needed.
  - Android platform heads: intents, content URI handling, scoped cache files, Android package metadata.
  - iOS platform heads: bundle IDs, URL schemes, share extension/app-group behavior, iOS appearance defaults.
- Preserve cross-platform file/data contracts: `.sxcu`, `.xsdc`, upload queue/history records, destination IDs, generated URLs, and user-facing import messages where practical.
- Do not let experimental convenience drift from production identity. App IDs, URL schemes, document types, app group IDs, logo, version display, and About links should match `src/mobile/ios` unless intentionally forked.
- Use the Avalonia docs MCP rules at the start of Avalonia work. Avoid WPF-only patterns such as triggers, `pack://` URIs, and `DependencyProperty`.

## First pass

1. Read `AGENTS.md`, this skill, and `.ai/skills/mobile-android-ios-parity/SKILL.md` for shared parity lessons.
2. Inspect:
   - `src/mobile/ios/README.md`
   - `src/mobile/ios/XerahSMobile`
   - `src/mobile/ios/ShareExtension`
   - `src/mobile-experimental/XerahS.Mobile.Ava`
   - `src/mobile-experimental/XerahS.Mobile.Maui`
   - `src/mobile-experimental/XerahS.Mobile.Core`
   - `src/mobile-experimental/XerahS.Mobile.iOS.ShareExtension`
3. Create a focused parity matrix before coding:

```markdown
| Behavior | iOS source | Experimental source | Status | Native implementation plan |
| --- | --- | --- | --- | --- |
| About screen metadata and links | ... | ... | missing/partial/done | ... |
```

Keep rows observable. Avoid documenting unrelated implementation details.

## Default parity checklist

- App identity: bundle/application IDs, URL scheme, app group, display name, version/build, logo.
- Launch/loading flow and top-level navigation.
- Share receive/open-in-place for one and multiple files.
- Accepted input types: images, video, audio, PDFs, text, arbitrary files, URLs, `.sxcu`, `.xsdc`.
- Cache copies preserve useful file names and extensions and avoid overwrites.
- `.sxcu` import updates or adds custom uploaders and navigates/statuses like iOS.
- `.xsdc` import supports encrypted S3 destination configs or clearly preserves the pending passphrase flow.
- Remote `.sxcu` deep link: `xerahs://import-sxcu?url=...`.
- Upload queue, progress, result URL/error display, copy actions, and persisted history.
- S3 options: region, endpoint, path-style/custom domain, signed payload, public ACL.
- Custom uploader options: method, headers, parameters, body, arguments, URL/deletion/thumbnail/error extraction.
- HEIC/HEIF conversion with native platform APIs or documented unsupported behavior.
- Settings validation and list/detail navigation.
- About screen metadata, links, social links, and logo.
- Native feel: Android Material-like touch sizes/system bars; iOS Cupertino-like typography, spacing, tint, navigation behavior.

## Implementation rules

- Prefer shared C# behavior in `XerahS.Mobile.Core` only when both Avalonia and MAUI need the same domain logic.
- Keep UI in each app's native UI layer:
  - Avalonia views in `XerahS.Mobile.Ava/Views`, view models in `XerahS.Mobile.Ava/ViewModels`, platform APIs in `Platforms/*`.
  - MAUI pages in `XerahS.Mobile.Maui/Views`, view models in `XerahS.Mobile.Maui/ViewModels`, platform APIs in `Platforms/*`.
- Do not put Avalonia or MAUI types into `XerahS.Mobile.Core`.
- For Avalonia resources, use `avares://XerahS.Mobile.Ava/...` and ensure assets are `AvaloniaResource`.
- For Avalonia URL opening, prefer `TopLevel.GetTopLevel(control)?.Launcher.LaunchUriAsync(uri)` from view code-behind when the launcher is inherently UI/platform-bound.
- For Android share/open flows, avoid shell-only `file://` assumptions; real parity needs `content://` handling from Files/share sheet.
- For app identity, match production iOS:
  - App/bundle ID: `com.xerahs.xerahs.mobile`
  - Share extension ID: `com.xerahs.xerahs.mobile.ShareExtension`
  - App group: `group.com.xerahs.xerahs`
  - URL scheme: `xerahs`

## Build workflow

For Android build/deploy details, use `.ai/skills/build-android/SKILL.md`.

Common commands from `src/mobile-experimental`:

```bash
dotnet build XerahS.Mobile.Core/XerahS.Mobile.Core.csproj
dotnet build XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj -f net11.0-android
dotnet build XerahS.Mobile.Maui/XerahS.Mobile.Maui.csproj -f net11.0-android
```

Before push, also follow root `AGENTS.md` build integrity rules. Do not disable warnings-as-errors.

## Commit workflow

Commit each completed parity slice before starting unrelated work when the tree is committable.

Good slice boundaries:

- Skill/docs for experimental parity workflow.
- App identity and platform metadata.
- About screen parity.
- `.sxcu`/`.xsdc` import behavior.
- Share/open/deep-link intent routing.
- Native-feel UI polish.

Use the root version prefix from `Directory.Build.props` and `AGENTS.md`, for example `[vX.Y.Z] [Feature] Add experimental mobile About parity`.

## QA workflow

- For Android experimental apps, use `Test Android Apps: android-emulator-qa`.
- Build/install the exact app head being tested.
- Launch by resolved package/activity, not stale old IDs.
- Derive tap coordinates from the UI tree, not screenshots.
- Verify About metadata, share/import status messages, Settings navigation, and History behavior with UI tree summaries where possible.
- If emulator validation is limited by content URI grants, local cleartext HTTP, missing iOS signing, or platform workload constraints, state the limitation exactly.

## Lessons learned

- Experimental contains both Avalonia and MAUI heads. If a change is user-facing and both heads expose the same feature, update both or explicitly document why one head is out of scope.
- App identity drifts easily in experimental projects. Always check `.csproj`, Android manifest/activity, iOS `Info.plist`, entitlements, and share extension IDs together.
- Do not claim `.sxcu`/`.xsdc` parity just because upload accepts arbitrary files. Config imports must be classified before upload queueing.
- MAUI and Avalonia can share import/domain code in `XerahS.Mobile.Core`, but each app should keep native navigation and launcher behavior in its own UI layer.
- Remove debug-only labels or deployment IDs from mobile UI before calling it native-feeling.

## Reporting

Final responses for this workflow should include:

- The iOS behavior contract used.
- Experimental heads changed: Avalonia, MAUI, shared core, platform heads.
- Build/test commands run.
- Emulator/device QA performed.
- Remaining parity gaps or platform limitations.
