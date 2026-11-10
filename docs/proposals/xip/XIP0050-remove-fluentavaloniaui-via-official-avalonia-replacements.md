# XIP0050 Remove FluentAvaloniaUI via Official Avalonia Replacements

**Status**: Complete
**Version**: v0.22.257

XIP0050: Remove FluentAvaloniaUI via Official Avalonia Replacements

## Summary

This XIP is a concrete migration plan to remove the third-party `FluentAvaloniaUI` dependency from `XerahS.UI` and replace its current usages with official Avalonia controls, local styles, and a small amount of first-party glue code.

The goal is not to redesign the app. The goal is to find the closest official Avalonia replacements, preserve current behavior, and then delete the third-party package only after parity is proven.

## Secondary Benefits

1. Standard `ContextMenu` becomes viable again where a plain context menu is sufficient.
2. The current lesson in [developers/lessons-learnt/general.md](../../../developers/lessons-learnt/general.md) that says `ContextMenu` does not render correctly with `FluentAvaloniaTheme` should become obsolete after this migration is complete.
3. `MenuFlyout` and `ContextFlyout` can then be reserved for cases that actually need flyout behavior, richer shared popup content, or button-attached flyouts rather than being required as a theme workaround.

## Why This XIP Exists

Current local evidence shows that `XerahS.UI` depends on `FluentAvaloniaUI` in several places:

1. package reference in [src/desktop/app/XerahS.UI/XerahS.UI.csproj](../../../src/desktop/app/XerahS.UI/XerahS.UI.csproj)
2. app theme root in [src/desktop/app/XerahS.UI/App.axaml](../../../src/desktop/app/XerahS.UI/App.axaml)
3. main shell navigation in [src/desktop/app/XerahS.UI/Views/MainWindow.axaml](../../../src/desktop/app/XerahS.UI/Views/MainWindow.axaml)
4. navigation logic in [src/desktop/app/XerahS.UI/Views/MainWindow.Navigation.cs](../../../src/desktop/app/XerahS.UI/Views/MainWindow.Navigation.cs)
5. dynamic capture submenu generation in [src/desktop/app/XerahS.UI/Helpers/NavigationItemsHelper.cs](../../../src/desktop/app/XerahS.UI/Helpers/NavigationItemsHelper.cs)
6. settings-style grouped rows in [src/desktop/app/XerahS.UI/Views/AboutView.axaml](../../../src/desktop/app/XerahS.UI/Views/AboutView.axaml)
7. icon controls in multiple views
8. one confirmation dialog in [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml.cs](../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml.cs)

There is also a dependency-version concern:

1. the repo pins Avalonia `11.3.12` in [Directory.Packages.props](../../../Directory.Packages.props)
2. the installed `FluentAvaloniaUI 2.4.1` package metadata targets Avalonia `11.2.5`

That version skew is a valid maintenance reason to remove the package if first-party replacements are practical.

## Goals

1. Remove `FluentAvaloniaUI` from `XerahS.UI`.
2. Keep `Avalonia.Themes.Fluent` as the supported official theme package.
3. Replace each current `FluentAvaloniaUI` usage with the closest official Avalonia control or a small first-party composite control.
4. Preserve current information architecture, menu structure, and navigation behavior.
5. Keep the main shell visually close to the current app.
6. End with a clean codebase that has no `FluentAvalonia.*` namespaces, XAML namespaces, or package references in `XerahS.UI`.

## Non-Goals

1. No redesign of app navigation structure.
2. No switch to `SimpleTheme`, `Material.Avalonia`, or another theme system.
3. No rewrite of page routing architecture beyond what is required to leave `NavigationView`.
4. No speculative UI cleanup unrelated to package removal.

## Execution Rules

1. Implementation for this XIP must happen on a branch other than `develop`. Do not implement this work directly on `develop`.
2. Git commits must be made frequently as work progresses. Prefer small, logical commits for each completed step rather than a single large commit at the end.

## Current Dependency Inventory

### Direct control/theme usages

1. `FluentAvaloniaTheme` in [src/desktop/app/XerahS.UI/App.axaml](../../../src/desktop/app/XerahS.UI/App.axaml)
2. `NavigationView` and `NavigationViewItem` in [src/desktop/app/XerahS.UI/Views/MainWindow.axaml](../../../src/desktop/app/XerahS.UI/Views/MainWindow.axaml)
3. `FontIconSource` in [src/desktop/app/XerahS.UI/Views/MainWindow.axaml](../../../src/desktop/app/XerahS.UI/Views/MainWindow.axaml)
4. `SettingsExpander` and `SettingsExpanderItem` in [src/desktop/app/XerahS.UI/Views/AboutView.axaml](../../../src/desktop/app/XerahS.UI/Views/AboutView.axaml)
5. `FontIcon` in:
   - [src/desktop/app/XerahS.UI/Views/SettingsView.axaml](../../../../src/desktop/app/XerahS.UI/Views/SettingsView.axaml)
   - [src/desktop/app/XerahS.UI/Views/ToolsView.axaml](../../../src/desktop/app/XerahS.UI/Views/ToolsView.axaml)
   - [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml](../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml)
   - other icon-bearing views
