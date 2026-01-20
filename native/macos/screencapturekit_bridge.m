/*
 * screencapturekit_bridge.m
 * Native ScreenCaptureKit wrapper for ShareX.Avalonia
 *
 * Requires macOS 12.3+ (Monterey)
 * Build: clang -framework ScreenCaptureKit -framework Foundation -framework
 * CoreGraphics \ -framework AppKit -dynamiclib -o
 * libscreencapturekit_bridge.dylib screencapturekit_bridge.m
 */

#include "screencapturekit_bridge.h"
#import <AppKit/AppKit.h>
#import <CoreGraphics/CoreGraphics.h>
#import <Foundation/Foundation.h>
#import <ScreenCaptureKit/ScreenCaptureKit.h>

// Error codes
#define SCK_SUCCESS 0
#define SCK_ERROR_NOT_AVAILABLE -1
#define SCK_ERROR_PERMISSION_DENIED -2
#define SCK_ERROR_CAPTURE_FAILED -3
#define SCK_ERROR_ENCODING_FAILED -4

// Helper function to encode CGImage to PNG data
static NSData *encodeCGImageToPNG(CGImageRef image) {
  if (!image)
    return nil;

  NSBitmapImageRep *bitmapRep =
      [[NSBitmapImageRep alloc] initWithCGImage:image];
  if (!bitmapRep)
    return nil;

  NSData *pngData = [bitmapRep representationUsingType:NSBitmapImageFileTypePNG
                                            properties:@{}];
  return pngData;
}

// Check macOS version
static BOOL isScreenCaptureKitAvailable(void) {
  if (@available(macOS 12.3, *)) {
    return YES;
  }
  return NO;
}

int sck_is_available(void) { return isScreenCaptureKitAvailable() ? 1 : 0; }

