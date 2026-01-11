# XerahS CLI

Command-line interface for ShareX workflow automation.

## Overview

XerahS CLI provides headless access to the same workflow pipeline used by the ShareX.Avalonia UI application. It allows you to execute workflows, capture screenshots, and record screen activity from the command line or automation scripts.

**Platform Support:** Windows, macOS, and Linux - as long as .NET 10 is installed, the CLI can run on any supported platform.

## Architecture

### Shared Components

XerahS CLI shares the following components with ShareX.Avalonia.UI:

- **Configuration Files**: Same JSON files in `Documents/ShareX/Settings/`
  - `ApplicationConfig.json` - General application settings
  - `WorkflowsConfig.json` - Workflow definitions and hotkeys
  - `UploadersConfig.json` - Uploader credentials

- **Workflow Pipeline**: Same task execution engine (`WorkerTask`, `TaskManager`, `TaskHelpers`)
- **Screen Recording**: Same recording services (`ScreenRecordingManager`, `ScreenRecorderService`)
- **Platform Services**: Same platform abstractions for capture, input, etc.

### CLI-Specific Components

- **Headless Services**: Minimal `IUIService` and `IToastService` implementations that output to console
- **Command Parsers**: System.CommandLine-based argument parsing
- **Bootstrap**: Shared initialization logic extracted from UI startup

## Installation

Build the CLI project:

```bash
dotnet build src/XerahS.CLI/XerahS.CLI.csproj
```

The executable will be located at:

**Windows:**
```
src/XerahS.CLI/bin/Debug/net10.0-windows/xerahs.exe
```

**macOS/Linux:**
```
src/XerahS.CLI/bin/Debug/net10.0/xerahs
```

You can also publish for specific platforms:

```bash
# Windows
dotnet publish src/XerahS.CLI/XerahS.CLI.csproj -c Release -r win-x64 --self-contained

# macOS
dotnet publish src/XerahS.CLI/XerahS.CLI.csproj -c Release -r osx-x64 --self-contained
dotnet publish src/XerahS.CLI/XerahS.CLI.csproj -c Release -r osx-arm64 --self-contained

# Linux
dotnet publish src/XerahS.CLI/XerahS.CLI.csproj -c Release -r linux-x64 --self-contained
```

## Commands

### Run Workflow

Execute a configured workflow by ID:

```bash
xerahs run <workflow-id>
```

**Example:**
```bash
# Run full screen capture workflow
xerahs run WF01

# Run active window capture workflow
xerahs run WF02
```

**Exit Codes:**
- `0` - Success
- `1` - Failure (workflow not found, disabled, or execution failed)

### Screen Recording

#### Start Recording

```bash
xerahs record start [options]
```

**Options:**
- `--mode <screen|window|region>` - Capture mode (default: screen)
- `--region <x,y,width,height>` - Region coordinates for region mode
- `--fps <number>` - Frames per second (default: 30)
- `--codec <h264|hevc|vp9|av1>` - Video codec (default: h264)
- `--bitrate <kbps>` - Video bitrate in Kbps (default: 4000)
- `--audio` - Capture system audio
- `--microphone` - Capture microphone
- `--output <path>` - Output file path

**Examples:**
```bash
# Start full screen recording
xerahs record start

# Record with high quality settings
xerahs record start --fps 60 --codec hevc --bitrate 8000

# Record with audio
xerahs record start --audio --microphone

# Record to specific path
xerahs record start --output "C:\Videos\my-recording.mp4"

# Record specific region
xerahs record start --mode region --region "100,100,1920,1080"
```

#### Stop Recording

```bash
xerahs record stop
```

Stops the active recording and saves the file.

#### Abort Recording

```bash
xerahs record abort
```

Aborts the active recording without saving.

### Screen Capture

#### Capture Full Screen

```bash
xerahs capture screen [--output <path>]
```

**Example:**
```bash
xerahs capture screen --output "C:\Screenshots\screen.png"
```

#### Capture Active Window

```bash
xerahs capture window [--output <path>]
```

**Example:**
```bash
xerahs capture window --output "C:\Screenshots\window.png"
```

#### Capture Region

```bash
xerahs capture region --region <x,y,width,height> [--output <path>]
```

**Note:** Region capture from CLI is not fully implemented yet.

### List Workflows

```bash
xerahs list workflows [--enabled-only]
```

**Example:**
```bash
# List all workflows
xerahs list workflows

# List only enabled workflows
xerahs list workflows --enabled-only
```

**Output:**
```
Workflows (4):

  WF01 - Full screen capture
    Job: PrintScreen
    Hotkey: PrintScreen
    Status: enabled

  WF02 - Active window capture
    Job: ActiveWindow
    Hotkey: Alt+PrintScreen
    Status: enabled

  WF03 - Screen recording - GDI
    Job: ScreenRecorder
    Hotkey: Shift+PrintScreen
    Status: enabled

  WF04 - Screen recording - Game
    Job: ScreenRecorder
    Hotkey: Ctrl+Shift+PrintScreen
    Status: disabled
```

### Configuration

#### Show Configuration Summary

```bash
xerahs config show
```

#### Show Configuration Paths

```bash
xerahs config path
```

