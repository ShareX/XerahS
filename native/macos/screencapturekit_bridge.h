/*
 * screencapturekit_bridge.h
 * Native ScreenCaptureKit wrapper for ShareX.Avalonia
 *
 * This library provides a C-compatible interface for P/Invoke from .NET
 * Requires macOS 12.3+ (Monterey)
 */

#ifndef SCREENCAPTUREKIT_BRIDGE_H
#define SCREENCAPTUREKIT_BRIDGE_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Check if ScreenCaptureKit is available on this system.
 * @return 1 if available (macOS 12.3+), 0 otherwise
 */
int sck_is_available(void);

/**
 * Capture the entire screen as PNG data.
 * @param out_data Pointer to receive allocated buffer (caller must free with
 * sck_free_buffer)
 * @param out_length Pointer to receive buffer length in bytes
 * @return 0 on success, negative error code on failure:
 *         -1: ScreenCaptureKit not available
 *         -2: Permission denied (screen recording not authorized)
 *         -3: Capture failed
 *         -4: PNG encoding failed
 */
int sck_capture_fullscreen(uint8_t **out_data, int *out_length);

/**
 * Capture a rectangular region of the screen as PNG data.
 * @param x Left coordinate of the region
 * @param y Top coordinate of the region
 * @param w Width of the region
 * @param h Height of the region
 * @param out_data Pointer to receive allocated buffer (caller must free with
 * sck_free_buffer)
 * @param out_length Pointer to receive buffer length in bytes
 * @return 0 on success, negative error code on failure (same as fullscreen)
 */
int sck_capture_rect(float x, float y, float w, float h, uint8_t **out_data,
                     int *out_length);

/**
 * Capture a specific window by window ID as PNG data.
 * @param window_id The CGWindowID of the window to capture
 * @param out_data Pointer to receive allocated buffer (caller must free with
 * sck_free_buffer)
 * @param out_length Pointer to receive buffer length in bytes
 * @return 0 on success, negative error code on failure (same as fullscreen)
 */
int sck_capture_window(uint32_t window_id, uint8_t **out_data, int *out_length);

/**
 * Free a buffer allocated by capture functions.
 * @param data Buffer to free (safe to pass NULL)
 */
void sck_free_buffer(uint8_t *data);

/* ============================================================
 * VIDEO RECORDING API (macOS 12.3+)
 * Uses SCStream + AVAssetWriter for native video recording
 * ============================================================ */

/**
 * Recording session handle (opaque pointer).
 */
typedef void *sck_recording_session_t;

/**
 * Start recording the screen to a video file.
 * @param output_path Path to output file (.mov or .mp4)
 * @param x Left coordinate of region (0 for fullscreen)
 * @param y Top coordinate of region (0 for fullscreen)
 * @param w Width of region (0 for fullscreen)
 * @param h Height of region (0 for fullscreen)
 * @param fps Target frames per second (e.g., 30)
 * @param show_cursor 1 to show cursor, 0 to hide
 * @param out_session Receives the session handle on success
 * @return 0 on success, negative error code on failure
 */
int sck_start_recording(const char *output_path, float x, float y, float w,
                        float h, int fps, int show_cursor,
                        sck_recording_session_t *out_session);

/**
 * Stop recording and finalize the video file.
 * @param session Session handle from sck_start_recording
 * @return 0 on success, negative error code on failure
 */
int sck_stop_recording(sck_recording_session_t session);

/**
 * Abort recording without saving the file.
 * @param session Session handle from sck_start_recording
 * @return 0 on success, negative error code on failure
 */
int sck_abort_recording(sck_recording_session_t session);

/**
 * Check if a recording session is currently active.
 * @param session Session handle
 * @return 1 if recording, 0 otherwise
 */
int sck_is_recording(sck_recording_session_t session);

#ifdef __cplusplus
}
#endif

#endif /* SCREENCAPTUREKIT_BRIDGE_H */
