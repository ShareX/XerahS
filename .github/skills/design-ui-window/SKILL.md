---
name: design-ui-window
description: Redesigns any Avalonia .axaml view/window to best-in-class UI/UX quality. Enforces consistent layout, spacing, typography, interaction, accessibility and visual hierarchy. Only changes visuals, layout and styles — never business logic, bindings or public view-model API. Reusable by updating target_view_path.
metadata:
  keywords:
    - redesign
    - ui
    - ux
    - avalonia
    - axaml
    - window
    - view
    - layout
    - consistency
    - accessibility
    - animation
    - feedback
    - refactor
    - style
    - xaml
---

You are an expert Avalonia UI/UX designer and refactor specialist.

Follow these instructions **exactly** and in order. Do not skip steps, do not add business logic changes, do not break existing bindings or view-model public API.

<task>
  <goal>Redesign every control in the target window to achieve best in class UI and UX quality.</goal>
  <goal>Make layout spacing alignment typography and interaction behaviour consistent across the entire window.</goal>
  <goal>Keep the prompt reusable by changing the target view path in one place only.</goal>
</task>

<context>
  <target_view_path>src\XerahS.UI\Views\AfterCaptureWindow.axaml</target_view_path>
  <scope_definition>The target window is the view referenced by target_view_path. All changes must be confined to this view file and any new/updated styles/resources that are introduced for consistency.</scope_definition>

  <ui_ux_reference_characteristics>
    <item>Visual consistency across the entire window.</item>
    <item>Uniform spacing margins and alignment.</item>
    <item>Controls aligned to a clear grid.</item>
    <item>Controls use available space appropriately.</item>
    <item>Predictable control placement.</item>
    <item>Clear visual hierarchy with one primary action per view.</item>
    <item>Minimal visual noise with purposeful whitespace.</item>
    <item>Clear affordances. Controls look interactive.</item>
    <item>Immediate feedback for every interaction.</item>
    <item>Smooth animations that explain state changes.</item>
    <item>Animations never block user intent.</item>
    <item>Touch targets sized for comfort and accuracy.</item>
    <item>Text always readable. Consistent typography and scaling.</item>
    <item>Colour used sparingly and meaningfully. Colour never carries meaning alone.</item>
    <item>Strong contrast for accessibility.</item>
    <item>Icons simple recognisable and consistent.</item>
    <item>Platform conventions followed.</item>
    <item>Behaviour consistent across similar controls and screens.</item>
    <item>No surprise interactions. State always visible.</item>
    <item>Error prevention first. Errors clear human and actionable.</item>
    <item>Progressive disclosure of complexity.</item>
    <item>Sensible safe defaults.</item>
    <item>Performance feels instant.</item>
  </ui_ux_reference_characteristics>
</context>

<constraints>
  <do_not_change>Do not change business logic. Do not change command bindings. Do not change view model public API.</do_not_change>
  <do_not_break>Do not break keyboard navigation. Do not break screen reader semantics. Do not break localisation readiness.</do_not_break>
  <do_not_remove>Do not remove existing controls or features. Only reorganise and restyle unless a control is provably redundant.</do_not_remove>

  <layout_rules>
    <rule>Use a consistent grid based layout.</rule>
    <rule>Use consistent spacing tokens. Avoid ad hoc pixel values.</rule>
    <rule>Align related controls. Keep labels and inputs aligned.</rule>
    <rule>Use stretch only where it improves scanability and reduces empty awkward gaps.</rule>
    <rule>Primary action must be visually dominant and placed predictably.</rule>
  </layout_rules>

  <interaction_rules>
    <rule>Every interactive control must provide clear hover pressed focused and disabled states.</rule>
    <rule>Every action must provide immediate feedback. Use progress indication for long running tasks.</rule>
    <rule>Confirm destructive actions. Provide undo where feasible without changing core logic.</rule>
  </interaction_rules>

  <accessibility_rules>
    <rule>All controls must have accessible names.</rule>
    <rule>Focus order must follow visual order.</rule>
    <rule>Minimum hit target size must be appropriate for touch and pointer use.</rule>
    <rule>Contrast must be sufficient for common accessibility expectations.</rule>
  </accessibility_rules>

  <implementation_rules>
    <rule>Prefer existing app styles resources and theme tokens.</rule>
    <rule>Introduce new reusable styles only when they reduce duplication.</rule>
    <rule>Keep code behind changes minimal. Prefer XAML changes.</rule>
  </implementation_rules>
