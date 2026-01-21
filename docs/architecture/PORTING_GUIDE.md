# Architecture & Porting Guide

## Main Goal
Build `XerahS` by porting the ShareX backend first, then designing the Avalonia UI.
- **Priority**: Backend porting > UI design (until explicitly started).
- **No WinForms**: Do not reuse WinForms UI code. Only copy non-UI methods and data models.

## Platform Abstractions
All platform-specific functionality must be isolated behind interfaces in `XerahS.Platform.Abstractions`.
- **Forbidden**: Direct calls to `NativeMethods`, `Win32 P/Invoke`, `System.Windows.Forms`, or Windows handles in Common/Core/Backend projects.
- **Structure**:
  - `XerahS.Platform.Windows`: Concrete Windows implementation.
  - `XerahS.Platform.Linux/MacOS`: Stubs or implementations.
  - `XerahS.Platform.Abstractions`: Shared interfaces.

## Porting Logic
1. **Clean**: If a file has 0 native refs, port directly.
2. **Mixed**: If a file mixes logic and native calls:
   - Extract pure logic to `Common`/`Core`.
   - Move native code to `Platform.Windows`.
   - Replace callsites with interface calls.
