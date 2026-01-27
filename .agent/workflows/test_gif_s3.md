---
description: Test GIF recording and verify Amazon S3 upload readiness
---
1. Build the CLI
// turbo
2. Run the GIF recording verification command against the local environment
   `xerahs-cli verify-gif-recording --duration 5 --debug`

# Note
This workflow verifies the `ScreenRecorderGIF` pipeline using `WorkerTask` and `VideoHelpers`.
To fully verify Amazon S3 upload:
1. Ensure `UploadersConfig.json` has valid Amazon S3 settings.
2. The current `verify-gif-recording` command focuses on GIF generation.
3. To test upload, usage of the full UI or manually configuring a workflow with S3 as destination is recommended.
