---
name: design-ui-window
description: Redesign a XerahS Avalonia page, dialog, or tool window when visual changes are requested.
---

# Redesign a XerahS window or page

The target is the view the user names. Keep changes in that view plus any shared styles or resources that are genuinely needed for consistency. Do not change business logic, command bindings, or view-model public API. Do not remove controls unless they are provably redundant. Also follow [avalonia-guidelines](../avalonia-guidelines/SKILL.md).

## XerahS surface and theme rules

- Identify the host type first: PageView, SurfaceWindow, ordinary dialog/window, or transparent overlay. Overlays that are intentionally transparent keep no painted surface.
- Black areas or gutters usually mean an unpainted layout container is falling through to the Fluent host surface. Fix the first painted surface before restyling inner controls.
- For tool windows and dialogs, replace a transparent root `Grid`/`StackPanel` with outer `Margin` by a root `Border` with `Background="{DynamicResource SolidBackgroundFillColorSecondaryBrush}"` and `Padding`, then nest the layout inside it. Routed pages use the shared host defaults first and add local root painting only if a gutter still shows.
- Use shared theme brushes (`SolidBackgroundFillColorSecondaryBrush`, `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `TextFillColorPrimaryBrush`, `TextFillColorSecondaryBrush`). Never hardcode dark colours.
- The accent is the OS system accent through `SystemAccentColor` / `SystemAccentColorLight1` / `SystemAccentColorDark1` in `src/desktop/app/XerahS.UI/Themes/ThemeResources.axaml`. Never reference `ShareX.Color.Accent.Start` or `ShareX.Color.Accent.End` in new brushes.
- Buttons are accent by default app-wide through `ThemeResources.axaml`. Do not add `Classes="accent"`. Use the opt-out classes `NoAccent`, `SettingsRow`, `ColorSwatchButton` or `DarkButton` only when a button truly needs a different look. Being a secondary action is not a reason to use `NoAccent`.
- Button content is already centred by a universal style. Do not add `HorizontalContentAlignment`/`VerticalContentAlignment` to buttons. The exception is `Button.SettingsRow`, which already stretches.
- Do not style scrollbar thumbs. XerahS keeps Fluent's neutral colours and disables auto-hide app-wide.
- If previews or control internals still render black after the root is fixed, or the same issue spans many windows, fix the shared theme/resource mapping instead of patching children one by one.
- During visual work keep `#ElementName` paths on `{Binding ...}`. Never rewrite them to `{ReflectionBinding ...}`.

## Workflow

1. Inventory every control: type, purpose, binding, container, host type, and whether the first painted surface is explicit.
2. Set the hierarchy: one visually dominant primary action, quieter secondary actions, grouped supporting options.
3. Rework the layout on a consistent grid with shared spacing tokens. Remove ad hoc pixel values, dead zones, cramped areas, and redundant borders or labels.
4. Standardise typography and control styling through shared styles. Extract repeated styling only when it reduces duplication.
5. Check accessibility: accessible names, focus order that matches visual order, keyboard reach, hit-target size, and contrast.
6. Build, then check the view at common sizes and DPI scales, with longer text, and with no runtime binding errors in the logs.

Report a short summary, the layout, style and accessibility changes, the files changed, and screenshots if available.
