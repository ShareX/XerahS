/*
 * screencapturekit_bridge.m
 * Native ScreenCaptureKit wrapper for ShareX.Avalonia
 * 
 * Requires macOS 12.3+ (Monterey)
 * Build: clang -framework ScreenCaptureKit -framework Foundation -framework CoreGraphics \
 *        -framework AppKit -dynamiclib -o libscreencapturekit_bridge.dylib screencapturekit_bridge.m
 */

#import <Foundation/Foundation.h>
#import <CoreGraphics/CoreGraphics.h>
#import <AppKit/AppKit.h>
#import <ScreenCaptureKit/ScreenCaptureKit.h>
#include "screencapturekit_bridge.h"

// Error codes
#define SCK_SUCCESS 0
#define SCK_ERROR_NOT_AVAILABLE -1
#define SCK_ERROR_PERMISSION_DENIED -2
#define SCK_ERROR_CAPTURE_FAILED -3
#define SCK_ERROR_ENCODING_FAILED -4

// Helper function to encode CGImage to PNG data
static NSData* encodeCGImageToPNG(CGImageRef image) {
    if (!image) return nil;
    
    NSBitmapImageRep *bitmapRep = [[NSBitmapImageRep alloc] initWithCGImage:image];
    if (!bitmapRep) return nil;
    
    NSData *pngData = [bitmapRep representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
    return pngData;
}

// Check macOS version
static BOOL isScreenCaptureKitAvailable(void) {
    if (@available(macOS 12.3, *)) {
        return YES;
    }
    return NO;
}

int sck_is_available(void) {
    return isScreenCaptureKitAvailable() ? 1 : 0;
}

int sck_capture_fullscreen(uint8_t** out_data, int* out_length) {
    if (!out_data || !out_length) return SCK_ERROR_CAPTURE_FAILED;
    *out_data = NULL;
    *out_length = 0;
    
    if (!isScreenCaptureKitAvailable()) {
        return SCK_ERROR_NOT_AVAILABLE;
    }
    
    if (@available(macOS 12.3, *)) {
        __block int result = SCK_SUCCESS;
        __block NSData *capturedData = nil;
        
        dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
        
        // Get shareable content
        [SCShareableContent getShareableContentWithCompletionHandler:^(SCShareableContent *content, NSError *error) {
            if (error || !content) {
                NSLog(@"[ScreenCaptureKit] Failed to get shareable content: %@", error);
                result = SCK_ERROR_PERMISSION_DENIED;
                dispatch_semaphore_signal(semaphore);
                return;
            }
            
            // Get the main display
            SCDisplay *mainDisplay = content.displays.firstObject;
            if (!mainDisplay) {
                NSLog(@"[ScreenCaptureKit] No displays found");
                result = SCK_ERROR_CAPTURE_FAILED;
                dispatch_semaphore_signal(semaphore);
                return;
            }
            
            // Create filter for the display
            SCContentFilter *filter = [[SCContentFilter alloc] initWithDisplay:mainDisplay excludingWindows:@[]];
            
            // Configure capture
            SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
            config.width = mainDisplay.width;
            config.height = mainDisplay.height;
            config.pixelFormat = kCVPixelFormatType_32BGRA;
            config.showsCursor = YES;
            
            // Capture screenshot
            [SCScreenshotManager captureImageWithFilter:filter configuration:config completionHandler:^(CGImageRef image, NSError *captureError) {
                if (captureError || !image) {
                    NSLog(@"[ScreenCaptureKit] Capture failed: %@", captureError);
                    result = SCK_ERROR_CAPTURE_FAILED;
                    dispatch_semaphore_signal(semaphore);
                    return;
                }
                
                capturedData = encodeCGImageToPNG(image);
                if (!capturedData || capturedData.length == 0) {
                    NSLog(@"[ScreenCaptureKit] PNG encoding failed");
                    result = SCK_ERROR_ENCODING_FAILED;
                }
                
                dispatch_semaphore_signal(semaphore);
            }];
        }];
        
        // Wait for async operation (timeout after 10 seconds)
        dispatch_time_t timeout = dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
        if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
            NSLog(@"[ScreenCaptureKit] Capture timed out");
            return SCK_ERROR_CAPTURE_FAILED;
        }
        
        if (result == SCK_SUCCESS && capturedData) {
            *out_length = (int)capturedData.length;
            *out_data = (uint8_t*)malloc(*out_length);
            if (*out_data) {
                memcpy(*out_data, capturedData.bytes, *out_length);
            } else {
                result = SCK_ERROR_CAPTURE_FAILED;
            }
        }
        
        return result;
    }
    
    return SCK_ERROR_NOT_AVAILABLE;
}

