# Wayland recording

- Never pass the portal's PipeWire node ID as `pipewiresrc target-object`; always use `path` with the portal remote descriptor because `target-object` matches an object name or serial and can select the default webcam instead.
- Never pipe arbitrary-width GStreamer I420 buffers into FFmpeg's packed `yuv420p` input; always align the capture and encoder width to eight pixels and height to two, or explicitly repack each plane. GStreamer rounds each plane's row stride to four bytes. A 1422x1140 buffer has 3,420 padding bytes per frame, causing diagonal tearing, chroma corruption and drifting frame boundaries.
- Never treat an existing MP4 or FFmpeg exit code zero as proof that the raw bridge worked; always verify complete input frames and both process results. FFmpeg can write a nonempty MP4 and exit successfully after reporting an incomplete rawvideo packet.
- Never publish recording status before registering the capture child under the stop lock; always make the child available to the stop operation so a quick stop cannot miss a process and leave its pipe running.
- Never return a raw bridge recording while its encoder task is still writing; always join the worker before exposing the MP4 to save, upload or playback, with the encoder worker owning shutdown and timeout cleanup.
