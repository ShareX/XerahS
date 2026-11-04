---
name: xerahs
description: >
  REQUIRED for anything involving XerahS, the ShareX-style screenshot, screen
  recording and upload app. Use when the user wants to change what happens
  after a capture, configure XerahS workflows (capture jobs bound to hotkeys),
  import or apply image effects or ShareX presets (.sxie, .xsie), turn the
  After Capture window on or off, upload a file through their XerahS
  destination, or check whether uploads are set up. Triggers: XerahS, ShareX,
  omaxerahs, workflow, after capture, image effects, border/shadow/watermark
  presets, .sxie, .xsie, upload screenshot, screenshot destination.
---

# XerahS Skill

XerahS is a desktop screenshot, screen recording and upload app (the Avalonia
successor to ShareX). Drive it through its CLI, **`omaxerahs`**, which ships with
XerahS and is on `PATH`. Do not hand-edit XerahS JSON config files: the running
app keeps settings in memory and would overwrite your edits, while `omaxerahs`
saves the change and tells the running app to reload it.

## CLI contract

- Every command prints exactly one JSON object to stdout.
  Success: `{"schemaVersion":1,"ok":true,...}`.
  Failure: `{"schemaVersion":1,"ok":false,"error":{"code":"...","message":"..."}}`, exit code 1.
- Human hints go to stderr. Pass `--json` to suppress them.
- Commands that change settings return `"appNotified"`: `true` means the running
  XerahS reloaded the change immediately; `false` means XerahS is not running and
  the change applies on its next start. Both are success.
- Error codes: `not_found`, `ambiguous`, `invalid_path`, `unsupported_type`,
  `invalid_value`, `usage`, `not_ready`, `auth`, `network`, `timeout`.

Discover what this version supports before relying on a command:

```bash
omaxerahs capabilities            # lists supported capability ids
omaxerahs <group> --help          # e.g. omaxerahs effects --help
```

## Workflows

A workflow is a capture job (region capture, full screen, recording, OCR, ...)
plus the tasks that run after the capture (save, copy, upload, show the After
Capture window, add image effects, annotate). Hotkeys for workflows on
Wayland/Hyprland are registered through the desktop GlobalShortcuts portal, so
`hotkey` is often `null` here even when the user presses a key such as F1.

```bash
omaxerahs workflow list                      # all workflows: id, name, job, afterCapture, imageEffects
omaxerahs workflow show "Region capture"     # one workflow, by id, unique id prefix, or name
omaxerahs workflow task-names                # valid after-capture task names
omaxerahs workflow tasks "Region capture" --add AddImageEffects --remove ShowAfterCaptureWindow
```

On Hyprland, XerahS can instead manage the keys itself (Settings > Hotkeys >
Hyprland Keybindings): it writes `~/.config/hypr/xerahs.lua`, and each key runs
`omaxerahs workflow run <id>`. Do not edit that file; it is regenerated.

### Run a workflow or start a capture

```bash
omaxerahs workflow run "Region capture"   # run it like its hotkey (in the running XerahS)
omaxerahs capture region                  # region | window | fullscreen | scroll
omaxerahs capture fullscreen --workflow "Full screen"
```

Both return at once with `"delivery":"running-instance"` (or `"started"` when
XerahS had to be launched); the capture itself happens in the app, using OmaSnap
on Hyprland when `omaxerahs capabilities` lists `capture.omasnap`, and the
workflow's normal after-capture tasks, upload and history.

Resolve the workflow first. When the user says "my screenshot" or names a key,
run `workflow list` and pick the capture workflow they mean (for a region
screenshot that is usually job `RectangleRegion`). If more than one could match,
ask, or use the `id` from the list. Names are matched case-insensitively; an
`ambiguous` error means use the id.

Common after-capture tasks: `ShowAfterCaptureWindow`, `AddImageEffects`,
`AnnotateMedia` (open the annotation editor), `CopyImageToClipboard`, `SaveImageToFile`, `UploadImageToHost`,
`PinToScreen`, `DoOCR`. Get the full list from `workflow task-names`.

## Image effects and ShareX presets

Each workflow has one image effects preset. It runs on every capture from that
workflow when the `AddImageEffects` task is on. XerahS imports its own `.xsie`
presets and ShareX `.sxie` presets.

```bash
# Replace the workflow's effects with a preset and turn effects on
omaxerahs effects import ~/Downloads/GoldBorder.sxie --workflow "Region capture"

# Import without turning effects on
omaxerahs effects import ~/Downloads/GoldBorder.sxie --workflow "Region capture" --no-enable

omaxerahs effects show    --workflow "Region capture"
omaxerahs effects enable  --workflow "Region capture"
omaxerahs effects disable --workflow "Region capture"   # keeps the preset
omaxerahs effects clear   --workflow "Region capture"   # removes effects and turns them off
```

After an import, report `workflow.imageEffects.effects` (the effects that will run)
and any `skippedEffects`. Skipped entries are ShareX effects with no XerahS
equivalent; tell the user rather than silently dropping them.

Example: "configure ~/Downloads/GoldBorder.sxie in XerahS":

1. `omaxerahs workflow list` and choose the screenshot workflow the user uses.
2. `omaxerahs effects import ~/Downloads/GoldBorder.sxie --workflow <id>`.
3. Confirm the effect list and whether `appNotified` is true.

## Image tools

Resize, convert or watermark image files without opening XerahS. Results go next to each
image unless `--output-folder` is given; `--name` sets the file name (`$filename` = source name).
Sources are never overwritten. The response lists `outputs` and any `failures`.

```bash
omaxerahs image resize shot.png --width 1280 --height 720 --mode fit      # fit | fill | stretch
omaxerahs image convert *.png --format webp --quality 85                  # png | jpeg | webp
omaxerahs image convert logo.png --format jpeg --background "#000000"     # fill for transparency
omaxerahs image watermark shot.png --text "© Mike" --position BottomRight --opacity 60
omaxerahs image watermark shot.png --image logo.png --image-scale 15 --position TopRight
omaxerahs image gif frame1.png frame2.png frame3.png -o demo.gif --delay 400   # --repeat N instead of looping forever
```

## Uploads

```bash
omaxerahs doctor              # is an image upload destination configured and ready?
omaxerahs upload <path>       # upload a file through the configured image destination; prints the URL
omaxerahs upload <path> --destination kovaforge   # any file to a named File destination, keeping its name
```

To publish a Markdown page with screenshots on KovaForge Docs: upload each image with
`omaxerahs upload`, put the returned `https://` URLs in the Markdown, then upload the `.md` with
`--destination kovaforge`. Check `omaxerahs capabilities` lists `upload.destination` first. Both
links are public to anyone who has them.

Upload destinations and credentials are configured in the XerahS app itself
(Destinations). If `doctor` reports not ready, tell the user to set one up
there; do not write uploader secrets yourself.

## Files and logs (read-only)

| What | Where |
|------|-------|
| Settings | `~/.config/xerahs/` (`WorkflowsConfig-*.json`, `ApplicationConfig.json`) |
| Logs | `~/.local/state/xerahs/Logs/yyyy-MM/XerahS-yyyyMMdd.log` and `XerahS-errors-yyyyMMdd.log` |
| History | `~/.local/state/xerahs/History/History.db` |

Read logs to diagnose problems. Change settings only through `omaxerahs`.

## Out of scope

- Uploader credentials and account sign-in: done by the user in the XerahS app.
- Omarchy's own screenshot tool (`omarchy capture ...`) is separate from XerahS;
  use the `omarchy` skill for it.
