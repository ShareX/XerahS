# Region selection dimming

Ported from [CubityFirst/ShareX PR #5](https://github.com/CubityFirst/ShareX/pull/5),
including its final fixes at commit `0c5e28387415b1708c5b9ce25fdaa88579219625`.

In task settings under Capture → Region capture, enable **Dim all, except the
current selection** to keep hovered windows and controls dimmed while retaining
their outlines. A region being dragged, moved, resized, or retained with Quick
capture disabled stays bright. The option is disabled by default and saved with
the task's capture settings. **Background dim strength (%)** controls the dimming.

XerahS starts a drag with an empty rectangle and only turns a clicked window into
a selection on release. This already avoids the source implementation's bright
flash on press, so no separate pending-selection state is needed.

The setting applies to region capture. The color picker and ruler retain their
own overlay options. Existing captures and Statistics history are unaffected.

Verification covers rendered pixel brightness for hover, press, drag, release,
move, resize, and clicking a window. Settings tests cover serialization, the
disabled default, dim strength clamping, and the existing `UseDimming` flag.
These checks and the Statistics regressions pass together (31 tests), and the
desktop solution builds successfully. Task settings text follows XerahS's
existing English UI; the source PR's translated labels are not included.
