---
name: ShareX Architecture and Porting
description: Platform abstraction rules, porting guidelines, and architecture standards for XerahS
---

## Platform Abstractions and Native Code Rules

All platform specific functionality must be isolated behind platform abstraction interfaces.

No code outside XerahS.Platform.* projects may reference:

- NativeMethods
- NativeConstants, NativeEnums, NativeStructs
- Win32 P/Invoke
- System.Windows.Forms
- Windows specific handles or messages

Direct calls to Windows APIs are forbidden in Common, Core, Uploaders, Media, or other backend projects.

### Required Architecture

Define platform neutral interfaces in XerahS.Platform.Abstractions.

Implement Windows functionality in XerahS.Platform.Windows.

Create stub implementations for future platforms:
- XerahS.Platform.Linux
- XerahS.Platform.MacOS

### Windows-Only Features

If a capability is Windows only:

- It must still be defined via an abstraction interface.
- Windows provides the concrete implementation.
- Other platforms provide a stub implementation that either:
  - throws PlatformNotSupportedException, or
  - returns a safe no-op with a logged warning.

UI and workflows must detect capability availability and disable or hide unsupported features.

### Porting Rule for Existing ShareX Code

A file may only be ported directly if it contains zero references to:

- NativeMethods or related native helpers
- WinForms types
- Windows specific interop

If a file mixes logic and native calls:

- Extract pure logic into Common or Core.
- Move native code into XerahS.Platform.Windows.
- Replace callsites with interface calls.

Native method names and signatures should remain Windows specific and must not leak into shared layers.

### Enforcement

When porting ShareX.HelpersLib, files that reference:

- NativeMethods.cs
- NativeMethods_Helpers.cs
- NativeMessagingHost.cs
- DWM, hooks, clipboard, or input APIs

must be treated as platform code and cannot be copied wholesale.

## Main Goal and Porting Rules

- Main goal: build XerahS by porting the ShareX backend first, then designing the Avalonia UI.
- Examine `C:\Users\liveu\source\repos\ShareX Team\ShareX` to understand existing non-UI logic and reuse it by copying into this repo after the Avalonia solution and projects are drafted.
- Do not reuse WinForms or any WinForms UI code; only copy non-UI methods and data models.
- Keep the backend port the priority until the UI design phase is explicitly started.

## Avalonia Solution Proposal (from ShareX analysis)

- Start with the simplest backend libraries first, then move to more complex modules.
- Proposed structure:
  - `XerahS.Common`: shared helpers, serialization, utilities.
  - `XerahS.Core`: task settings, workflows, application-level services.
  - `XerahS.Uploaders`: uploaders, config, OAuth, HTTP helpers.
  - `XerahS.History`: history models and persistence.
  - `XerahS.Indexer`: file indexing and search.
  - `XerahS.ImageEffects`: filters/effects pipeline.
  - `XerahS.Media`: encoding, thumbnails, FFmpeg integration.
  - `XerahS.ScreenCapture`: capture engines and platform abstractions.
  - `XerahS.Platform.*`: OS-specific implementations (Windows first, others later).
  - `XerahS.App` and `XerahS.UI`: Avalonia UI and view models (defer until backend is ready).

## Historical Comparisons and Parity

When asked to ensure feature parity with a specific historical commit or "make it identical to commit X":

1. **Do not rely on repeated git calls**: Avoid repeatedly querying git history for file contents during the session.
2. **Create a `ref` directory**: Create a temporary folder (e.g., `src/XerahS/ref`).
3. **Download reference files**: Use `git show <commit_hash>:<file_path> > src/XerahS/ref/<commit_short>_<filename>` to verify the state of relevant files at that commit.
4. **Compare locally**: Perform diffs and analysis between the local current files and the downloaded reference files.
5. **Clean up**: Remove the `ref` directory once the task is complete and verified, unless instructed otherwise.

This reduces git command overhead and provides a stable reference point for parity checks.
