# Changelog

All notable changes to ShareX.Avalonia (XerahS) will be documented in this file.

The format follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html):
- **MAJOR** (x): Breaking changes (0 while unreleased)
- **MINOR** (y): New features and enhancements
- **PATCH** (z): Bug fixes and patches

## Unreleased

- Documentation updates: macOS/Linux/Editor extraction/Integration (`5e1184e`, `6394dc7`, `17a319e`, `2e7e29c`, `090d9e9`, `cabd8c3`, `fc4851f`)

## v0.3.1 - Bug Fixes

- 0.3.1 - `df9bd33` - Fix NullReferenceException in CaptureFullScreenDxgi by preventing premature disposal of ID3D11DeviceContext

## v0.3.0 - Modern Capture Architecture (SIP0016)

### Features & Improvements
- 0.3.0 - Implemented Unified Screen Recording pipeline with native Windows/MF support, FFmpeg fallback, and audio options (`9224b62`, `7a6e47b`, `8fc451c`, `a223bd0`, `642133d`, `1e2ef2c`, `d4409f9`, `d6773c0`, `bdeef06`, `674f350`, `b0e623e`)
- 0.3.0 - Modernized Region Capture backend with cross-platform and DXGI support (`97ba056`, `9cc0209`, `3bdaaf0`, `21ec1e2`, `aa417f6`, `4ee0c05`)
- 0.3.0 - Enhanced Workflow and Hotkey system: ID persistence, state machine, reordering, and wizard UI (`faebe87`, `927b819`, `dd1f36e`, `d904b17`, `1b29c6d`, `4c8c0df`, `64f2b42`, `0a446d9`, `9d84dd3`, `7b11ec1`, `132b0fc`, `aab8396`, `8b2477d`, `3958351`, `7d2b6ee`, `fd395c3`)
- 0.3.0 - Refactored Destination and Uploader persistence to be Job-Type aware and use TaskSettings logic (`3388271`, `e7b1603`, `979dc0d`, `67a54ee`, `8d1dbc2`, `edc5a06`)
- 0.3.0 - New Toast Notification system with custom UI, advanced settings, and platform integration (`81ccf18`, `f1d9b88`, `6229154`, `91106fb`, `919f31a`, `ad4be91`, `b3c2df6`)
- 0.3.0 - Settings UI improvements: Weekly Backups, Tray interaction, Image Effects editor, and ApplicationGeneral settings (`0a8e15f`, `5224805`, `c34faa9`, `035e8b4`, `4ddfb59`, `f4a5dc2`, `6f24b2c`, `d3cc468`, `76ca3c8`, `f46995f`, `054c9a9`)
- 0.3.0 - Cross-platform support updates: macOS ScreenCaptureKit, Linux CLI wrappers, and System Service abstraction (`de2d17b`, `c511441`, `86945ba`, `dfa4cd4`, `b637a74`, `18fd706`, `1440efc`)
- 0.3.0 - DPI scaling fixes and robust Window Activation logic (`c79e7a7`, `386aaae`, `875fd8b`, `00e8728`, `cb602a8`, `e4817b1`, `e35159c`, `48b1a2c`)
- 0.3.0 - Performance optimizations: Modern Capture (DXGI) acceleration and Task logic improvements (`25f544d`, `52ae45e`, `df5764e`, `c94ce29`, `261c643`, `8c52464`)
- 0.3.0 - Media Foundation encoder implementation with low-level COM/VTable interop (`b6bd5bb`, `559c8cb`, `fafb402`, `b039ec8`, `295104d`, `7374dc0`)
- 0.3.0 - Project documentation (SIPs, Rules, FAQ) and build system updates (`f80ad00`, `7534f23`, `cba5d60`, `a88d5f9`, `97097a3`, `e7dc315`, `52844d0`, `affb927`, `ed9d756`, `facfe0c`, `c7d48f6`, `1dc0dad`, `a66e6f9`, `9417e1d`, `739dcfe`, `a34bf22`, `af30444`, `ac09f37`, `410a14c`, `ccbd9b3`, `9a321f1`, `09d6cad`, `eecc915`, `d12e289`, `e5d8e2f`, `2f44742`, `f295bd2`, `9aa61ec`, `4e88d23`, `bfe81da`, `30f7273`, `24cbfd2`, `fd30d4f`, `779410b`, `9b24777`, `1914e72`, `b2ccfae`, `3b51a93`)

