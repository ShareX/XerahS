# FFmpeg in XerahS

XerahS uses FFmpeg as a recording backend across all supported platforms. The role FFmpeg plays differs per OS — on some platforms it is the primary recorder, on others it is a fallback behind a native API.

---

## Windows

### Installation

XerahS can download FFmpeg automatically from **Workflows > Edit workflow > Video Settings > FFmpeg Tools > Download FFmpeg...**. It downloads the current **FFmpeg 9.0 stable-branch static GPL build** from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), verifies its SHA-256 checksum, and installs `ffmpeg`, `ffprobe`, and the license into the app's `Tools` folder. Windows x64 and ARM64 are supported; BtbN does not publish 32-bit Windows auto-builds.

To use your own binary, open **Workflows > Edit workflow > Video Settings > FFmpeg Tools > Configure FFmpeg...**, enable **Override FFmpeg executable path**, and choose the binary there.

### Capture devices

On Windows, XerahS uses the following FFmpeg input devices:

| Device | Flag | Notes |
|---|---|---|
| GDI grab | `-f gdigrab` | Default fallback. Works on all Windows versions. Lower performance. |
| Desktop Duplication API | `-f ddagrab` | Hardware-accelerated. Requires Windows 8+. |
| DirectShow (screen-capture-recorder) | `-f dshow -i video="screen-capture-recorder"` | Third-party virtual device. |
| DirectShow (virtual-audio-capturer) | `-f dshow -i audio="virtual-audio-capturer"` | Third-party virtual audio device. |

The primary recording path on Windows uses **Windows.Graphics.Capture** (native API). FFmpeg with `gdigrab` is the fallback for systems that do not support it.

### Audio

System audio is captured via DirectShow (`-f dshow`) using **Stereo Mix**. This requires Stereo Mix to be enabled in Windows Sound settings (right-click the speaker icon > Sounds > Recording tab > Show Disabled Devices).

Microphone capture also uses DirectShow with the selected device ID.

### Example command (Windows fallback)

```
ffmpeg -f gdigrab -framerate 30 -draw_mouse 1 -i desktop -f dshow -i audio="Stereo Mix" -map 0:v -map 1:a -c:v libx264 -preset ultrafast -b:v 5000k -c:a aac -b:a 192k -pix_fmt yuv420p -y output.mp4
```

For a region:

```
ffmpeg -f gdigrab -framerate 30 -draw_mouse 1 -offset_x 100 -offset_y 50 -video_size 1280x720 -i desktop -c:v libx264 -preset ultrafast -b:v 5000k -pix_fmt yuv420p -y output.mp4
```

---

## macOS

### Installation

FFmpeg is not bundled. Install it via Homebrew:

```sh
brew install ffmpeg
```

XerahS searches `PATH` and common locations automatically. You can also set a custom path from **Workflows > Edit workflow > Video Settings > FFmpeg Tools > Configure FFmpeg...** by enabling **Override FFmpeg executable path**.

### Recording backends

XerahS prefers the native **ScreenCaptureKit** (AVAssetWriter) backend on macOS 12.3+. FFmpeg is used as a fallback when ScreenCaptureKit is unavailable.

| Backend | Condition |
|---|---|
| Native ScreenCaptureKit (primary) | macOS 12.3+ |
| FFmpeg `avfoundation` (fallback) | Older macOS or when native backend fails |

### Capture device

The `avfoundation` input device is used:

```
-f avfoundation -framerate 30 -i "1"
```

The input index `"1"` refers to the main display. Audio uses `-f avfoundation -i ":0"` for the default audio device.

Region capture is implemented as a crop filter applied post-capture, since `avfoundation` does not natively support offset/size arguments:

```
-vf "crop=1280:720:100:50"
```

### Example command (macOS fallback)

```sh
ffmpeg -f avfoundation -framerate 30 -capture_cursor 1 -i "1" -f avfoundation -i ":0" -map 0:v -map 1:a -c:v libx264 -preset ultrafast -b:v 5000k -c:a aac -b:a 192k -pix_fmt yuv420p -y output.mp4
```

---

## Linux

### Installation

The same **Download FFmpeg...** action installs BtbN’s FFmpeg 9.0 stable-branch static GPL build for Linux x64 or ARM64 into your user tools folder. TAR/XZ extraction is built in; no package manager or administrator access is required. Both `ffmpeg` and `ffprobe` are checksum-verified as one archive and installed together. See [the distribution contract](ffmpeg-release-handover.md) for asset names and verification details. Existing system packages and custom executable paths are still supported.

Wayland recording support is backend-dependent. XerahS currently tries these Linux paths in order:

| Session / condition | Backend |
|---|---|
| wlroots compositor with `wf-recorder` installed | `wf-recorder` |
| Wayland portal session and FFmpeg build that exposes a `pipewire` input | XDG ScreenCast portal + FFmpeg |
| Wayland portal session and GStreamer with `pipewiresrc` installed | XDG ScreenCast portal + GStreamer |
| X11 session | FFmpeg `x11grab` |

If you want to use the FFmpeg Wayland path specifically, verify the actual binary first:

```sh
ffmpeg -devices 2>&1 | grep pipewire
```

If a `pipewire` line appears in the output, your FFmpeg binary exposes that input. If not, do not assume a newer distro release will add it automatically. XerahS can still work through GStreamer on Wayland, or through `wf-recorder` on wlroots compositors.

**Fedora / RHEL (RPM Fusion)**

RPM Fusion's build includes PipeWire. Enable it first if you haven't:

```sh
sudo dnf install https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm
sudo dnf install ffmpeg
```

**Ubuntu / Debian**

The default `ffmpeg` package does **not** provide a reliable PipeWire guarantee across current Ubuntu and Debian releases. Install it if you need FFmpeg generally, but always verify the actual binary:

```sh
sudo apt update
sudo apt install ffmpeg
```

After installing, verify PipeWire support again:

```sh
ffmpeg -devices 2>&1 | grep pipewire
```

- If you now see a `pipewire` device, you can use the FFmpeg Wayland path.
- If you still do **not** see `pipewire`, use one of these supported alternatives instead:
  - Install GStreamer PipeWire support and let XerahS use the portal + `pipewiresrc` fallback.
  - On wlroots compositors, install `wf-recorder`.
  - Point XerahS at a custom FFmpeg binary that actually exposes the `pipewire` input device.

On Ubuntu, if you are only missing some codecs (e.g. H.264/MP3 playback) rather than PipeWire itself, you can optionally install:

```sh
sudo apt install ubuntu-restricted-extras
```

**Arch Linux**

Package contents change over time, so verify the actual binary instead of assuming support from the repo name alone:

```sh
sudo pacman -S ffmpeg
```

**NixOS**

Use `ffmpeg-full`, then verify the binary:

```nix
environment.systemPackages = [ pkgs.ffmpeg-full ];
```

**Static builds**

Do not assume a static FFmpeg build will provide Wayland portal capture support. Verify the binary with `ffmpeg -devices` before using it in XerahS.

GStreamer with PipeWire plugins is used as a fallback if FFmpeg lacks PipeWire support:

```sh
# GStreamer PipeWire plugins (fallback)
sudo apt install gstreamer1.0-pipewire          # Debian/Ubuntu
sudo dnf install gstreamer1-plugin-pipewire     # Fedora
sudo pacman -S gst-plugin-pipewire              # Arch
```

### How screen recording works on Linux

When XerahS uses a portal-backed Wayland recorder, it talks to the **XDG ScreenCast portal** (`org.freedesktop.portal.ScreenCast`) to obtain a PipeWire stream from the compositor.

The flow is fully automatic:

1. XerahS opens a D-Bus session with the portal.
2. The compositor displays its own native source picker — the user selects a monitor or window.
3. The portal returns a PipeWire node ID for the selected source.
4. XerahS passes that node ID to the selected recorder integration.

**The user never needs to know or configure a PipeWire node ID.** It is resolved automatically per recording session.

### Recording backend priority (Wayland)

| Priority | Backend | Condition |
|---|---|---|
| 1 | `wf-recorder` | wlroots compositor detected and wf-recorder is installed |
| 2 | XDG Portal + FFmpeg (`pipewire`) | FFmpeg has PipeWire input support |
| 3 | XDG Portal + GStreamer (`pipewiresrc`) | GStreamer PipeWire plugins installed |

On X11, the fallback is `x11grab`.

### Verify your setup

Run the built-in diagnostic from the app: **Workflows > Edit workflow > Video Settings > Linux Recording Diagnostics**. It reports:

- Whether FFmpeg has PipeWire input
- Whether GStreamer has PipeWire plugins
- The recommended backend for your session type
- Any missing dependencies with install suggestions

### Audio on Linux

System audio is captured from the PulseAudio monitor source of the default output device. XerahS resolves this automatically using `pactl`. Microphone capture uses the selected device ID from settings.

Both are passed to FFmpeg as a second input:

```
-f pulse -i alsa_output.pci-0000_00_1f.3.analog-stereo.monitor
```

### Example command (Wayland, FFmpeg + PipeWire)

Use this only when your FFmpeg build actually lists a `pipewire` input device:

```sh
ffmpeg \
  -f pipewire -framerate 30 -i <node_id> \
  -f pulse -i alsa_output.pci-0000_00_1f.3.analog-stereo.monitor \
  -map 0:v -map 1:a \
  -c:v libx264 -preset ultrafast -b:v 5000k \
  -c:a aac -b:a 192k \
  -pix_fmt yuv420p \
  -y output.mp4
```

With region crop:

```sh
ffmpeg \
  -f pipewire -framerate 30 -i <node_id> \
  -vf "crop=1280:720:0:0" \
  -c:v libx264 -preset ultrafast -b:v 5000k \
  -pix_fmt yuv420p \
  -y output.mp4
```

`<node_id>` is the integer PipeWire node provided by the portal — XerahS fills this in automatically.

### Example command (X11 fallback)

```sh
ffmpeg -f x11grab -framerate 30 -draw_mouse 1 -i :0.0 -c:v libx264 -preset ultrafast -b:v 5000k -pix_fmt yuv420p -y output.mp4
```

For a specific region on X11:

```sh
ffmpeg -f x11grab -framerate 30 -draw_mouse 1 -video_size 1280x720 -i :0.0+100,50 -c:v libx264 -preset ultrafast -b:v 5000k -pix_fmt yuv420p -y output.mp4
```

---

## Supported codecs (all platforms)

| Codec | FFmpeg encoder | Notes |
|---|---|---|
| H.264 | `libx264` | Default. Best compatibility. |
| H.265 / HEVC | `libx265` | Better compression, slower encode. |
| VP9 | `libvpx-vp9` | Open, good for WebM. |
| AV1 | `libaom-av1` | Best compression, CPU-intensive. |

All codecs use `-pix_fmt yuv420p` for broad player compatibility.

---

## Custom FFmpeg path

If XerahS cannot find FFmpeg automatically, open **Workflows > Edit workflow > Video Settings > FFmpeg Tools > Configure FFmpeg...**, enable **Override FFmpeg executable path**, and set the binary path there. The app checks (in order):

1. Explicitly configured path
2. `Options.CLIPath` if override is enabled
3. `PATH` environment variable and common install locations (`Tools/`, `Program Files/FFmpeg/bin/`, etc.)