6. `CommandBarSeparator` in [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml](../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml)
7. `ContentDialog` in [src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml.cs](../../../src/desktop/app/XerahS.UI/Views/WorkflowsView.axaml.cs)

### Behaviors that must survive the migration

1. always-open left pane shell
2. nested navigation items
3. dynamic `Capture` submenu items from workflows
4. action-only items that re-trigger on repeated click without relying on selection changes
5. page-host swapping inside `ContentFrame`
6. settings-style grouped rows in `AboutView`
7. icon-font-based rendering using existing glyph constants
8. modal confirmation for workflow operations

## Closest Official Avalonia Replacement Matrix

| Current FluentAvalonia usage | Current role in XerahS | Primary official replacement | Secondary fallback | Decision criteria |
| --- | --- | --- | --- | --- |
| `NavigationView` | Main shell container with left navigation and content host | `SplitView` + `TreeView` + `TransitioningContentControl` | `Grid` + `ScrollViewer` + custom nested `Expander`/`ItemsControl` | Hierarchical items, stable selection, easy styling, keyboard support, low code complexity |
| `NavigationViewItem` | Hierarchical nav item with icon, tag, children, selection | `TreeViewItem` via `TreeDataTemplate` and a `NavigationNode` model | Local `NavigationPaneItem` user control | Child items, selected state, action-only node support |
| `FontIconSource` | Nav icons | `TextBlock` using icon font | `PathIcon` | Lowest migration cost and reuse of existing glyph constants |
| `FontIcon` | Page/header icons | `TextBlock` using icon font | `PathIcon` | Keep glyph constants and visual parity |
| `SettingsExpander` | Grouped settings-like sections | `Expander` with custom header template | local `SettingsSection` control | Header icon, row layout, expand/collapse visuals |
| `SettingsExpanderItem` | Clickable row with trailing action icon | `Button` or `Border` + `Grid` row template | local `SettingsRow` control | Click affordance, alignment, reuse |
| `CommandBarSeparator` | Toolbar separator | `Separator` with toolbar style | styled `Border` | Visual parity only |
| `ContentDialog` | One confirmation dialog | modal `Window.ShowDialog<TResult>` | existing modal overlay host in `MainWindow` | Simplicity, testability, no global infra |
| `FluentAvaloniaTheme` | App-level theme/resource source | official `FluentTheme` + local compatibility resources and control themes | official `FluentTheme` + more aggressive local restyling | Resource compatibility, startup stability, smallest regression surface |

## Workstream A: Produce a complete parity checklist before coding

Before replacing anything, capture what the current UI is relying on.

### A1. Inventory all remaining package usages

Enumerate all of the following under `src/desktop/app/XerahS.UI`:

1. `using FluentAvalonia`
2. `xmlns:ui="using:FluentAvalonia.UI.Controls"`
3. `xmlns:sty="using:FluentAvalonia.Styling"`
4. `FluentAvaloniaTheme`
5. `NavigationView`
6. `SettingsExpander`
7. `FontIcon`
8. `FontIconSource`
9. `CommandBarSeparator`
10. `ContentDialog`

### A2. Inventory resource-key dependencies

Audit `XerahS.UI` XAML for theme keys that may currently come from `FluentAvaloniaTheme`.

At minimum, verify the source and replacement plan for keys/styles such as:

1. `SolidBackgroundFillColorBaseBrush`
2. `TextFillColorSecondaryBrush`
3. `AccentFillColorDefaultBrush`
4. `SurfaceStrokeColorDefaultBrush`
5. `TitleTextBlockStyle`
6. `BodyTextBlockStyle`
7. `CaptionTextBlockStyle`
8. `SubtitleTextBlockStyle`
9. `CardBorderTheme`

### A3. Record parity targets

For each migrated area, record expected behavior before edits:

1. what the user can click
2. what expands and collapses
3. what selects
4. what opens a page
5. what launches an action
6. what styles must remain recognizable

This checklist becomes the acceptance contract for the migration.

### A4. Record flyout and context-menu cleanup opportunities

As part of the audit, identify where `MenuFlyout` or `ContextFlyout` is being used only because of the current `FluentAvaloniaTheme` limitation documented in [developers/lessons-learnt/general.md](../../../developers/lessons-learnt/general.md).

For each occurrence, classify it as one of:

1. must remain a flyout because it is attached to a button or uses richer popup behavior
2. can stay as a flyout because reuse/shared binding behavior is still useful
3. can be simplified to a standard `ContextMenu` after migration to official Avalonia theme resources

## Workstream B: Prototype the shell and choose the replacement

The highest-risk change is the main shell. Do not commit to package removal before the shell candidate is proven.

### B1. Build the primary shell prototype

Prototype:

1. `SplitView` as shell frame
2. left pane with `TreeView`
3. `TransitioningContentControl` or `ContentControl` as page host
4. `NavigationNode` view model backing hierarchical items

### B2. Reproduce these exact shell requirements

The prototype must support:

1. static sections like `Recording`, `Editor`, `History`, `Workflows`, `About`
2. nested sections like `Upload`, `Tools`, and `Settings`
3. dynamic `Capture` child items from workflows
4. selected-item navigation for page nodes
5. explicit invoke handling for action-only nodes
6. always-open left pane
7. icon rendering using existing glyph constants

### B3. Use fallback only if the primary prototype fails a hard requirement

Fallback shell:

1. `Grid` or `SplitView`
2. `ScrollViewer`
3. custom `ItemsControl` hierarchy
4. nested `Expander`
5. local selected-row logic

Fallback is allowed only if `TreeView` styling or behavior proves too costly relative to the parity goal.

### B4. Shell selection criteria

Pick the implementation that gives the best balance of:

1. behavioral parity
2. lowest code volume
3. clearest styling model
4. lowest event-handling complexity
5. best keyboard and focus behavior

## Workstream C: Replace shell code with first-party navigation model

Once the shell candidate is selected:

1. introduce a reusable `NavigationNode` model
2. move nav hierarchy into a first-party model or local declarative structure
3. replace `NavigationView` event handling in [src/desktop/app/XerahS.UI/Views/MainWindow.Navigation.cs](../../../src/desktop/app/XerahS.UI/Views/MainWindow.Navigation.cs)
4. preserve tag-driven routing so the rest of `HandleNavigationTag` remains stable
5. preserve `NavigationItemsHelper` behavior for dynamic capture items

### C1. Action-only navigation requirement

The replacement must preserve a key current behavior:

1. some nodes trigger actions immediately
2. those nodes must be re-invokable even when the page selection does not change

This means the replacement cannot rely on selection alone. It must have explicit invoke handling for action nodes.

## Workstream D: Replace small control usages

After the shell is stable, replace the lower-risk package usages.

### D1. Replace icon controls

Replace `FontIcon` and `FontIconSource` with:

1. `TextBlock`
2. existing icon font
3. existing glyph constants

This is the lowest-risk path and keeps icon authoring consistent across the app.

### D2. Replace settings sections

Replace `SettingsExpander` and `SettingsExpanderItem` with:

1. `Expander`
2. custom header template
3. styled row buttons or borders
4. reusable local styles for trailing action icons and row spacing

### D3. Replace command separators

Replace `CommandBarSeparator` with:

1. `Separator`
2. local toolbar separator style

### D4. Replace confirmation dialog

Replace `ContentDialog` with:

1. a small modal `Window`
2. `ShowDialog<bool>` or equivalent result mapping

If a reusable dialog pattern is needed later, build that after the first replacement works.

## Workstream E: Replace theme root and add local resource compatibility

Do not remove the theme root early. Replace controls first, then resolve theme dependencies.

### E1. Switch to official theme root

Replace:

1. `sty:FluentAvaloniaTheme`

With:

1. official `FluentTheme`

### E2. Add local compatibility resources if needed

If official `FluentTheme` does not expose every resource/style key currently used by `XerahS.UI`, add first-party compatibility aliases or styles in local theme dictionaries.

Expected outputs:

1. local brush aliases
2. local text styles
3. local card/control themes

This keeps the app visually stable without keeping the third-party package.

## Workstream F: Remove package and namespaces

Only after workstreams A through E are complete:

1. remove `FluentAvaloniaUI` from [Directory.Packages.props](../../../Directory.Packages.props)
2. remove `PackageReference Include="FluentAvaloniaUI"` from [src/desktop/app/XerahS.UI/XerahS.UI.csproj](../../../src/desktop/app/XerahS.UI/XerahS.UI.csproj)
3. remove all `using FluentAvalonia.*`
4. remove all `xmlns:ui="using:FluentAvalonia.UI.Controls"`
5. remove all `xmlns:sty="using:FluentAvalonia.Styling"`

The build should compile with no source dependency on the package before the reference is deleted.

## Implementation Order

1. Workstream A: audit and parity checklist
2. Workstream B: shell prototype and replacement decision
3. Workstream C: first-party shell implementation
4. Workstream D: remaining small control replacements
5. Workstream E: theme replacement and local compatibility layer
6. Workstream F: package removal