## v0.2.1 - Multi-Monitor and DPI Fixes

- 0.2.1 - `1a33aed` - Implement macOS window activation and state checks
- 0.2.1 - `876f46a` - Update version to 0.2.1 and resource version to 0.2.0
- 0.2.1 - `e47e81b` - Fix: Multi-monitor region capture offset and DPI scaling issues

## v0.2.0 - macOS Platform Support & Plugin System

### Features & Improvements
- 0.2.0 - Implemented Plugin System with packaging `.sxadp`, CLI tools, and installer (`4f4d120`, `298`, `4be59f3`, `a6acc5d`, `020ddde`, `cead71a`, `433788c`, `f81c656`, `0adc892`, `b60676f`, `0e3aab3`, `437349a`, `fc2dafc`, `72f0bc8`, `73315c4`)
- 0.2.0 - Initial macOS platform support: ScreenCaptureKit, SharpHook hotkeys, Clipboard, and App Bundle (`1a33aed`, `6fbf63e`, `439c35a`, `fae6588`, `ca05d4b`, `97957d7`, `d17e1b9`, `acba9d5`, `c9a2ee0`, `0fb4b4a`)
- 0.2.0 - Major Editor refactor to ShareX.Editor, SkiaSharp transition, and new annotation tools (Crop, Pixelate, Smart Padding) (`bfc56e4`, `d6f0490`, `ff0678c`, `0a6f41b`, `4411a17`, `86c6588`, `765ee80`, `12d9abf`, `0d76bac`, `4204e76`, `5c14abc`, `5b8cc6a`, `c3f1399`)
- 0.2.0 - History system overhaul: SQLite backend, auto-backup, and UI improvements (`0f20d76`, `22b6cf5`, `66f6589`, `5a3c2c8`, `e189ed8`, `0802010`, `697d1a2`)
- 0.2.0 - General Settings and Configuration improvements (`7c42a83`, `293886c`, `1c8014c`, `6b61f4b`, `f2859d1`)
- 0.2.0 - Documentation updates: Workflow overview, SIPs, and Developer guides (`77b70ee`, `1914e72`)

## v0.1.0 - Initial Feature Set

### Core Features
- 0.1.0 - Initial Editor implementation with Annotation Canvas, Shapes, and Gradient support (`3babd33`, `5be170d`, `5dd5263`, `8395e4d`, `dec0317`, `c2a603e`, `8888ee8`, `656a186`, `7b4790f`, `e44b2f2`, `064bb3a`)
- 0.1.0 - Region Capture features: Marching ants, detailed info label, and background dimming (`00b3a63`, `64be7b5`, `fd47b8d`, `f552e2f`, `c67d61d`, `67d0914`, `9d63c9f`)
- 0.1.0 - Hotkey System implementation with robust key capture and inline editing (`49aa435`, `861afd1`, `7357914`, `80cd222`, `236aee9`)
- 0.1.0 - Plugin architecture refactoring to pure dynamic loading (`af14844`, `a2adbf3`, `53db734`, `62561af`, `8d771ab`, `ea204fa`)

---

## Version Summary

- **v0.3.1**: Critical bug fix for DXGI capture crash
- **v0.3.0**: Modern capture architecture with DXGI/Direct3D11, screen recording (SIP0017), toast notifications, workflow system
- **v0.2.1**: Multi-monitor and DPI scaling fixes
- **v0.2.0**: macOS platform support, plugin system, SQLite history, editor extraction, XerahS rebranding
- **v0.1.0**: Initial implementation with basic capture, annotation canvas, hotkey system, and editor features

---

*This changelog follows Semantic Versioning while the project remains in pre-release (0.x.x).*
