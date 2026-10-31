# FFmpeg distribution and maintenance

ShareX and XerahS use [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) for prebuilt FFmpeg binaries. The former `ShareX/FFmpeg` release bucket is retired. XerahS no longer depends on its release tags, packaging workflows, signing keys, or checksum signatures.

This document replaces the former ShareX FFmpeg release-publishing handover. Recording configuration and backend selection are documented in [FFMPEG.md](FFMPEG.md).

## Selected builds

XerahS selects **static GPL builds from the FFmpeg 9.0 stable release branch**, including current fixes in that branch. It uses BtbN's floating `latest` release and matches the entire asset name. It does not select master snapshots, shared-library packages, LGPL variants, or another CPU architecture when the expected package is missing.

| Platform | BtbN asset |
|---|---|
| Windows x64 | `ffmpeg-n9.0-latest-win64-gpl-9.0.zip` |
| Windows ARM64 | `ffmpeg-n9.0-latest-winarm64-gpl-9.0.zip` |
| Linux x64 | `ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz` |
| Linux ARM64 | `ffmpeg-n9.0-latest-linuxarm64-gpl-9.0.tar.xz` |

BtbN does not publish macOS or 32-bit Windows auto-builds. The downloader explains how to install a compatible binary manually; macOS users can use `brew install ffmpeg`. Custom paths and existing installations remain supported.

The source of truth for the selected branch is `FFmpegUpdateChecker.ReleaseBranch`. When adopting another stable branch, update that constant, asset-selection tests, and this table together. Check [BtbN's release assets](https://github.com/BtbN/FFmpeg-Builds/releases) before changing it.

## Download and installation contract

`FFmpegDownloader` uses one path for Windows and Linux:

1. Resolve the exact archive and `checksums.sha256` from the same BtbN release metadata.
2. Download the checksum manifest over HTTPS. Require one unambiguous SHA-256 entry for the exact archive filename.
3. Download into a unique temporary folder and verify the archive hash before extraction.
4. Extract only regular `bin/ffmpeg`, `bin/ffprobe` (with `.exe` on Windows), and `LICENSE.txt` from one package root. Reject missing, empty, duplicate, or linked tool entries. Other archive paths are never written to disk.
5. Stage the complete pair and license before replacing installed files. Rename on the destination filesystem, restore previous tools if replacement fails, and preserve recovery files if restoration itself fails.
6. Set executable permissions on Unix and remove temporary files. Return the binary actually installed in the requested folder, rather than a different FFmpeg found on `PATH`.

ZIP extraction uses .NET; Linux TAR/XZ extraction uses .NET's TAR reader with SharpCompress's managed XZ decoder. No `tar`, `xz`, package-manager command, native extraction library, or administrator access is needed.

The manifest supplies integrity verification over HTTPS. It is **not** the former ShareX ECDSA signature scheme: XerahS no longer requests `SHA256SUMS.sig` or carries that scheme's public key. A missing checksum, mismatched hash, cancellation, or incomplete archive never replaces existing tools.

Local tool directories retain their existing names (`Tools/win-x64`, `Tools/linux-x64`, `Tools/linux-arm64`, etc.). BtbN's different asset naming does not move user installations or change plugin directories. Applications that already have FFmpeg do not automatically replace it merely because the download provider changed.

Windows executables are used as distributed by BtbN. XerahS does not rewrite their embedded manifests or claim a ShareX Authenticode signature. The package's GPL license is installed alongside the tool pair.

## Recording implications and verification

Static GPL packages retain the H.264/H.265 encoder choices. Downloading a newer FFmpeg does not imply PipeWire capture support. Keep detecting actual input devices and using the existing GStreamer or `wf-recorder` paths on Wayland where appropriate.

Regression tests cover exact asset selection for all four targets, unsupported CPUs, checksum failure, nested ZIP and TAR/XZ layouts, missing/duplicate/linked tools, cancellation, and rollback. The explicit network test `FFmpegLinuxDownloadTests.DownloadsVerifiedToolsAndEncodesPlayableVideo` downloads the actual selected package into a temporary folder, checks its version, encodes a synthetic H.264 clip, and probes the result with the matching `ffprobe`.