int sck_capture_rect(float x, float y, float w, float h, uint8_t** out_data, int* out_length) {
    if (!out_data || !out_length) return SCK_ERROR_CAPTURE_FAILED;
    *out_data = NULL;
    *out_length = 0;
    
    if (!isScreenCaptureKitAvailable()) {
        return SCK_ERROR_NOT_AVAILABLE;
    }
    
    if (@available(macOS 12.3, *)) {
        __block int result = SCK_SUCCESS;
        __block NSData *capturedData = nil;
        
        dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
        
        [SCShareableContent getShareableContentWithCompletionHandler:^(SCShareableContent *content, NSError *error) {
            if (error || !content) {
                result = SCK_ERROR_PERMISSION_DENIED;
                dispatch_semaphore_signal(semaphore);
                return;
            }
            
            SCDisplay *mainDisplay = content.displays.firstObject;
            if (!mainDisplay) {
                result = SCK_ERROR_CAPTURE_FAILED;
                dispatch_semaphore_signal(semaphore);
                return;
            }
            
            SCContentFilter *filter = [[SCContentFilter alloc] initWithDisplay:mainDisplay excludingWindows:@[]];
            
            SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
            config.width = (size_t)w;
            config.height = (size_t)h;
            config.pixelFormat = kCVPixelFormatType_32BGRA;
            config.showsCursor = YES;
            config.sourceRect = CGRectMake(x, y, w, h);
            config.destinationRect = CGRectMake(0, 0, w, h);
            
            [SCScreenshotManager captureImageWithFilter:filter configuration:config completionHandler:^(CGImageRef image, NSError *captureError) {
                if (captureError || !image) {
                    result = SCK_ERROR_CAPTURE_FAILED;
                    dispatch_semaphore_signal(semaphore);
                    return;
                }
                
                capturedData = encodeCGImageToPNG(image);
                if (!capturedData || capturedData.length == 0) {
                    result = SCK_ERROR_ENCODING_FAILED;
                }
                
                dispatch_semaphore_signal(semaphore);
            }];
        }];
        
        dispatch_time_t timeout = dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
        if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
            return SCK_ERROR_CAPTURE_FAILED;
        }
        
        if (result == SCK_SUCCESS && capturedData) {
            *out_length = (int)capturedData.length;
            *out_data = (uint8_t*)malloc(*out_length);
            if (*out_data) {
                memcpy(*out_data, capturedData.bytes, *out_length);
            } else {
                result = SCK_ERROR_CAPTURE_FAILED;
            }
        }
        
        return result;
    }
    
    return SCK_ERROR_NOT_AVAILABLE;
}

int sck_capture_window(uint32_t window_id, uint8_t** out_data, int* out_length) {
    if (!out_data || !out_length) return SCK_ERROR_CAPTURE_FAILED;
    *out_data = NULL;
    *out_length = 0;
    
    if (!isScreenCaptureKitAvailable()) {
        return SCK_ERROR_NOT_AVAILABLE;
    }
    
    if (@available(macOS 12.3, *)) {
        __block int result = SCK_SUCCESS;
        __block NSData *capturedData = nil;
        
        dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
        
        [SCShareableContent getShareableContentWithCompletionHandler:^(SCShareableContent *content, NSError *error) {
            if (error || !content) {
                result = SCK_ERROR_PERMISSION_DENIED;
                dispatch_semaphore_signal(semaphore);
                return;
            }
            
            // Find the window by ID
            SCWindow *targetWindow = nil;
            for (SCWindow *window in content.windows) {
                if (window.windowID == window_id) {
                    targetWindow = window;
                    break;
                }
            }
            
            if (!targetWindow) {
                NSLog(@"[ScreenCaptureKit] Window with ID %u not found", window_id);
                result = SCK_ERROR_CAPTURE_FAILED;
                dispatch_semaphore_signal(semaphore);
                return;
            }
            
            // Create a filter for the specific window
            SCContentFilter *filter = [[SCContentFilter alloc] initWithDesktopIndependentWindow:targetWindow];
            
            SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
            config.width = (size_t)targetWindow.frame.size.width;
            config.height = (size_t)targetWindow.frame.size.height;
            config.pixelFormat = kCVPixelFormatType_32BGRA;
            config.showsCursor = NO; // Window capture typically excludes cursor
            
            [SCScreenshotManager captureImageWithFilter:filter configuration:config completionHandler:^(CGImageRef image, NSError *captureError) {
                if (captureError || !image) {
                    result = SCK_ERROR_CAPTURE_FAILED;
                    dispatch_semaphore_signal(semaphore);
                    return;
                }
                
                capturedData = encodeCGImageToPNG(image);
                if (!capturedData || capturedData.length == 0) {
                    result = SCK_ERROR_ENCODING_FAILED;
                }
                
                dispatch_semaphore_signal(semaphore);
            }];
        }];
        
        dispatch_time_t timeout = dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
        if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
            return SCK_ERROR_CAPTURE_FAILED;
        }
        
        if (result == SCK_SUCCESS && capturedData) {
            *out_length = (int)capturedData.length;
            *out_data = (uint8_t*)malloc(*out_length);
            if (*out_data) {
                memcpy(*out_data, capturedData.bytes, *out_length);
            } else {
                result = SCK_ERROR_CAPTURE_FAILED;
            }
        }
        
        return result;
    }
    
    return SCK_ERROR_NOT_AVAILABLE;
}

void sck_free_buffer(uint8_t* data) {
    if (data) {
        free(data);
    }
}
