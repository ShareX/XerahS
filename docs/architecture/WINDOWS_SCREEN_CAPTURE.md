# Windows Screen Capture Architecture

This is the authoritative description of how XerahS takes screenshots on Windows. It replaces the older region capture backend documents. Their `IRegionCaptureBackend` stack was never wired into the application and has been removed on every platform.

## Layers

```text
IScreenCaptureService (XerahS.Platform.Abstractions)          port used by the app
        │
WindowsScreenCaptureServiceFactory                             single composition point
        │
WindowsModernCaptureService / WindowsScreenCaptureService      thin facades: API call → ScreenCaptureRequest
        │
Capture/Engine        ScreenCaptureEngine                      runs the backend chain, handles fallback
                      CaptureBackendPolicy                     pure: which backends, in which order
                      ScreenCaptureRequest, CaptureRectResolver
                      IScreenCaptureBackend                    port implemented by each native API
        │
Capture/Backends      DxgiDesktopDuplicationBackend            DXGI Desktop Duplication
                      WindowsGraphicsCaptureBackend            Windows.Graphics.Capture (borderless)
                      GdiScreenCaptureBackend                  GDI BitBlt into a DIB section
                      TextureRegionReader                      GPU crop and readback shared by DXGI and WGC
        │
Capture/Wgc           GraphicsCaptureInterop                   WGC and D3D11 interop shared with screen recording
```

Dependencies point inwards only. The facades know the engine and the request type. The engine knows only `IScreenCaptureBackend` and the policy. Backends know their native API and the shared helpers. Nothing below the facade references Avalonia or the UI.

## Composition

`WindowsPlatform.Initialize`, `ShareXBootstrap` and `Program` all obtain the service from `WindowsScreenCaptureServiceFactory.Create`. The factory returns `WindowsModernCaptureService` on Windows 8 and later, otherwise `WindowsScreenCaptureService` (GDI only).

## Backend chain

| Order | Backend | Available when | Notes |
|---|---|---|---|
| 1 | DXGI Desktop Duplication | Windows 8+ | Duplicates only the outputs under the request. Unrotated SDR outputs are cropped on the GPU. Rotated and HDR outputs read the whole output with rotation and tone mapping. |
| 2 | Windows.Graphics.Capture | Build 20348+ and borderless access granted | Runs only when DXGI returns nothing, for example on laptops with hybrid graphics where Desktop Duplication is unsupported. Never shows the yellow capture border. The cursor comes from `IsCursorCaptureEnabled`, so system cursors are not hidden. |
| 3 | GDI BitBlt | Always | Copies straight from a DIB section. It applies `HdrScreenshotColorCorrector` when enabled. |

`CaptureOptions.UseModernCapture = false` restricts every capture to GDI. A backend that throws or returns null hands over to the next one.

## Behaviour contract

These rules are covered by `WindowsCaptureEngineTests` and `WindowsModernCaptureServiceTests` and must hold for any change:

- Full screen captures span the union of the DXGI outputs, or the virtual screen on GDI and WGC.
- Rectangle captures on DXGI crop with `DxgiCropRectHelper`. When the outputs and the virtual screen differ in size the crop is scaled, so the whole desktop is captured and cropped.
- Rectangle captures on GDI and WGC round outwards and clamp to the virtual screen.
- The modern service captures windows as clamped rectangles. The GDI only service reads window bounds as they are.
- DXGI hides system cursors unless `ShowCursor` is true and composites the cursor afterwards. GDI hides them only when `ShowCursor` is explicitly false.
- Screen pixels are always opaque.

## Performance

Compared with the previous implementation in one class:

- Region and window captures on DXGI duplicate and read back only the monitors under the selection, not every monitor.
- Unrotated SDR outputs copy only the selected pixels off the GPU (`CopySubresourceRegion`) and write them straight into the result bitmap. No intermediate bitmap of the whole desktop and no second crop.
- DXGI cursor composition draws into an overlay the size of the cursor instead of the whole desktop. The PNG encode and decode of that overlay is gone.
- GDI captures read the DIB section directly instead of doing a PNG encode and decode round trip through `System.Drawing`.

## Screen recording

`Recording/WindowsGraphicsCaptureSource` uses the same `Capture/Wgc` interop. Each frame is copied into one staging texture that is reused until the frame size changes, then mapped and passed to the encoder with its real row pitch. Frame handlers are synchronous and must finish with the pointer before they return.

## Verifying on Linux

`XerahS.Platform.Windows` skips compilation on Linux by default. To compile it, and the test project for the Windows target, run:

```bash
dotnet restore tests/XerahS.Tests/XerahS.Tests.csproj -p:OS=Windows_NT -p:EnableWindowsTargeting=true -p:CsWinRTGenerateProjection=false
dotnet build --no-restore tests/XerahS.Tests/XerahS.Tests.csproj -p:OS=Windows_NT -p:EnableWindowsTargeting=true -p:CsWinRTGenerateProjection=false
```

This proves the code compiles. Capture behaviour still has to be checked on Windows.
