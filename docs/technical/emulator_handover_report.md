# Android Emulator Setup Handover Report

**Date:** 2026-02-19
**Author:** Antigravity (Assistant)
**Status:** BLOCKED (System/Networking Issue)

## Objective
Configure and run an Android Emulator (AVD) for the Avalonia Android project (`XerahS`) targeting `net10.0-android`, and establish a stable ADB connection for debugging.

## Environment Details
*   **OS:** Linux (Fedora 43, Kernel 6.18.9)
*   **Android SDK:** `/home/xf/Android/Sdk`
*   **AVD Path:** `/home/xf/Android_Config_2/.android/avd`
*   **Project Path:** `/home/Public/GitHub/ShareXteam/XerahS`
*   **Target Framework:** `net10.0-android` (upgraded from `net8.0-android`)

## Summary of Challenges

### 1. Project Configuration & SDK Mismatch
*   **Issue:** Initial project targeted `net8.0-android`, but only API 34+ (Android 14) SDK components were installed.
*   **Resolution:** Modified `.csproj` to target `net10.0-android`.
*   **Issue:** Missing `AndroidManifest.xml` caused build failures.
*   **Resolution:** Generated a minimal valid `AndroidManifest.xml`.

### 2. AVD Configuration & Launch Failures
*   **Issue:** `Pixel_Default` AVD failed to launch due to "Multiple emulators running" error (stale `.lock` files).
*   **Attempt:** Cleared `*.lock` files in `/home/xf/Android_Config_2/.android/avd/Pixel_Default.avd/`.
*   **Issue:** `Pixel_Default` continuously failed with graphics backend errors (`lavapipe` hang).
*   **Attempt:** Tried launching with `-gpu guest` (software rendering inside VM). Result: Extremely slow/unresponsive.
*   **Attempt:** Tried `-gpu swiftshader_indirect` (host software rendering). Result: Better, but encountered networking delays.
*   **Attempt:** Tried `-gpu off` (headless). Result: Successfully entered boot loop but failed network bind.
*   **Issue:** `Pixel_Manual` (user-created) targeted non-existent `android-36` image and had invalid skin name `pixel`.
*   **Resolution:** Updated `config.ini` to target `android-34` and use `skin.name=1080x1920`.

### 3. Networking & ADB Connectivity (CURRENT BLOCKER)
This was the most persistent and critical failure point.

*   **Observed Behavior:**
    *   Emulator launches successfully (PID exists, `qemu-system-x86_64` running).
    *   **Failure:** `adb devices` returns an empty list.
    *   **Failure:** Emulator binds to dynamic IPv6 ports (e.g., `36687`, `44023`) instead of standard IPv4 `5554`/`5555`.
    *   **Failure:** Manual `adb connect [::1]:<port>` fails silently or times out.

*   **Attempted Fixes:**
    1.  **Forced Ports:** Launched with `-ports 5554,5555`.
        *   *Result:* Emulator sometimes ignored this or crashed during bind.
    2.  **ipv6-only Localhost:** Tried connecting via `adb connect localhost:<port>`.
        *   *Result:* Connection refused.
    3.  **Python ADB Bridge:**
        *   Wrote `adb_bridge.py` to forward local IPv4 `5555`/`5566` to emulator's IPv6 port.
        *   *Result:* Bridge successfully listened on IPv4, but connection to IPv6 backend (`[::1]:36687`) was **REFUSED** by the OS/Kernel.
    4.  **ADB Server Reset:**
        *   Killed and restarted ADB server in `nodaemon` mode bound to IPv4.
        *   *Result:* ADB server process became unresponsive or failed to see device.

### 4. System Unresponsiveness (CRITICAL)
*   **Issue:** During network diagnostics, standard system commands began hanging indefinitely.
    *   `ss -tlpn` (Listing ports) -> **HANG**
    *   `netstat` -> **HANG**
    *   `ip addr show lo` -> **HANG** (Suggests kernel-level locking on network interface)
    *   `nft list ruleset` -> **HANG**
*   **Implication:** identifying the root cause became impossible as the diagnostic tools themselves ceased to function. The shell became unresponsive to `SIGINT` (Ctrl+C).

## Root Cause Analysis (Hypothesis)
1.  **IPv6/IPv4 Stack Conflict:** The emulator is heavily preferring IPv6 loopback (`::1`), while the ADB server and host tools are expecting IPv4 (`127.0.0.1`). The bridge attempt failed, suggesting a firewall or kernel restriction on `lo` traffic.
2.  **Kernel/Driver Lockup:** The fact that `ip addr show` and `ss` commands hang suggests a deadlock in the kernel's networking subsystem, likely triggered by the emulator's network bridge (`virtio-net`) or the `lavapipe` graphics driver interacting with the system.

## Recommendations for Next Session
1.  **System Reboot (MANDATORY):** The current session is in a zombie state with hung network calls. A reboot is required to clear stale file locks and kernel states.
2.  **Disable IPv6 (Temporary):** Attempt to launch the emulator with flags forcing IPv4, or disable IPv6 on the host loopback to force the emulator to bind `127.0.0.1`.
3.  **Use `cold-boot`:** Ensure the emulator does not load a corrupted snapshot: `-no-snapshot-load`.
4.  **Verify Firewall:** Once the system is responsive, check `nftables`/`iptables` for `DROP` rules on `lo`.
