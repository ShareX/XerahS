#!/bin/bash
# Launch Android using /usr/bin/qemu-system-x86_64 (System QEMU)
# Workaround for broken networking in bundled Android SDK emulator.

# Paths
ANDROID_HOME=/home/xf/Android/Sdk
SYSTEM_IMG=$ANDROID_HOME/system-images/android-34/default/x86_64/system.img
VENDOR_IMG=$ANDROID_HOME/system-images/android-34/default/x86_64/vendor.img
KERNEL=$ANDROID_HOME/system-images/android-34/default/x86_64/kernel-ranchu
RAMDISK=ramdisk.img
USERDATA=userdata.img

# Ensure local copies of writable images exist
if [ ! -f "$RAMDISK" ]; then
    cp $ANDROID_HOME/system-images/android-34/default/x86_64/ramdisk.img .
fi
if [ ! -f "$USERDATA" ]; then
    cp $ANDROID_HOME/system-images/android-34/default/x86_64/userdata.img .
fi

# Get ADB Key
ADB_KEY=$(cat ~/.android/adbkey.pub)

# Cleanup previous instances
pkill -f "Android System QEMU"

# Use system QEMU
QEMU_BIN=/usr/bin/qemu-system-x86_64

echo "Launching Android on System QEMU..."
echo "Using Kernel: $KERNEL"
echo "ADB Public Key will be injected."

nohup $QEMU_BIN \
    -name "Android System QEMU" \
    -m 4096 \
    -smp 4 \
    -cpu host \
    -enable-kvm \
    -device virtio-gpu-pci \
    -display none \
    -kernel $KERNEL \
    -append "console=ttyS0 androidboot.hardware=ranchu androidboot.selinux=permissive androidboot.dm_verity=disabled no_timer_check clocksource=pit loop.max_part=7 androidboot.shell=1 verbose androidboot.adb.pubkey=\"$ADB_KEY\"" \
    -initrd $RAMDISK \
    -drive if=none,index=0,id=system,file=$SYSTEM_IMG,format=raw,readonly=on \
    -device virtio-blk-pci,drive=system,modern-pio-notify \
    -drive if=none,index=1,id=vendor,file=$VENDOR_IMG,format=raw,readonly=on \
    -device virtio-blk-pci,drive=vendor,modern-pio-notify \
    -drive if=none,index=2,id=userdata,file=$USERDATA,format=raw \
    -device virtio-blk-pci,drive=userdata,modern-pio-notify \
    -netdev user,id=mynet,hostfwd=tcp::5555-:5555 \
    -device virtio-net-pci,netdev=mynet \
    -nographic \
    -serial file:qemu_serial.log \
    > qemu_system.log 2>&1 < /dev/null &
PID=$!
echo "System QEMU started with PID: $PID"
echo "Logs redirected to qemu_system.log and qemu_serial.log"
echo "You can check status with: adb connect localhost:5555"
disown