int sck_capture_fullscreen(uint8_t **out_data, int *out_length) {
  if (!out_data || !out_length)
    return SCK_ERROR_CAPTURE_FAILED;
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
    [SCShareableContent getShareableContentWithCompletionHandler:^(
                            SCShareableContent *content, NSError *error) {
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
      SCContentFilter *filter =
          [[SCContentFilter alloc] initWithDisplay:mainDisplay
                                  excludingWindows:@[]];

      // Configure capture
      SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
      config.width = mainDisplay.width;
      config.height = mainDisplay.height;
      config.pixelFormat = kCVPixelFormatType_32BGRA;
      config.showsCursor = YES;

      // Capture screenshot
      [SCScreenshotManager
          captureImageWithFilter:filter
                   configuration:config
               completionHandler:^(CGImageRef image, NSError *captureError) {
                 if (captureError || !image) {
                   NSLog(@"[ScreenCaptureKit] Capture failed: %@",
                         captureError);
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
    dispatch_time_t timeout =
        dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
    if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
      NSLog(@"[ScreenCaptureKit] Capture timed out");
      return SCK_ERROR_CAPTURE_FAILED;
    }

    if (result == SCK_SUCCESS && capturedData) {
      *out_length = (int)capturedData.length;
      *out_data = (uint8_t *)malloc(*out_length);
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

int sck_capture_rect(float x, float y, float w, float h, uint8_t **out_data,
                     int *out_length) {
  if (!out_data || !out_length)
    return SCK_ERROR_CAPTURE_FAILED;
  *out_data = NULL;
  *out_length = 0;

  if (!isScreenCaptureKitAvailable()) {
    return SCK_ERROR_NOT_AVAILABLE;
  }

  if (@available(macOS 12.3, *)) {
    __block int result = SCK_SUCCESS;
    __block NSData *capturedData = nil;

    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);

    [SCShareableContent getShareableContentWithCompletionHandler:^(
                            SCShareableContent *content, NSError *error) {
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

      SCContentFilter *filter =
          [[SCContentFilter alloc] initWithDisplay:mainDisplay
                                  excludingWindows:@[]];

      SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
      config.width = (size_t)w;
      config.height = (size_t)h;
      config.pixelFormat = kCVPixelFormatType_32BGRA;
      config.showsCursor = YES;
      config.sourceRect = CGRectMake(x, y, w, h);
      config.destinationRect = CGRectMake(0, 0, w, h);

      [SCScreenshotManager
          captureImageWithFilter:filter
                   configuration:config
               completionHandler:^(CGImageRef image, NSError *captureError) {
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

    dispatch_time_t timeout =
        dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
    if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
      return SCK_ERROR_CAPTURE_FAILED;
    }

    if (result == SCK_SUCCESS && capturedData) {
      *out_length = (int)capturedData.length;
      *out_data = (uint8_t *)malloc(*out_length);
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

int sck_capture_window(uint32_t window_id, uint8_t **out_data,
                       int *out_length) {
  if (!out_data || !out_length)
    return SCK_ERROR_CAPTURE_FAILED;
  *out_data = NULL;
  *out_length = 0;

  if (!isScreenCaptureKitAvailable()) {
    return SCK_ERROR_NOT_AVAILABLE;
  }

  if (@available(macOS 12.3, *)) {
    __block int result = SCK_SUCCESS;
    __block NSData *capturedData = nil;

    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);

    [SCShareableContent getShareableContentWithCompletionHandler:^(
                            SCShareableContent *content, NSError *error) {
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
      SCContentFilter *filter = [[SCContentFilter alloc]
          initWithDesktopIndependentWindow:targetWindow];

      SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
      config.width = (size_t)targetWindow.frame.size.width;
      config.height = (size_t)targetWindow.frame.size.height;
      config.pixelFormat = kCVPixelFormatType_32BGRA;
      config.showsCursor = NO; // Window capture typically excludes cursor

      [SCScreenshotManager
          captureImageWithFilter:filter
                   configuration:config
               completionHandler:^(CGImageRef image, NSError *captureError) {
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

    dispatch_time_t timeout =
        dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
    if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
      return SCK_ERROR_CAPTURE_FAILED;
    }

    if (result == SCK_SUCCESS && capturedData) {
      *out_length = (int)capturedData.length;
      *out_data = (uint8_t *)malloc(*out_length);
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

void sck_free_buffer(uint8_t *data) {
  if (data) {
    free(data);
  }
}

// ============================================================
// VIDEO RECORDING IMPLEMENTATION
// ============================================================

#import <AVFoundation/AVFoundation.h>

API_AVAILABLE(macos(12.3))
@interface SCKRecordingSession : NSObject <SCStreamOutput>
@property(nonatomic, strong) SCStream *stream;
@property(nonatomic, strong) AVAssetWriter *assetWriter;
@property(nonatomic, strong) AVAssetWriterInput *videoInput;
@property(nonatomic, strong)
    AVAssetWriterInputPixelBufferAdaptor *pixelBufferAdaptor;
@property(nonatomic, assign) BOOL isRecording;
@property(nonatomic, assign) BOOL sessionStarted;
@property(nonatomic, strong) NSString *outputPath;
@property(nonatomic, strong) dispatch_queue_t processingQueue;
@end

@implementation SCKRecordingSession

- (instancetype)init {
  self = [super init];
  if (self) {
    _isRecording = NO;
    _sessionStarted = NO;
    _processingQueue = dispatch_queue_create(
        "com.xerahs.screencapturekit.recording", DISPATCH_QUEUE_SERIAL);
  }
  return self;
}

- (void)stream:(SCStream *)stream
    didOutputSampleBuffer:(CMSampleBufferRef)sampleBuffer
                   ofType:(SCStreamOutputType)type {
  if (!self.isRecording) {
    return;
  }

  if (type != SCStreamOutputTypeScreen) {
    return;
  }

  if (!sampleBuffer || !CMSampleBufferDataIsReady(sampleBuffer)) {
    return;
  }

  // Get the pixel buffer from the sample buffer
  CVImageBufferRef imageBuffer = CMSampleBufferGetImageBuffer(sampleBuffer);
  if (!imageBuffer) {
    return;
  }

  // Retain the sample buffer for async use
  CFRetain(sampleBuffer);
  CMTime presentationTime =
      CMSampleBufferGetPresentationTimeStamp(sampleBuffer);

  dispatch_async(self.processingQueue, ^{
    @try {
      if (!self.isRecording || !self.assetWriter ||
          self.assetWriter.status != AVAssetWriterStatusWriting) {
        CFRelease(sampleBuffer);
        return;
      }

      CVPixelBufferRef pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer);
      if (!pixelBuffer) {
        CFRelease(sampleBuffer);
        return;
      }

      if (!self.sessionStarted) {
        [self.assetWriter startSessionAtSourceTime:presentationTime];
        self.sessionStarted = YES;
        NSLog(@"[SCKRecording] Session started at timestamp: %.3fs",
              CMTimeGetSeconds(presentationTime));
      }

      if (self.pixelBufferAdaptor &&
          self.pixelBufferAdaptor.assetWriterInput.isReadyForMoreMediaData) {
        if (![self.pixelBufferAdaptor appendPixelBuffer:pixelBuffer
                                   withPresentationTime:presentationTime]) {
          if (self.assetWriter.status == AVAssetWriterStatusFailed) {
            NSLog(@"[SCKRecording] Failed to append pixel buffer: %@",
                  self.assetWriter.error);
          }
        }
      }
    } @catch (NSException *exception) {
      NSLog(@"[SCKRecording] Exception in frame callback: %@", exception);
    } @finally {
      CFRelease(sampleBuffer);
    }
  });
}

- (void)dealloc {
  NSLog(@"[SCKRecording] Session deallocated");
}

@end

int sck_start_recording(const char *output_path, float x, float y, float w,
                        float h, int fps, int show_cursor,
                        sck_recording_session_t *out_session) {
  if (!out_session || !output_path)
    return SCK_ERROR_CAPTURE_FAILED;
  *out_session = NULL;

  if (!isScreenCaptureKitAvailable()) {
    return SCK_ERROR_NOT_AVAILABLE;
  }

  if (@available(macOS 12.3, *)) {
    __block int result = SCK_SUCCESS;
    __block SCKRecordingSession *session = [[SCKRecordingSession alloc] init];
    session.outputPath = [NSString stringWithUTF8String:output_path];

    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);

    [SCShareableContent getShareableContentWithCompletionHandler:^(
                            SCShareableContent *content, NSError *error) {
      if (error || !content) {
        NSLog(@"[SCKRecording] Failed to get shareable content: %@", error);
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

      // Create filter
      SCContentFilter *filter =
          [[SCContentFilter alloc] initWithDisplay:mainDisplay
                                  excludingWindows:@[]];

      // Configure stream
      SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];

      BOOL isFullscreen = (w <= 0 || h <= 0);
      if (isFullscreen) {
        config.width = mainDisplay.width;
        config.height = mainDisplay.height;
      } else {
        config.width = (size_t)w;
        config.height = (size_t)h;
        config.sourceRect = CGRectMake(x, y, w, h);
        config.destinationRect = CGRectMake(0, 0, w, h);
      }

      config.minimumFrameInterval = CMTimeMake(1, fps);
      config.pixelFormat = kCVPixelFormatType_32BGRA;
      config.showsCursor = (show_cursor != 0);
      config.queueDepth = 5;

      NSLog(@"[SCKRecording] Config: %zux%zu @ %dfps, cursor=%d", config.width,
            config.height, fps, show_cursor);

      // Setup AVAssetWriter
      NSURL *outputURL = [NSURL fileURLWithPath:session.outputPath];

      // Remove existing file if present
      [[NSFileManager defaultManager] removeItemAtURL:outputURL error:nil];

      NSError *writerError = nil;
      session.assetWriter = [[AVAssetWriter alloc] initWithURL:outputURL
                                                      fileType:AVFileTypeMPEG4
                                                         error:&writerError];
      if (writerError) {
        NSLog(@"[SCKRecording] Failed to create AVAssetWriter: %@",
              writerError);
        result = SCK_ERROR_CAPTURE_FAILED;
        dispatch_semaphore_signal(semaphore);
        return;
      }

      // Configure video input
      NSDictionary *videoSettings = @{
        AVVideoCodecKey : AVVideoCodecTypeH264,
        AVVideoWidthKey : @(config.width),
        AVVideoHeightKey : @(config.height),
        AVVideoCompressionPropertiesKey : @{
          AVVideoAverageBitRateKey : @(4000000), // 4 Mbps
          AVVideoMaxKeyFrameIntervalKey : @(fps * 2),
          AVVideoProfileLevelKey : AVVideoProfileLevelH264HighAutoLevel
        }
      };

      session.videoInput =
          [[AVAssetWriterInput alloc] initWithMediaType:AVMediaTypeVideo
                                         outputSettings:videoSettings];
      session.videoInput.expectsMediaDataInRealTime = YES;

      if ([session.assetWriter canAddInput:session.videoInput]) {
        [session.assetWriter addInput:session.videoInput];
      } else {
        NSLog(@"[SCKRecording] Cannot add video input to writer");
        result = SCK_ERROR_CAPTURE_FAILED;
        dispatch_semaphore_signal(semaphore);
        return;
      }

      // Create pixel buffer adaptor for BGRA input
      NSDictionary *sourcePixelBufferAttributes = @{
        (NSString *)
        kCVPixelBufferPixelFormatTypeKey : @(kCVPixelFormatType_32BGRA),
        (NSString *)kCVPixelBufferWidthKey : @(config.width),
        (NSString *)kCVPixelBufferHeightKey : @(config.height)
      };
      session.pixelBufferAdaptor = [[AVAssetWriterInputPixelBufferAdaptor alloc]
             initWithAssetWriterInput:session.videoInput
          sourcePixelBufferAttributes:sourcePixelBufferAttributes];
      NSLog(@"[SCKRecording] Created pixel buffer adaptor for BGRA -> H264");

      // Start writer
      if (![session.assetWriter startWriting]) {
        NSLog(@"[SCKRecording] Failed to start writing: %@",
              session.assetWriter.error);
        result = SCK_ERROR_CAPTURE_FAILED;
        dispatch_semaphore_signal(semaphore);
        return;
      }

      // Create and start stream
      session.stream = [[SCStream alloc] initWithFilter:filter
                                          configuration:config
                                               delegate:nil];

      NSError *addOutputError = nil;
      [session.stream addStreamOutput:session
                                 type:SCStreamOutputTypeScreen
                   sampleHandlerQueue:session.processingQueue
                                error:&addOutputError];
      if (addOutputError) {
        NSLog(@"[SCKRecording] Failed to add stream output: %@",
              addOutputError);
        result = SCK_ERROR_CAPTURE_FAILED;
        dispatch_semaphore_signal(semaphore);
        return;
      }

      [session.stream startCaptureWithCompletionHandler:^(NSError *startError) {
        if (startError) {
          NSLog(@"[SCKRecording] Failed to start capture: %@", startError);
          result = SCK_ERROR_CAPTURE_FAILED;
        } else {
          session.isRecording = YES;
          NSLog(@"[SCKRecording] Recording started to: %@", session.outputPath);
        }
        dispatch_semaphore_signal(semaphore);
      }];
    }];

    dispatch_time_t timeout =
        dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
    if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
      NSLog(@"[SCKRecording] Start recording timed out");
      return SCK_ERROR_CAPTURE_FAILED;
    }

    if (result == SCK_SUCCESS) {
      *out_session = (__bridge_retained void *)session;
    }

    return result;
  }

  return SCK_ERROR_NOT_AVAILABLE;
}

int sck_stop_recording(sck_recording_session_t session_handle) {
  if (!session_handle)
    return SCK_ERROR_CAPTURE_FAILED;

  if (@available(macOS 12.3, *)) {
    SCKRecordingSession *session =
        (__bridge_transfer SCKRecordingSession *)session_handle;

    if (!session.isRecording) {
      return SCK_SUCCESS; // Already stopped
    }

    session.isRecording = NO;

    __block int result = SCK_SUCCESS;
    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);

    [session.stream stopCaptureWithCompletionHandler:^(NSError *error) {
      if (error) {
        NSLog(@"[SCKRecording] Error stopping capture: %@", error);
      }

      dispatch_sync(session.processingQueue, ^{
        [session.videoInput markAsFinished];

        [session.assetWriter finishWritingWithCompletionHandler:^{
          if (session.assetWriter.status == AVAssetWriterStatusCompleted) {
            NSLog(@"[SCKRecording] Recording saved successfully to: %@",
                  session.outputPath);
          } else {
            NSLog(@"[SCKRecording] Failed to finalize recording: %@",
                  session.assetWriter.error);
            result = SCK_ERROR_ENCODING_FAILED;
          }
          dispatch_semaphore_signal(semaphore);
        }];
      });
    }];

    dispatch_time_t timeout =
        dispatch_time(DISPATCH_TIME_NOW, 10 * NSEC_PER_SEC);
    if (dispatch_semaphore_wait(semaphore, timeout) != 0) {
      NSLog(@"[SCKRecording] Stop recording timed out");
      return SCK_ERROR_CAPTURE_FAILED;
    }

    return result;
  }

  return SCK_ERROR_NOT_AVAILABLE;
}

int sck_abort_recording(sck_recording_session_t session_handle) {
  if (!session_handle)
    return SCK_ERROR_CAPTURE_FAILED;

  if (@available(macOS 12.3, *)) {
    SCKRecordingSession *session =
        (__bridge_transfer SCKRecordingSession *)session_handle;

    session.isRecording = NO;

    [session.stream stopCaptureWithCompletionHandler:^(NSError *error) {
      if (error) {
        NSLog(@"[SCKRecording] Error aborting capture: %@", error);
      }
    }];

    [session.assetWriter cancelWriting];

    // Delete partial file
    [[NSFileManager defaultManager] removeItemAtPath:session.outputPath
                                               error:nil];

    NSLog(@"[SCKRecording] Recording aborted");
    return SCK_SUCCESS;
  }

  return SCK_ERROR_NOT_AVAILABLE;
}

int sck_is_recording(sck_recording_session_t session_handle) {
  if (!session_handle)
    return 0;

  if (@available(macOS 12.3, *)) {
    SCKRecordingSession *session =
        (__bridge SCKRecordingSession *)session_handle;
    return session.isRecording ? 1 : 0;
  }

  return 0;
}
