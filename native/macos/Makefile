# ScreenCaptureKit Bridge Library
# Build script for macOS native library
# Requires: Xcode Command Line Tools, macOS 12.3+

CC = clang
FRAMEWORKS = -framework ScreenCaptureKit -framework Foundation -framework CoreGraphics -framework AppKit -framework AVFoundation -framework CoreMedia -framework CoreVideo
CFLAGS = -fobjc-arc -O2 -arch x86_64 -arch arm64
OUTPUT = libscreencapturekit_bridge.dylib
SOURCE = screencapturekit_bridge.m

.PHONY: all clean install

all: $(OUTPUT)

$(OUTPUT): $(SOURCE) screencapturekit_bridge.h
	$(CC) $(CFLAGS) $(FRAMEWORKS) -dynamiclib -o $(OUTPUT) $(SOURCE)
	@echo "Built $(OUTPUT)"
	@echo "Copy to output directory: cp $(OUTPUT) <your-app-output-dir>/"

clean:
	rm -f $(OUTPUT)

install: $(OUTPUT)
	@echo "Installing to /usr/local/lib/$(OUTPUT)"
	cp $(OUTPUT) /usr/local/lib/
	@echo "Done. You may need to run: install_name_tool -id @rpath/$(OUTPUT) /usr/local/lib/$(OUTPUT)"

