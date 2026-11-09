# Inline Workflows workspace

The Workflows page follows Destination Settings' navigation structure:

- The left rail lists workflows in their saved order.
- The next rail selects Task, Image, Video, Settings, or Index, with sections
  available according to the workflow's task. Short labels keep this rail narrow.
- The selected section is edited directly in the content area.

Selecting, adding, or duplicating a workflow opens its editor inside the page.
The workflow editor dialog remains available to callers outside this page.
Add, duplicate, remove, pin, reorder, and reset actions remain available.

The left rail contains Add workflow, the workflow list, and Reset workflows.
An Options menu beside the selected workflow name holds duplicate, pin, reorder,
and remove actions. This replaces the wide toolbar and redundant Edit button.
The Task section retains description, task selection, workflow identity, and
hotkey recording. Existing image, video, task-option, and index controls are
reused rather than maintaining a second editor implementation.

## Saving

Changes save automatically after a short pause in editing. Switching workflows
or leaving the page flushes pending changes. The status distinguishes a pending
save, successful persistence, and a failure that can be retried. Hotkey changes
are registered when the edited workflow is applied.

The editor observes ordinary settings, image-effect changes, and property-grid
edits so advanced values also participate in automatic saving. It keeps the
current editor instance during its own workflow refreshes to preserve focus
and section selection.

## Layout and accessibility

The workspace uses shared theme surfaces and system accent colors. Workflow and
section navigation remain available while the content scrolls independently.
Wide settings panels can scroll horizontally rather than hiding their inputs.
Workflow list labels expose task and shortcut details; navigation and action
controls have accessible names.

## Verification

The desktop solution builds with zero warnings and errors. Behavioral checks
cover debounced saves, switching workflows, failure/retry, preserving runtime
hotkey registration, effects, advanced properties, FFmpeg draft ownership,
pinning, adding, duplicating, removing, and reordering. An on-disk round trip
checks both an edited workflow and an unchanged neighboring workflow.

Headless previews cover Task, Image, and Effects in both themes at 1126 × 700,
plus an 800 × 600 viewport. The Task controls fit the smaller viewport; wider
panels retain horizontal scrolling. Local generated PNGs are under
`artifacts/workflows-inline/previews/`. The native Windows preview was also
opened with an isolated profile to inspect the inline editor in the shell.
