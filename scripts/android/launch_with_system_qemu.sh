#!/bin/bash
# Attempt to launch Android using /usr/bin/qemu-system-x86_64 (System QEMU)
# Warning: This stripes Android-specific flags like -android-ports.
# We manually add ADB port forwarding.

ANDROID_HOME=/home/xf/Android/Sdk
AVD_HOME=/home/xf/Android_Config_2/.android/avd
SYSTEM_IMG=$ANDROID_HOME/system-images/android-34/default/x86_64/system.img
VENDOR_IMG=$ANDROID_HOME/system-images/android-34/default/x86_64/vendor.img
KERNEL=$ANDROID_HOME/system-images/android-34/default/x86_64/kernel-ranchu
RAMDISK=$ANDROID_HOME/system-images/android-34/default/x86_64/ramdisk.img

# Use system QEMU
QEMU_BIN=/usr/bin/qemu-system-x86_64

echo "Launching Android on System QEMU..."

$QEMU_BIN \
    -name "Android System QEMU" \
    -m 2048 \
    -smp 2 \
    -cpu host \
    -enable-kvm \
    -kernel $KERNEL \
    -append "console=ttyS0 androidboot.hardware=ranchu androidboot.selinux=permissive androidboot.dm_verity=disabled no_timer_check clocksource=pit loop.max_part=7" \
    -initrd $RAMDISK \
    -drive if=none,index=0,id=system,file=$SYSTEM_IMG,format=raw,readonly=on \
    -device virtio-blk-pci,drive=system,modern-pio-notify \
    -drive if=none,index=1,id=vendor,file=$VENDOR_IMG,format=raw,readonly=on \
    -device virtio-blk-pci,drive=vendor,modern-pio-notify \
    -netdev user,id=mynet,hostfwd=tcp::5555-:5555 \
    -device virtio-net-pci,netdev=mynet \
    -nographic \
    -serial mon:stdio \
    &
PID=$!
echo "System QEMU started with PID: $PID"
disown