This order minimizes churn and avoids deleting the package before a working replacement exists.

## Acceptance Criteria

The migration is complete only when all of the following are true:

1. `FluentAvaloniaUI` is no longer referenced in [Directory.Packages.props](../../../Directory.Packages.props).
2. `XerahS.UI.csproj` contains no `PackageReference` to `FluentAvaloniaUI`.
3. `src/desktop/app/XerahS.UI` contains no:
   - `using FluentAvalonia`
   - `xmlns:ui="using:FluentAvalonia.UI.Controls"`
   - `xmlns:sty="using:FluentAvalonia.Styling"`
4. the app uses official `FluentTheme`
5. the main shell still supports:
   - nested navigation
   - dynamic capture items
   - action-only invocations
   - page navigation
6. `AboutView` still presents grouped expandable sections with clickable rows
7. workflow confirmation dialog still works
8. `dotnet build` passes with `0` errors and warnings treated as errors
9. the `ContextMenu vs. ContextFlyout` lesson in [developers/lessons-learnt/general.md](../../../developers/lessons-learnt/general.md) is reviewed and either removed or rewritten once the migration proves that `ContextMenu` no longer requires a FluentAvalonia-specific workaround

## Verification Matrix

### Build verification

1. `dotnet build src\\desktop\\app\\XerahS.UI\\XerahS.UI.csproj -m:1`
2. `dotnet build src\\desktop\\XerahS.sln -m:1`

### Manual verification

1. launch app and verify startup without resource-resolution exceptions
2. verify main navigation renders and left pane is usable
3. verify `Capture` dynamic submenu population
4. verify repeated invocation for action-only items
5. verify page switching for `Editor`, `Recording`, `History`, `Workflows`, `Settings`, `About`
6. verify `AboutView` sections expand and row clicks still open links
7. verify workflow delete/reset confirmation still appears and returns the correct result
8. verify icon glyphs still render correctly in navigation and page headers

## Risks and Mitigations

1. Risk: `TreeView` may require more styling work than expected to feel like the current shell.
   Mitigation: prototype before committing to the implementation; allow a custom hierarchical pane fallback.
2. Risk: current theme resource keys may not all come from official Avalonia Fluent.
   Mitigation: audit first; add local compatibility resources instead of keeping the package.
3. Risk: action-only nav behavior may regress if implemented as selection-only.
   Mitigation: make invoke semantics a hard acceptance criterion for the shell workstream.
4. Risk: the migration may drift into a redesign.
   Mitigation: keep current routing tags, content host, and information architecture intact.

## Recommended Deliverables

1. updated `XIP0050`
2. first-party navigation shell implementation in `XerahS.UI`
3. local theme compatibility resources if required
4. no `FluentAvaloniaUI` package reference
5. optional follow-up audit doc if theme compatibility turns out to be larger than expected

## Official Sources Used For This Plan

1. Avalonia themes overview
   - <https://docs.avaloniaui.net/docs/basics/user-interface/styling/themes/>
2. Avalonia `SplitView`
   - <https://docs.avaloniaui.net/docs/reference/controls/splitview>
3. Avalonia `TreeView`
   - <https://docs.avaloniaui.net/docs/reference/controls/treeview-1>
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_TreeView>
4. Avalonia `Expander`
   - <https://docs.avaloniaui.net/docs/reference/controls/expander>
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_Expander>
5. Avalonia `TransitioningContentControl`
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_TransitioningContentControl>
6. Avalonia icon guidance
   - <https://docs.avaloniaui.net/docs/guides/graphics-and-animation/how-to-use-icons>
7. Avalonia `PathIcon`
   - <https://docs.avaloniaui.net/docs/reference/controls/path-icon>
8. Avalonia `Separator`
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_Separator>
9. Avalonia `Window` and `ShowDialog`
   - <https://api-docs.avaloniaui.net/docs/T_Avalonia_Controls_Window>
   - <https://api-docs.avaloniaui.net/docs/M_Avalonia_Controls_Window_ShowDialog__1>
10. Avalonia `ContextMenu`
    - <https://docs.avaloniaui.net/docs/reference/controls/contextmenu>
    - key finding: official Avalonia presents `ContextMenu` as a standard control and `ContextFlyout` as an alternative for richer or sharable UI, not as a mandatory workaround
11. Avalonia `MenuFlyout`
    - <https://docs.avaloniaui.net/docs/reference/controls/menu-flyout>
    - key finding: official Avalonia documents `MenuFlyout` as an alternative to context menus, which supports treating current forced `MenuFlyout` usage as a FluentAvalonia-specific workaround rather than a permanent architectural requirement
12. Avalonia controls namespace inventory
    - <https://api-docs.avaloniaui.net/docs/N_Avalonia_Controls>