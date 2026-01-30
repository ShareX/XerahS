# Region Capture Workflows

This document outlines the technical differences and implementation details of the two primary region capture modes in XerahS: **Transparent Overlay** and **Frozen Background**.

## Overview

XerahS supports two distinct approaches to region capture, determined by the `WorkflowType` and the underlying `RegionCaptureOptions.CaptureTransparent` property.

| Feature | RectangleTransparent | Standard Region Capture |
| :--- | :--- | :--- |
| **WorkflowType** | `WorkflowType.RectangleTransparent` | `WorkflowType.RectangleRegion` etc. |
| **Overlay Background** | **Transparent** (Live Desktop) | **Frozen Screenshot** (Static Image) |
| **Interaction** | User can see animations/updates behind overlay | Screen state is captured immediately when triggered |
| **Use Case** | capturing dynamic content, waiting for specific moment | Typical screenshot workflow |

## Implementation Details

### 1. Workflow Initiation (`WorkerTask.cs`)

The distinction is handled in `WorkerTask.DoWorkAsync`. The `useTransparentOverlay` flag is derived directly from the job type:

```csharp
// WorkerTask.cs
var useTransparentOverlay = taskSettings.Job == WorkflowType.RectangleTransparent;
```

This flag is then passed into the `CaptureOptions` object:

```csharp
var captureOptions = new CaptureOptions
{
    // ...
    CaptureTransparent = useTransparentOverlay,
    // ...
};
```

### 2. Service Layer (`ScreenCaptureService.cs`)

The `ScreenCaptureService` acts as a bridge, forwarding the options to the `RegionCaptureService`:

```csharp
// ScreenCaptureService.cs
var captureService = new RegionCaptureService
{
    Options = new XerahS.RegionCapture.RegionCaptureOptions
    {
        // ...
        CaptureTransparent = options?.CaptureTransparent ?? false,
    }
};
```

### 3. Region Capture Logic (`RegionCaptureService.cs`)

The `RegionCaptureService` uses the `CaptureTransparent` property to determine how to render the overlay using `OverlayManager`.

-   **`CaptureTransparent = true`**: The overlay window is created with a transparent background. No initial screenshot is taken. The user sees the live desktop through the overlay.
-   **`CaptureTransparent = false`** (Default): A full-screen screenshot is captured *before* the overlay is shown. This screenshot is rendered as the background of the overlay window, creating a "frozen" effect.

### Key Files

-   `src/XerahS.Core/Enums.cs`: Defines `WorkflowType.RectangleTransparent`.
-   `src/XerahS.Core/Tasks/WorkerTask.cs`: Orchestrates the workflow and sets the transparent flag.
-   `src/XerahS.RegionCapture/RegionCaptureService.cs`: Defines `RegionCaptureOptions` and manages the overlay behavior.