**Example Output:**
```
Configuration File Paths:

Personal Folder:
  C:\Users\username\Documents\ShareX

Settings Folder:
  C:\Users\username\Documents\ShareX\Settings

Configuration Files:
  ApplicationConfig: C:\Users\username\Documents\ShareX\Settings\ApplicationConfig.json
  WorkflowsConfig:   C:\Users\username\Documents\ShareX\Settings\WorkflowsConfig.json
  UploadersConfig:   C:\Users\username\Documents\ShareX\Settings\UploadersConfig.json

Backup Folder:
  C:\Users\username\Documents\ShareX\Settings\Backup
```

## Configuration Parity

XerahS CLI loads the exact same configuration files as ShareX.Avalonia.UI:

1. **Application Settings** (`ApplicationConfig.json`):
   - Theme settings
   - Default save locations
   - Upload preferences

2. **Workflows** (`WorkflowsConfig.json`):
   - Workflow definitions
   - Hotkey bindings (not used by CLI)
   - Task settings per workflow

3. **Uploaders** (`UploadersConfig.json`):
   - Uploader credentials
   - Upload destinations

Changes made via the UI are immediately visible to the CLI and vice versa.

## Workflow Pipeline

CLI uses the same workflow execution pipeline as the UI:

```
CLI Command
    ↓
TaskHelpers.ExecuteWorkflow()
    ↓
TaskManager.StartTask()
    ↓
WorkerTask.DoWorkAsync()
    ↓
[Capture → Process → Upload]
    ↓
TaskManager.TaskCompleted (event)
    ↓
CLI waits and returns exit code
```

## Headless Execution

XerahS CLI runs headless without requiring Avalonia UI initialization:

- **Logging**: Same log files in `Documents/ShareX/Logs/yyyy-MM/`
- **Platform Services**: Full platform service initialization (screen, capture, input, etc.)
- **Recording**: Synchronous recording initialization to ensure services are ready
- **UI Interactions**: Console output instead of toast notifications or dialogs

## Exit Codes

All CLI commands return appropriate exit codes:

- `0` - Success
- `1` - Failure

This allows integration with scripts and CI/CD pipelines:

```bash
# Example: Fail build if capture fails
if ! xerahs capture screen --output screenshot.png; then
    echo "Capture failed!"
    exit 1
fi
```

## Automation Examples

### Scheduled Screenshot

```bash
# Windows Task Scheduler or cron job
xerahs capture screen --output "C:\Screenshots\daily-%date%.png"
```

### Automated Recording

```bash
# Start recording, run application, stop recording
xerahs record start --fps 60 --output test-recording.mp4
myapp.exe
xerahs record stop
```

### Workflow Execution

```bash
# Execute multiple workflows
xerahs run WF01
xerahs run WF02
xerahs run WF03
```

## Troubleshooting

### Logs

CLI logs to the same location as the UI application:

```
Documents/ShareX/Logs/yyyy-MM/ShareX-yyyy-MM-dd.log
```

### Common Issues

**Recording fails immediately:**
- Ensure recording initialization completed (CLI waits automatically)
- Check log files for platform capability errors
- Audio capture requires FFmpeg fallback (automatically enabled with `--audio`)

**Workflow not found:**
- Use `xerahs list workflows` to see available workflow IDs
- Check that `WorkflowsConfig.json` exists and contains workflows

**Configuration not loading:**
- Use `xerahs config path` to verify configuration file locations
- Ensure files are valid JSON
- Check backup files in `Settings/Backup/` if main files are corrupted

## Development

### Adding New Commands

1. Create command class in `src/XerahS.CLI/Commands/`
2. Implement `Create()` method returning `System.CommandLine.Command`
3. Register in `Program.cs`

Example:

```csharp
public static class MyCommand
{
    public static Command Create()
    {
        var command = new Command("mycommand", "Description");

        command.SetHandler(async () =>
        {
            Environment.ExitCode = await ExecuteAsync();
        });

        return command;
    }

    private static async Task<int> ExecuteAsync()
    {
        // Implementation
        return 0;
    }
}
```

### Shared Bootstrap

Both UI and CLI use `ShareXBootstrap.InitializeAsync()` from the `ShareX.Avalonia.Bootstrap` project:

**UI Bootstrap:**
```csharp
var options = new BootstrapOptions
{
    EnableLogging = true,
    InitializeRecording = false, // Async in background
    UIService = new AvaloniaUIService(),
    ToastService = new AvaloniaToastService()
};
await ShareXBootstrap.InitializeAsync(options);
```

**CLI Bootstrap:**
```csharp
var options = new BootstrapOptions
{
    EnableLogging = true,
    InitializeRecording = true, // Wait for completion
    UIService = new HeadlessUIService(),
    ToastService = new HeadlessToastService()
};
await ShareXBootstrap.InitializeAsync(options);
```

## Validation

XerahS CLI meets the following validation criteria:

- ✅ Loads the same JSON files as ShareX.Avalonia.UI
- ✅ Uses the same workflow pipeline (no duplicated implementation)
- ✅ ScreenRecorder job can be executed through shared pipeline
- ✅ Runs headless without UI rendering
- ✅ Returns non-zero exit codes on failure
- ✅ ShareX.Avalonia.UI behavior unchanged

## Future Enhancements

- Region capture UI selector for CLI
- Watch mode for automated workflow triggers
- Batch processing commands
- JSON output mode for script consumption
- Progress bars for long-running operations