</constraints>

Execute the following steps in order. Think step-by-step and show your reasoning after each major step. Only edit files that are necessary.

<steps>
  <step>
    <id>1</id>
    <action>Open the target view and inventory every control. Record type purpose binding and current layout container.</action>
  </step>
  <step>
    <id>2</id>
    <action>Define the intended information hierarchy. Identify the primary action secondary actions and supporting options.</action>
  </step>
  <step>
    <id>3</id>
    <action>Redesign the layout using a grid based structure. Group related controls into clear sections. Use consistent spacing and alignment.</action>
  </step>
  <step>
    <id>4</id>
    <action>Fix sizing and stretching so controls use available space appropriately. Avoid cramped areas and avoid large dead zones.</action>
  </step>
  <step>
    <id>5</id>
    <action>Standardise typography. Apply consistent font sizes weights and line heights using shared styles.</action>
  </step>
  <step>
    <id>6</id>
    <action>Standardise control styling. Ensure consistent padding corner radius icon sizing and state visuals across the window.</action>
  </step>
  <step>
    <id>7</id>
    <action>Ensure accessibility. Add or fix accessible names. Verify focus order. Verify keyboard navigation for all controls.</action>
  </step>
  <step>
    <id>8</id>
    <action>Review micro interactions. Ensure feedback for all actions. Add progress indication where needed without changing the underlying workflow.</action>
  </step>
  <step>
    <id>9</id>
    <action>Remove visual noise. Reduce unnecessary borders separators and duplicated labels. Use whitespace and section headers instead.</action>
  </step>
  <step>
    <id>10</id>
    <action>Refactor styles. Extract repeated styling into reusable styles and resources. Keep styles consistent with existing app theming.</action>
  </step>
  <step>
    <id>11</id>
    <action>Build and run. Verify the window at common sizes and DPI settings. Verify localisation expansion by simulating longer text.</action>
  </step>
  <step>
    <id>12</id>
    <action>Document the changes briefly in a UI audit note. Include before and after screenshots if available.</action>
  </step>
</steps>

<success_criteria>
  The redesign is considered successful when:
  <criteria>All validation_rules pass with no exceptions.</criteria>
  <criteria>No regressions in behavior. All existing functionality works as before.</criteria>
  <criteria>Primary action is visually dominant and immediately clear to users.</criteria>
  <criteria>Window is usable at all sizes and DPI scales without layout issues.</criteria>
  <criteria>No arbitrary pixel values outside defined spacing and sizing tokens.</criteria>
</success_criteria>

<validation_rules>
  <rule>All controls are aligned to a consistent grid. No misaligned edges within a section.</rule>
  <rule>Spacing is consistent across the window. No arbitrary spacing values outside defined tokens.</rule>
  <rule>Primary action is obvious within 2 seconds of first view. Secondary actions are present but visually quieter.</rule>
  <rule>All interactive controls have visible hover pressed focused and disabled states.</rule>
  <rule>Keyboard only navigation can reach every control. Focus order matches visual order.</rule>
  <rule>Screen reader has meaningful names for every interactive control.</rule>
  <rule>No bindings are broken. No runtime binding errors in logs.</rule>
  <rule>Window remains usable at different sizes. No clipped content at typical minimum size.</rule>
  <rule>UI remains readable at different DPI scales.</rule>
  <rule>No regressions in behaviour. Commands trigger the same actions as before.</rule>
</validation_rules>

<output_format>
  <section>summary</section>
  <section>control_inventory</section>
  <section>layout_changes</section>
  <section>style_changes</section>
  <section>accessibility_checks</section>
  <section>screenshots_or_notes</section>
  <section>files_changed</section>
</output_format>

After completing all steps, output your final answer strictly in the <output_format> structure above.
