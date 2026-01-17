# macOS Publish Guide (ShareX.Avalonia)

This guide documents the steps to publish ShareX.Avalonia for macOS, including Intel (`osx-x64`) and Apple Silicon (`osx-arm64`), and the bundling steps needed for a working `.app` with a proper icon.

## Prerequisites

- macOS machine with .NET SDK installed
- `Logo.png` and `Logo.icns` available at `src/ShareX.Avalonia.UI/Assets/`

## Generate the macOS icon

macOS app bundles use `.icns` files (not `.ico`). Create the iconset and generate the `.icns` once:

```bash
mkdir -p "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset"

sips -z 16 16   "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_16x16.png"
sips -z 32 32   "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_16x16@2x.png"
sips -z 32 32   "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_32x32.png"
sips -z 64 64   "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_32x32@2x.png"
sips -z 128 128 "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_128x128.png"
sips -z 256 256 "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_128x128@2x.png"
sips -z 256 256 "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_256x256.png"
sips -z 512 512 "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_256x256@2x.png"
sips -z 512 512 "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_512x512.png"
sips -z 1024 1024 "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.png" --out "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset/icon_512x512@2x.png"

iconutil -c icns "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/ShareX.iconset" -o "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.UI/Assets/Logo.icns"
```

## Publish for Intel and Apple Silicon

Intel (x64):

```bash
dotnet publish -c Release -r osx-x64 --self-contained true "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/ShareX.Avalonia.App.csproj"
```

Apple Silicon (arm64):

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/ShareX.Avalonia.App.csproj"
```

Outputs land at:

- `src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/`
- `src/ShareX.Avalonia.App/bin/Release/net10.0/osx-arm64/publish/`

## Verify the `.app` bundle

Check the bundle exists:

```bash
find "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish" -maxdepth 2 -name "*.app"
```

Check the bundle executable:

```bash
ls -la "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app/Contents/MacOS/ShareX.Avalonia.App"
file "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app/Contents/MacOS/ShareX.Avalonia.App"
```

Check the icon files and plist keys:

```bash
ls -la "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app/Contents/Resources/Logo.icns"
/usr/libexec/PlistBuddy -c "Print :CFBundleIconFile" "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app/Contents/Info.plist"
```

## Gatekeeper troubleshooting

If macOS reports the app is damaged or incomplete:

```bash
xattr -dr com.apple.quarantine "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app"
```

Optional ad-hoc signing (helps local launches):

```bash
codesign --force --deep --sign - "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app"
```

## Create a DMG

Step 1: stage the `.app` in a clean folder:

```bash
rm -rf "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/dist"
mkdir -p "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/dist"
cp -R "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/src/ShareX.Avalonia.App/bin/Release/net10.0/osx-x64/publish/ShareX.Avalonia.App.app" "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/dist/"
```

Step 2 (Intel):

```bash
hdiutil create -volname "ShareX.Avalonia" \
  -srcfolder "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/dist" \
  -ov -format UDZO \
  "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/ShareX.Avalonia-osx-x64.dmg"
```

Step 2 (Apple Silicon):

```bash
hdiutil create -volname "ShareX.Avalonia" \
  -srcfolder "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/dist" \
  -ov -format UDZO \
  "/Users/mike/Projects/ShareX Team/ShareX.Avalonia/ShareX.Avalonia-osx-arm64.dmg"
```

## Why the csproj changes were needed

The publish output did not create a `.app` bundle by default, and `ApplicationIcon` only supports Windows `.ico` embedding. The `ShareX.Avalonia.App.csproj` was updated to:

- Keep `ApplicationIcon` pointing to `.ico` for Windows builds.
- Create a minimal `.app` bundle during `Publish` for macOS (so Finder can launch it).
- Copy the published files into `Contents/MacOS`.
- Copy `Logo.icns` into `Contents/Resources` and set `CFBundleIconFile`/`CFBundleIconName` in `Info.plist`.

These changes live in `src/ShareX.Avalonia.App/ShareX.Avalonia.App.csproj`.
