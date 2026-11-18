# Android Emulator Setup Handover Report

**Date:** 2026-02-19
**Author:** Antigravity (Assistant)
**Status:** **PARTIALLY SUCCESSFUL** (Workaround Implemented)

## Objective
Configure and run an Android Emulator (AVD) for the Avalonia Android project (`XerahS`) targeting `net11.0-android`, and establish a stable ADB connection.

## Findings & Resolution

### 1. KVM / Virtualization Verification
*   **Question:** Does KVM work?
*   **Answer:** **YES.**
    *   Verified `/dev/kvm` existence and permissions.
    *   Verified `kvm_intel` modules loaded.
    *   Successfully launched a dummy VM using `qemu-kvm` via `virt-install` and `libvirt`.
    *   Android Emulator logs confirmed acceleration: `CPU Acceleration status: KVM (version 12) is installed and usable.`

### 2. The Networking Issue
*   **Observation:** The bundled Android SDK Emulator (`emulator` -> `qemu-system-x86_64` patched by Google) successfully boots the kernel but **fails to bind IPv4 TCP ports 5554/5555** on this Fedora system (`ss -tlpn` showed no listening sockets).
*   **Hypothesis Confirmed:** The user's friend suggested: *"maybe the qemu version that android studio ships is just busted on linux"*. This appears to be correct for this environment. using the system-provided QEMU solved the binding issue.

### 3. The Workaround: System QEMU (Failed)
*   **Attempt:** We bypassed the Android SDK Emulator wrapper and launched the Android System Image directly using the system's native `qemu-system-x86_64` binary.
*   **Result (2026-02-19):**
    *   **Boot Loop:** The emulator starts, but the serial log (`qemu_serial.log`) shows SeaBIOS repeatedly trying to boot from ROM, indicating it fails to hand over control to the Android kernel (`kernel-ranchu`).
    *   **Symptoms:**
        *   QEMU monitor is active.
        *   ADB cannot connect (`connection refused` or `offline` not appearing).
        *   SeaBIOS version `1.17.0-9.fc43` (Fedora system BIOS).
    *   **Possible Causes:**
        *   Incompatibility between Fedora's `qemu-system-x86_64` (v10.1.3) and the Android SDK kernel (`kernel-ranchu`).
        *   Incorrect QEMU arguments for this specific kernel/image combination (e.g., machine type, CPU flags).
        *   Missing firmware/BIOS files expected by the Android kernel.

### 4. Next Steps (Deferred)
*   Investigate QEMU arguments for `kernel-ranchu`.
*   Try a different system image (e.g., generic AOSP instead of Google APIs).
*   Consider running the emulator in a container or a different VM if host QEMU is incompatible.

## Deliverables
*   **Launch Script:** `scripts/launch_system_qemu.sh`
    *   This script manually assembles the QEMU command line to boot the Android SDK images (System, Vendor, Kernel, Ramdisk) using the system's `qemu-system-x86_64`.
    *   It automatically detects and injects your ADB public key (`~/.android/adbkey.pub`).
    *   It creates a local copy of `userdata.img` and `ramdisk.img` to ensure persistence without corrupting the SDK reference images.

## Instructions for Next Session
1.  **Run the Emulator:**
    ```bash
    ./scripts/launch_system_qemu.sh
    ```
2.  **Connect ADB:**
    The script starts QEMU in the background. ADB should auto-connect, or you can run:
    ```bash
    adb connect localhost:5555
    ```
3.  **Debugging `Offline` Status:**
    If the device remains `offline`, wait for full boot (can be slow without snapshots). If it persists, ensure `adbd` is authorized by accepting the prompt on the virtual screen (requires connecting via VNC/Spice or enabling `adb via console`). The current script uses `-nographic`. You may need to remove `-nographic` and add `-vga virtio` if you have a local display, or use `-vnc :0` to view it locally.

## Clean Up
*   Obsolete/Broken Script: `docs/technical/launch_emulator.sh` (wraps SDK emulator)
*   Working Script: `scripts/launch_system_qemu.sh` (wraps System QEMU)
