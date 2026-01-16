# SIP0017 Quick Integration Guide
## 5-Minute Setup for Native Screen Recording

This guide provides the minimal steps needed to integrate the Stage 1 implementation into ShareX.Avalonia.

---

## 1. Add NuGet Package (30 seconds)

```bash
cd "c:\Users\liveu\source\repos\ShareX Team\ShareX.Avalonia\src\ShareX.Avalonia.Platform.Windows"
dotnet add package Microsoft.Windows.SDK.Contracts --version 10.0.22621.48
```

---

## 2. Add Project Reference (30 seconds)

Edit `src\ShareX.Avalonia.Platform.Windows\ShareX.Avalonia.Platform.Windows.csproj`:

```xml
<ItemGroup>
  <!-- Add this line -->
  <ProjectReference Include="..\ShareX.Avalonia.ScreenCapture\ShareX.Avalonia.ScreenCapture.csproj" />
</ItemGroup>
```

---

## 3. Extend TaskSettingsCapture (1 minute)

Edit `src\ShareX.Avalonia.Core\Models\TaskSettings.cs`:

Find the `TaskSettingsCapture` class (around line 176), locate this section:
```csharp
public RegionCaptureOptions RegionCaptureOptions = new RegionCaptureOptions();
public FFmpegOptions FFmpegOptions = new FFmpegOptions();
public ScrollingCaptureOptions ScrollingCaptureOptions = new ScrollingCaptureOptions();
```

Add this line after `FFmpegOptions`:
```csharp
public XerahS.ScreenCapture.Recording.ScreenRecordingSettings NativeRecordingSettings = new();
```

Final result:
```csharp
public RegionCaptureOptions RegionCaptureOptions = new RegionCaptureOptions();
public FFmpegOptions FFmpegOptions = new FFmpegOptions();
public XerahS.ScreenCapture.Recording.ScreenRecordingSettings NativeRecordingSettings = new();
public ScrollingCaptureOptions ScrollingCaptureOptions = new ScrollingCaptureOptions();
```

---

## 4. Initialize Recording (2 minutes)

### Option A: Add to WindowsPlatform.cs

Edit `src\ShareX.Avalonia.Platform.Windows\WindowsPlatform.cs`:

Add using statements at the top:
```csharp
using XerahS.Platform.Windows.Recording;
using XerahS.ScreenCapture.Recording;
```

Add this method to the `WindowsPlatform` class:
```csharp
public static void InitializeRecording()
{
    // Check if native recording is supported
    if (WindowsGraphicsCaptureSource.IsSupported && MediaFoundationEncoder.IsAvailable)
    {
        ScreenRecorderService.CaptureSourceFactory = () => new WindowsGraphicsCaptureSource();
        ScreenRecorderService.EncoderFactory = () => new MediaFoundationEncoder();
    }
    // else: FFmpeg fallback will be implemented in Stage 4
}
```

Edit `src\ShareX.Avalonia.App\Program.cs`, find the platform initialization and add call:
```csharp
WindowsPlatform.Initialize(screenService, uiCaptureService, ...);
WindowsPlatform.InitializeRecording(); // Add this line
```

---

## 5. Test Build (1 minute)

```bash
cd "c:\Users\liveu\source\repos\ShareX Team\ShareX.Avalonia"
dotnet build
```

Expected: `Build succeeded`

---

## 6. Test Recording (Optional - 2 minutes)

Add temporary test code to any ViewModel or window code-behind:

```csharp
using XerahS.ScreenCapture.Recording;

private async void TestRecording()
{
    var recorder = new ScreenRecorderService();

    recorder.StatusChanged += (s, e) =>
    {
        Console.WriteLine($"Status: {e.Status}, Duration: {e.Duration}");
    };

    recorder.ErrorOccurred += (s, e) =>
    {
        Console.WriteLine($"Error: {e.Error.Message}, Fatal: {e.IsFatal}");
    };

    var options = new RecordingOptions
    {
        Mode = CaptureMode.Screen,
        Settings = new ScreenRecordingSettings
        {
            FPS = 30,
            BitrateKbps = 4000,
            Codec = VideoCodec.H264
        }
    };

    try
    {
        Console.WriteLine("Starting recording...");
        await recorder.StartRecordingAsync(options);

        Console.WriteLine("Recording for 5 seconds...");
        await Task.Delay(5000);

        Console.WriteLine("Stopping recording...");
        await recorder.StopRecordingAsync();

        Console.WriteLine("Recording saved!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Recording failed: {ex.Message}");
    }
}
```

Run the app and call `TestRecording()`. Check `Documents\ShareX\Screenshots\{yyyy-MM}\` for the MP4 file.

---

## Complete! 🎉

You now have native screen recording integrated. The video will be saved to:
```
C:\Users\{username}\Documents\ShareX\Screenshots\{yyyy-MM}\{yyyy-MM-dd_HH-mm-ss}.mp4
```

---

## Next Steps

1. **Wire UI commands** - Connect Start/Stop recording to your UI buttons/hotkeys
2. **Add settings UI** - Expose FPS, bitrate controls in TaskSettingsViewModel
3. **Test on different systems** - Windows 10 1803+, Windows 11, various GPUs
4. **Implement FFmpeg fallback** - Stage 4 for systems without WGC/MF support

---

## Troubleshooting

**Build Error: "Cannot find type WindowsGraphicsCaptureSource"**
- Solution: Add project reference from Platform.Windows to ScreenCapture

**Runtime Error: "PlatformNotSupportedException"**
- Check Windows version: must be 10.0.17134 (1803) or later
- Run: `winver` to verify Windows version

**Runtime Error: "COMException when creating sink writer"**
- Media Foundation not available or codec missing
- Check: Rename `c:\windows\system32\mfplat.dll` to test fallback behavior

**Video file not created**
- Check: `Documents\ShareX\Screenshots\{current-month}` directory exists
- Check: No permission errors in output directory

---

## Support

For issues, refer to:
- **Implementation Summary:** `SIP0017_Implementation_Summary.md`
- **Original SIP:** `SIP0017_Screen_Recording_Modernization.md`
- **ShareX Team:** Report issues via GitHub

---

**Total Integration Time:** ~5 minutes
**Difficulty:** Easy
**Risk:** Low
