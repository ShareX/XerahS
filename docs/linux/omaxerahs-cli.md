# omaxerahs: the XerahS command line

`omaxerahs` ships with native Linux packages (`/usr/bin/omaxerahs`) next to `xerahs`.
It is the scriptable surface of XerahS for shell scripts, the Omarchy bar plugin,
and AI coding agents.

## Contract

- Every command prints exactly one JSON object to stdout: `{"schemaVersion":1,"ok":true,...}`
  or `{"schemaVersion":1,"ok":false,"error":{"code":"...","message":"..."}}` with exit code 1.
- Human hints go to stderr; `--json` suppresses them.
- `omaxerahs capabilities` lists the capability ids this build supports.

## Commands

| Command | Purpose |
|---------|---------|
| `doctor` | Report whether an image upload destination is ready. |
| `upload <path> [--url-only]` | Upload a file through the configured image destination. `--url-only` prints just the URL (OmaSnap pin Upload button). |
| `upload <path> --destination <destination>` | Upload any file to a named File destination, keeping its name: an instance id, a unique id prefix (6+ characters), a display name, or a provider id such as `kovaforge`. Capability `upload.destination`. |
| `workflow list` / `workflow show <workflow>` | Inspect workflows (id, job, after-capture tasks, image effects). |
| `workflow tasks <workflow> --add <task> --remove <task>` | Turn after-capture tasks on or off. |
| `workflow task-names` | List valid after-capture task names. |
| `workflow run <workflow>` | Run a workflow in the running XerahS like its hotkey (starts XerahS if needed). Used by Hyprland keybindings. |
| `capture <region\|window\|fullscreen\|scroll> [--workflow <workflow>]` | Start a capture through the same path as the hotkeys (OmaSnap on Hyprland). |
| `effects import <file> --workflow <workflow> [--no-enable]` | Replace a workflow's image effects with a `.xsie` or ShareX `.sxie` preset. |
| `effects show / enable / disable / clear --workflow <workflow>` | Inspect or change a workflow's image effects. |
| `image resize / convert / watermark <files...>` | Batch image tools (same engine as the Image Resizer, Converter and Watermark windows). |
| `image gif <frames...> -o out.gif` | Animated GIF from images (same engine as the Animated GIF Maker). |
| `skill install / uninstall / path` | Manage the `xerahs` agent skill. |

`<workflow>` is a workflow id, a unique id prefix (6+ characters), or a workflow name.

Commands that change settings save `WorkflowsConfig` and send `--reload-workflows` to a running
XerahS over the single-instance channel, so the app picks up the change without raising its
window and does not overwrite it later. The response field `appNotified` reports whether a running
instance was reached; when XerahS is not running the change applies on its next start.

`workflow run` and `capture` send `--run-workflow <id>` over the same channel. The app runs the
workflow without raising its window; the command returns immediately (`"delivery"` is
`running-instance`, or `started` when XerahS was not running). Ids (8+ hex characters) are relayed
without loading settings, so a Hyprland key press stays fast.

Capability ids added in 0.31.0: `workflow.run`, `capture`, and `capture.omasnap` when the OmaSnap
host-mode probe succeeds (Hyprland only). `doctor` also reports `history.ok` (the native SQLite
provider loads). See [omarchy.md](omarchy.md).

## Agent skill

`omaxerahs skill install` writes the bundled `xerahs` skill to
`$XDG_DATA_HOME/xerahs/agents/skills/xerahs` and symlinks it into `~/.agents/skills` and into the
skill directories of agents that are set up (`~/.claude`, `~/.codex`, `~/.hermes` and its profiles,
`~/.config/opencode`). This mirrors how Omarchy links its own skills. Existing `xerahs` skills that
were not created by `omaxerahs` are left untouched, and `skill uninstall` removes only its own links.

On Omarchy, XerahS runs `omaxerahs skill install` in the background at startup, so the agent launched
by `omarchy agent` always has a skill that matches the installed XerahS. Example request an agent can
then fulfil: "configure ~/Downloads/GoldBorder.sxie in XerahS" becomes
`omaxerahs effects import ~/Downloads/GoldBorder.sxie --workflow "Region capture"`.
