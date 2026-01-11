# Native ScreenCaptureKit Bridge for ShareX.Avalonia

This directory contains the native macOS library used by ShareX.Avalonia to interface with the `ScreenCaptureKit` framework. This allows for high-performance screen recording and capture on macOS 12.3+.

## Contents

- `screencapturekit_bridge.h`: C-compatible header defining the exported API.
- `screencapturekit_bridge.m`: Objective-C implementation using ARC.
- `Makefile`: Build script for compiling the dynamic library.

## Requirements

- **Operating System**: macOS 12.3 (Monterey) or later.
- **Tools**: Xcode Command Line Tools (install via `xcode-select --install`).
- **Compiler**: Clang (included with Xcode tools).

## Building

To build the shared library (`libscreencapturekit_bridge.dylib`):

```bash
make
```

This will produce `libscreencapturekit_bridge.dylib` in the current directory.

### Development Build

If you are developing primarily on macOS and want to copy the built library directly to the ShareX.Avalonia build output directories (Debug/Release):

```bash
make dev
```

### Installation (System-wide)

To install the library to `/usr/local/lib`:

```bash
sudo make install
```

## Usage in C#

The library functions are exposed via `[DllImport]` in `ShareX.Avalonia.Platform.MacOS`:

```csharp
[DllImport("libscreencapturekit_bridge.dylib")]
private static extern int sck_capture_fullscreen(out IntPtr data, out int length);
```

Ensure the `.dylib` is present in the application's execution directory or in a standard library path.

## Architecture

The bridge exposes a simplified C API:
- `sck_is_available()`: Checks for API availability.
- `sck_capture_fullscreen()`: Captures the primary display.
- `sck_capture_rect()`: Captures a specific screen region.

Memory is managed manually:
- Capture functions allocate a buffer for the PNG image data.
- The caller **must** free this buffer using `sck_free_buffer()`.
