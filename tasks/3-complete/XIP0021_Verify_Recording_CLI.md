# XIP0021: Verify Recording CLI Command

## Goal
Implement a new CLI command `verify-recording` (or `test-recording`) to automate the verification of the screen recording pipeline, specifically targeting the FFmpeg implementation ("Use modern screen capture" unchecked).

## Background
The user requires a mechanism to robustly test the "Screen Recorder" workflow (specifically ID `67f116dc`) by running it repeatedly with random parameters and verifying the output. The existing `run` command does not natively support random region injection or automated verification of the output file. The `XerahS.CLI` toolset will be updated to support these verification requirements directly.

## Requirements
1.  **Workflow Execution**: Utilize `XerahS.CLI` to execute a specific workflow (ID `67f116dc`).
2.  **Random Region**: The CLI must have flags to randomly select an area on the screen.
3.  **FFmpeg Recording**: The command utilizes the workflow (which is configured for FFmpeg) to record.
4.  **Duration Control**: The CLI must stop the recording after a specified duration (e.g., 10 seconds).
5.  **Output Verification**: The CLI must check if the resulting MP4 file exists and is non-zero bytes.
6.  **Debug Info**: On failure, provide sufficient debug info (logs, FFmpeg output) to help diagnose the issue.
7.  **Iterative**: The command should be designed to be run iteratively until success (or support an `--iterations` flag).

## Proposed Changes

### 1. New Command: `VerifyRecordingCommand`
Create a new command class `src/XerahS.CLI/Commands/VerifyRecordingCommand.cs`.

**Command Syntax:**
```bash
xerahs verify-recording [workflow-id] [options]
```

**Options:**
- `workflow-id`: The workflow ID to execute (default: `67f116dc`).
- `--random-region`: (Flag) Inspects monitors and picks a random safe region (using logic similar to `VerifyRegionCaptureCommand`).
- `--duration <seconds>`: Duration to record (default: 10).
- `--iterations <count>`: Number of attempts (default: 1). Use loop for "iterate until success" if needed.
- `--debug`: Enable verbose logging.

### 2. Implementation Logic

1.  **Resolve Workflow**: Load the workflow by ID from `SettingsManager`.
2.  **Set Up Region**:
    *   If `--random-region` is set:
        *   Get `PlatformServices.Screen.GetVirtualScreenBounds()`.
        *   Generate a random valid rectangle (e.g., 500x500) within bounds.
        *   Handle monitor boundaries as needed (or keep it simple for now).
    *   Inject this region into the workflow settings.
        *   **Mechanism**: Set `workflow.TaskSettings.CaptureSettings.CaptureCustomRegion` AND ensure the workflow job is configured to use it (or force it via logic).
        *   Alternatively, call `ScreenRecordingManager` directly with the region if the workflow abstraction is too rigid.
3.  **Ensure FFmpeg/Legacy Mode**:
    *   Verify `workflow.TaskSettings.CaptureSettings.UseModernCapture` is `false`. If not, warn or override (if the intent is to strict test the unchecked state).
4.  **Execute Recording**:
    *   Start the recording task.
    *   **Crucial**: Ensure `ScreenRecordingManager` receives the specific random region.
5.  **Wait**:
    *   `await Task.Delay(duration * 1000)`.
6.  **Stop Recording**:
    *   Call `ScreenRecordingManager.Instance.StopRecordingAsync()`.
    *   Wait for the task to complete (listen to `TaskCompleted` event).
7.  **Verify Output**:
    *   Retrieve the result `TaskInfo`.
    *   Check `task.Results` for the file path.
    *   **Validation**:
        ```csharp
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            Console.WriteLine("PASS: Recording successful.");
            // Return 0
        }
        else
        {
            Console.Error.WriteLine("FAIL: Output file invalid.");
            // Dump logs/debug info
            // Return 1
        }
        ```

### 3. File Updates

#### [NEW] `src/XerahS.CLI/Commands/VerifyRecordingCommand.cs`
- Implement the `VerifyRecordingCommand` class.
- Reuse `RegionCaptureVerifier` code for monitor discovery and random rect generation (refactor `RegionCaptureVerifier` to `XerahS.Common` or duplicate minimal logic).

#### [MODIFY] `src/XerahS.CLI/Program.cs`
- Register the new command: `rootCommand.Add(VerifyRecordingCommand.Create());`

## Verification Plan
1.  Build the CLI.
2.  Run `xerahs verify-recording 67f116dc --random-region --duration 5`.
3.  Verify that:
    *   A random region is selected.
    *   Recording flows for 5 seconds.
    *   An MP4 file is created.
    *   The CLI exits with code 0 on success, 1 on failure.
4.  Test failure case (e.g., disconnect ffmpeg) to ensure debug info is printed.
