#!/bin/bash
export ANDROID_HOME=/home/xf/Android/Sdk
export PATH=$ANDROID_HOME/emulator:$ANDROID_HOME/platform-tools:$PATH
export ANDROID_AVD_HOME=/home/xf/Android_Config_2/.android/avd

# Kill stale instances first
pkill -9 qemu-system-x86_64
rm -f $ANDROID_AVD_HOME/Pixel_Default.avd/*.lock

echo "Launching Android Emulator..."
$ANDROID_HOME/emulator/emulator -avd Pixel_Default -gpu swiftshader_indirect -no-snapshot-load -verbose -ports 5554,5555 > emulator_out.log 2>&1 &
PID=$!
echo "Emulator started with PID: $PID"
disown
