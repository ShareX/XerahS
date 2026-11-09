# XIP0060 First-Run Onboarding Wizard

**Status**: Implemented (reduced scope — do not re-implement removed features)  
**Version**: v0.22.257

> **Agent notice (2026-06-28):** Several sections below describe features that were **removed from the shipping wizard** and **will not be implemented**. Do not re-add them from this document. See [Scope reduction](#scope-reduction-2026-06-28) before implementing anything from the historical design.

## Scope reduction (2026-06-28)

The live onboarding wizard was simplified. The following XIP0060 features are **explicitly out of scope** and **must not be reintroduced** by agents reading this proposal:

| Removed feature | Reason |
|---|---|
| Display language selection (welcome step) | App language is not configured during onboarding; removed to reduce friction. |
| OCR language pack step | OCR is configured in Settings after first run, not during onboarding. |
| ShareX configuration import (onboarding upload step) | Legacy import remains in Destination Settings only; not part of the wizard. |

**Current wizard steps (5):** Welcome (theme) → Workflows → Hotkeys → Destinations → Ready check. The last step reports the real state of the capture folder, hotkey registration, macOS permissions, Wayland and FFmpeg, and offers **Finish and take my first capture** (v0.33.6).

**Created**: 2026-04-06  
**Updated**: 2026-06-28  
**Area**: Desktop | UX | Core  
**Goal**: Deliver a polished, multi-step onboarding experience that guides first-time users through essential configuration, dramatically improving activation rates and time-to-value.

---

## Summary

This proposal introduces a first-run onboarding wizard for XerahS that activates when `IsFirstTimeRun` is true. The **current** wizard covers save location, hotkeys, and upload destination (see [Scope reduction](#scope-reduction-2026-06-28) for removed steps). By replacing the minimal first-run experience with an intentional step-by-step flow, we reduce user drop-off and misconfiguration.

---

## Motivation

### The Problem

Current XerahS first-run behavior is minimal: users install the application and are dropped directly into the main interface with no guidance. The only first-run affordance is a one-time legacy ShareX config import button in DestinationSettings. This creates several critical UX gaps:

1. **Silent Failures**: Users often don't realize captures are being saved to `%TEMP%` until they can't find their screenshots later.

2. **Hotkey Conflicts**: Default hotkeys frequently clash with system or application shortcuts, leaving users wondering why "nothing happens" when they press Print Screen.

3. **Upload Confusion**: New users don't understand the distinction between local saves and cloud uploads, leading to accidental public uploads or frustration when sharing links don't work.

4. **OCR Friction**: The OCR feature appears broken out-of-the-box because no language packs are downloaded by default, with no indication of how to fix it.

5. **ShareX Migration Blindness**: Users coming from ShareX may not notice the import button or understand what will be imported.

### The Cost

Analytics from comparable tools (ShareX, Greenshot, Flameshot) suggest that **40-60% of new users never capture a single screenshot** in their first session. Of those who do, a significant portion encounter configuration issues that lead to negative reviews or uninstalls within 24 hours.

### The Opportunity

A well-designed onboarding wizard can:

- **Increase activation rate** by ensuring every user completes at least one successful capture in their first session
- **Reduce support burden** by surfacing configuration options proactively rather than reactively
- **Improve retention** by establishing correct defaults that match user intent
- **Differentiate XerahS** as the polished, user-friendly alternative to legacy tools

### Prior Art

**Yoink** (macOS) provides an excellent reference implementation with its `SetupWizard` and `InstallWizard`:
- Clean, single-purpose steps with clear progress indication
- Smart defaults that can be accepted with one click
- Ability to skip individual steps or the entire wizard
- Contextual help without overwhelming the user
- Completion celebration that leaves users feeling accomplished

---

## Detailed Design

### Wizard Architecture

The onboarding wizard will be implemented as a modal dialog flow using Avalonia's `TransitioningContentControl` for smooth step transitions. The architecture follows a state machine pattern with the following characteristics:

```
┌─────────────────────────────────────────────────────────────┐
│                    OnboardingWizard                         │
│  ┌─────────────────────────────────────────────────────┐   │
│  │         TransitioningContentControl                  │   │
│  │  ┌─────────┐   ┌─────────┐   ┌─────────┐            │   │
│  │  │ Step 1  │ → │ Step 2  │ → │ Step 3  │ → ...      │   │
│  │  │ Language│   │ Save    │   │ Hotkey  │            │   │
│  │  └─────────┘   └─────────┘   └─────────┘            │   │
│  └─────────────────────────────────────────────────────┘   │
│  [Back]  [Skip]                    [Next] / [Finish]       │
└─────────────────────────────────────────────────────────────┘
```

### Navigation Rules

1. **Back Button**: Enabled from Step 2 onwards. Returns to previous step, preserving user selections.
2. **Next Button**: Advances to next step. Validates current step before proceeding.
3. **Skip Button**: Available on every step. Skips current step and advances. Can skip entire wizard via "Skip All".
4. **Finish Button**: Appears on final step. Completes wizard and persists all settings.
5. **Progress Indicator**: Visual progress bar or step dots showing current position (e.g., "Step 3 of 5").

### Step Persistence

All selections are held in a transient `OnboardingState` object until the wizard completes. If the user cancels mid-wizard, no changes are persisted. On completion, all settings are committed atomically via `SettingsBase.SaveAsync()`.

---

## Proposed Steps

### Step 1: Welcome ~~& Language Selection~~ (language selection removed)

> **Removed:** Display language selection was removed on 2026-06-28. The welcome step is informational only.

**Purpose**: Set the tone and introduce the wizard.

**Content**:
- Welcome message and brief overview of the remaining steps
- ~~Language dropdown~~ (removed — do not implement)

**Smart Defaults**:
- ~~Language pre-selected from `CultureInfo.CurrentUICulture`~~ (removed)

**Validation**:
- No input required; user proceeds with Next

**Edge Cases**:
- ~~RTL languages~~ (removed with language step)

---

### Step 2: Default Save Location

**Purpose**: Prevent the "where did my screenshots go?" problem.

**Content**:
- Directory picker with browse button
- Quick-select buttons for common locations:
  - Pictures/Screenshots (default)
  - Desktop
  - Documents
  - Custom...
- Checkbox: "Create subfolder with today's date" (default: true)
- Preview of path: `~/Pictures/Screenshots/2026-04-06/`

**Smart Defaults**:
- Primary: `Environment.GetFolderPath(SpecialFolder.MyPictures)/Screenshots`
- Fallback: User profile root if Pictures unavailable

**Validation**:
- Path must be writable (test write on Next)
- Create directory if it doesn't exist
- Warn if path is a network drive or removable media

**Edge Cases**:
- Path becomes unavailable after selection (handled at capture time, not here)
- Cloud sync folders (OneDrive, Dropbox): Show informational tooltip
- Long paths (>260 chars on Windows): Warn about potential issues

---

### Step 3: Hotkey Configuration

**Purpose**: Ensure users can actually trigger captures without conflicts.

**Content**:
- Visual hotkey recorder for primary capture hotkey
- Default suggestion: `Print Screen` (or `Cmd+Shift+5` on macOS)
- Conflict detection: Check against system hotkeys and common apps
- "Test your hotkey" button that triggers a test capture
- Secondary hotkeys (collapsed by default):
  - Region capture
  - Window capture
  - Fullscreen capture
  - Screen recording

**Smart Defaults**:
- Primary: `Print Screen`
- Region: `Ctrl+Print Screen` (or `Cmd+Shift+4` on macOS)
- Window: `Alt+Print Screen` (or `Cmd+Shift+4+Space` on macOS)

**Validation**:
- Detect conflicts with known system shortcuts
- Warn if hotkey is already bound by XerahS or detected common apps
- Require at least one capture hotkey to be set (can be same as default)

**Conflict Resolution UI**:
```
⚠️ Conflict Detected
"Print Screen" is currently bound to Windows Snipping Tool.
[X] Disable Windows Snipping Tool shortcut
[ ] Choose different hotkey
```

**Edge Cases**:
- Media keys: Filter out non-modifier keys that can't be used as hotkeys
- Accessibility: Ensure hotkey recorder is keyboard-navigable
- Wayland: Warn that global hotkeys require specific desktop environment support

---

### Step 4: Upload Destination

**Purpose**: Clarify the local vs. cloud distinction and set up sharing.

**Content**:
- Radio button selection:
  - **Local only** (default for privacy-conscious users)
  - **Imgur** (anonymous)
  - **Imgur** (authenticated)
  - **Custom uploader** (from XIP0024)
  - **More options...** (opens full DestinationSettings)
- For authenticated options: "Connect Account" button triggering OAuth flow
- Privacy note: "Your images are only uploaded when you explicitly choose 'Upload'"

**Smart Defaults**:
- Default: Local only (opt-in for uploads)
- If ShareX config imported with uploaders: Pre-select primary uploader

**Validation**:
- Test connection for authenticated uploaders before completing
- Anonymous Imgur: No validation needed (works without auth)

**Edge Cases**:
- No internet connection: Skip or show "Configure later" option
- OAuth flow interruption: Allow retry or skip
- Enterprise environments: Detect proxy settings, offer manual configuration

---

### Step 5: OCR Language Setup (removed from wizard)

> **Removed on 2026-06-28.** OCR language packs are configured in Settings, not during onboarding. Do not re-add this step.

**Purpose**: ~~Ensure OCR works out-of-the-box for the user's language.~~ (historical)

**Content**:
- Multi-select list of available OCR languages
- Pre-selected: Language from Step 1 + English
- Download size indicator per language (e.g., "English ~25 MB")
- "Download now" vs "Download in background" options
- Preview: "You'll be able to extract text from screenshots using your hotkey + T"

**Smart Defaults**:
- Primary: Match Step 1 language selection
- Secondary: English (as fallback)
- Only select, don't download until wizard completes (or background)

**Validation**:
- At least one language must be selected
- Warn if selected languages exceed ~200 MB total

**Edge Cases**:
- Low disk space: Warn and offer to skip
- Slow connection: Offer background download with notification on completion
- Language pack download failure: Retry logic, fallback to manual download instructions

---

### Step 6: Completion & First Capture

**Purpose**: Celebrate completion and drive immediate activation.

**Content**:
- Success animation/illustration
- Summary of configured settings (expandable)
- "Take your first screenshot" button (triggers region capture)
- Checkbox: "Show tips on startup" (default: true for first week)
- Link: "Open full settings" for power users

**Post-Wizard Actions**:
- Persist all settings to `ApplicationConfig.json`
- Call `MarkFirstTimeRunCompleted()`
- If "Take first screenshot" clicked: Trigger region capture immediately
- Schedule OCR language downloads if background option selected

---

## Technical Approach

### Integration with Existing Infrastructure

#### IsFirstTimeRun Detection

The wizard hooks into the existing `SettingsBase.IsFirstTimeRun` mechanism:

```csharp
// In App.axaml.cs or MainWindowViewModel
protected override async void OnInitialized()
{
    await base.OnInitialized();
    
    if (Settings.IsFirstTimeRun)
    {
        var wizard = new OnboardingWizard();
        var result = await wizard.ShowDialog<OnboardingResult>(this);
        
        if (result.Completed || result.Skipped)
        {
            Settings.MarkFirstTimeRunCompleted();
            await Settings.SaveAsync();
        }
    }
}
```

#### Settings Persistence

The wizard uses a transient state object that maps to existing settings:

```csharp
public class OnboardingState
{
    // Step 1: Language
    public string SelectedLanguage { get; set; } = "en";
    
    // Step 2: Save Location
    public string ScreenshotsFolder { get; set; }
    public bool CreateDateSubfolders { get; set; } = true;
    
    // Step 3: Hotkeys
    public HotkeyConfig PrimaryCaptureHotkey { get; set; }
    public List<HotkeyConfig> AdditionalHotkeys { get; set; } = new();
    
    // Step 4: Upload
    public UploaderConfig SelectedUploader { get; set; }
    
    // Step 5: OCR
    public List<string> SelectedOcrLanguages { get; set; } = new();
    public bool DownloadOcrInBackground { get; set; }
}
```

On completion, the state is committed:

```csharp
public async Task CommitSettingsAsync(OnboardingState state)
{
    // Step 1: Language
    Settings.Language = state.SelectedLanguage;
    
    // Step 2: Save Location
    Settings.ScreenshotsFolder = state.ScreenshotsFolder;
    Settings.CreateDateSubfolders = state.CreateDateSubfolders;
    
    // Step 3: Hotkeys
    HotkeyManager.SetPrimaryCapture(state.PrimaryCaptureHotkey);
    foreach (var hotkey in state.AdditionalHotkeys)
    {
        HotkeyManager.Register(hotkey);
    }
    
    // Step 4: Upload
    Settings.DefaultUploader = state.SelectedUploader;
    
    // Step 5: OCR (schedule downloads)
    if (state.DownloadOcrInBackground)
    {
        _ = Task.Run(() => OcrEngine.DownloadLanguagesAsync(state.SelectedOcrLanguages));
    }
    else
    {
        await OcrEngine.DownloadLanguagesAsync(state.SelectedOcrLanguages);
    }
    
    await Settings.SaveAsync();
}
```

### Avalonia UI Implementation

#### Project Structure

```
src/desktop/XerahS.Desktop/
├── Views/
│   └── Onboarding/
│       ├── OnboardingWizard.axaml         # Main container
│       ├── OnboardingWizard.axaml.cs
│       ├── Steps/
│       │   ├── WelcomeStep.axaml
│       │   ├── SaveLocationStep.axaml
│       │   ├── HotkeyStep.axaml
│       │   ├── UploadStep.axaml
│       │   ├── OcrStep.axaml
│       │   └── CompleteStep.axaml
│       └── Controls/
│           ├── HotkeyRecorder.axaml       # Reusable hotkey input
│           ├── ProgressIndicator.axaml
│           └── ConflictWarning.axaml
└── ViewModels/
    └── Onboarding/
        ├── OnboardingWizardViewModel.cs
        ├── OnboardingState.cs
        └── Steps/
            ├── WelcomeStepViewModel.cs
            ├── SaveLocationStepViewModel.cs
            ├── HotkeyStepViewModel.cs
            ├── UploadStepViewModel.cs
            ├── OcrStepViewModel.cs
            └── CompleteStepViewModel.cs
```

#### Navigation Pattern

```csharp
public class OnboardingWizardViewModel : ViewModelBase
{
    private readonly OnboardingState _state = new();
    private int _currentStepIndex;
    
    public ObservableCollection<StepViewModelBase> Steps { get; } = new()
    {
        new WelcomeStepViewModel(),
        new SaveLocationStepViewModel(),
        new HotkeyStepViewModel(),
        new UploadStepViewModel(),
        new OcrStepViewModel(),
        new CompleteStepViewModel()
    };
    
    public StepViewModelBase CurrentStep => Steps[_currentStepIndex];
    
    public bool CanGoBack => _currentStepIndex > 0;
    public bool CanGoNext => _currentStepIndex < Steps.Count - 1;
    public bool IsLastStep => _currentStepIndex == Steps.Count - 1;
    
    public void Next()
    {
        if (!CurrentStep.Validate()) return;
        CurrentStep.SaveToState(_state);
        
        if (IsLastStep)
        {
            CompleteWizard();
        }
        else
        {
            _currentStepIndex++;
            CurrentStep.LoadFromState(_state);
            OnPropertyChanged(nameof(CurrentStep));
        }
    }
    
    public void Skip()
    {
        // Mark step as skipped but continue
        CurrentStep.MarkSkipped();
        Next();
    }
    
    private async void CompleteWizard()
    {
        await CommitSettingsAsync(_state);
        Close(new OnboardingResult { Completed = true });
    }
}
```

### Platform-Specific Considerations

#### Windows
- Hotkey conflict detection via `RegisterHotKey` API (test before setting)
- Shell integration: Option to pin to taskbar/start menu on completion step

#### macOS
- Hotkey conflict detection limited (macOS doesn't expose global hotkey registry)
- Accessibility permissions: Prompt for screen recording permission on first capture attempt

#### Linux
- Wayland: Hotkey configuration may be limited; show informational message
- Portal integration: Use `xdg-desktop-portal` for file picker if available

---

## Compatibility & Edge Cases

### Upgrade vs. Fresh Install

| Scenario | Behavior |
|----------|----------|
| Fresh install (no `ApplicationConfig.json`) | Show wizard on first launch |
| Upgrade (existing config, `IsFirstTimeRun=false`) | Skip wizard entirely |
| Upgrade (existing config, `IsFirstTimeRun=true` from beta) | Show wizard with existing settings pre-populated |
| Portable mode | Same behavior, store config relative to executable |

### Import from ShareX (removed from wizard)

> **Removed on 2026-06-28.** ShareX import is available in Destination Settings only, not in the onboarding upload step. Do not re-add wizard import UI.

The existing ShareX import functionality ~~will be integrated into Step 4 (Upload Destination)~~ (historical design):

```csharp
public class UploadStepViewModel : StepViewModelBase
{
    public bool HasShareXConfig => ShareXImporter.DetectConfig();
    
    [RelayCommand]
    private async Task ImportFromShareX()
    {
        var result = await ShareXImporter.ImportAsync();
        if (result.Success)
        {
            // Pre-populate uploaders from imported config
            AvailableUploaders.AddRange(result.Uploaders);
            SelectedUploader = result.PrimaryUploader;
        }
    }
}
```

### Skipping Behavior

| Skip Action | Result |
|-------------|--------|
| Skip single step | Step uses defaults, continues to next |
| "Skip All" button | All steps use defaults, wizard closes, `IsFirstTimeRun` marked complete |
| Close window (X) | Wizard cancels, no settings changed, `IsFirstTimeRun` remains true (will show again on next launch) |

### Enterprise/MDM Environments

Support for enterprise deployment via configuration files:

```json
// deployment-config.json
{
  "onboarding": {
    "enabled": false,           // Skip wizard entirely
    "screenshotsFolder": "\\\server\share\screenshots",
    "language": "en",
    "uploaders": ["none"],
    "disableOcr": true
  }
}
```

If `onboarding.enabled` is false, the wizard is suppressed regardless of `IsFirstTimeRun`.

### Accessibility

- All steps keyboard-navigable (Tab order, Enter to advance)
- Screen reader announcements for progress changes
- High contrast theme support
- Minimum 4.5:1 contrast ratio for all text
- Focus indicators visible on all interactive elements

---

## Acceptance Criteria

### Functional Requirements

1. **Wizard Trigger**: Wizard appears automatically on first launch when `IsFirstTimeRun` is true
2. **Step Completion**: User can complete all 5 configuration steps in sequence
3. **Settings Persistence**: All configured settings are saved to `ApplicationConfig.json` on completion
4. **First Capture**: User can trigger a test capture directly from the completion step
5. **Skip Support**: User can skip individual steps or the entire wizard
6. **Back Navigation**: User can return to previous steps to change selections
7. **Validation**: Invalid configurations are blocked with clear error messages
8. **Conflict Detection**: Hotkey conflicts are detected and surfaced to the user

### UX Requirements

1. **Completion Time**: Average user completes wizard in under 2 minutes
2. **Skip Rate**: Less than 20% of users skip the entire wizard
3. **Error Rate**: Less than 5% of users encounter validation errors
4. **First Capture**: 80%+ of users who complete wizard take a screenshot within 5 minutes
5. **Accessibility**: Wizard passes WCAG 2.1 AA standards

### Technical Requirements

1. **Performance**: Wizard loads in under 500ms on recommended hardware
2. **Memory**: Wizard adds less than 50MB RAM usage during operation
3. **Cross-Platform**: All steps functional on Windows, macOS, and Linux
4. **Localization**: Wizard UI itself is localizable (at minimum: EN, DE, FR, ES, JA, ZH)
5. **Test Coverage**: 80%+ unit test coverage for ViewModels, integration tests for full flow

### Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Wizard completion rate | >70% | % of first-run users who complete all steps |
| Time to first capture | <3 min | Median time from app launch to first screenshot |
| Support tickets (config) | -50% | Reduction in "where are my screenshots" tickets |
| Day-1 retention | +20% | % of users who return within 24 hours |
| App Store rating | +0.5 stars | Improvement in "ease of use" category |

---

## Implementation Phases

### Phase 1: Core Framework (Week 1-2)
- Create `OnboardingWizard` shell with navigation
- Implement `OnboardingState` and settings persistence
- Build progress indicator and navigation controls

### Phase 2: Essential Steps (Week 3-4)
- Step 1: Welcome & Language
- Step 2: Save Location
- Step 3: Hotkey Configuration

### Phase 3: Advanced Steps (Week 5-6)
- Step 4: Upload Destination (integrate with existing uploader UI)
- Step 5: OCR Language Setup
- Step 6: Completion

### Phase 4: Polish & Integration (Week 7-8)
- Hotkey conflict detection
- ShareX import integration
- Accessibility audit
- Localization

### Phase 5: Testing & Release (Week 9-10)
- Cross-platform testing
- User testing with 5-10 participants
- Analytics instrumentation
- Documentation

---

## References

### Yoink Reference

Yoink's `SetupWizard` implementation provides excellent UX patterns:
- **Progressive disclosure**: Only show advanced options when requested
- **Smart defaults**: Pre-select options based on system detection
- **Immediate feedback**: Test buttons that validate configuration in real-time
- **Graceful exit**: Users can skip without penalty, but are encouraged to complete

Source: [Yoink Mac App Store](https://apps.apple.com/us/app/yoink/id457622435) — observe the onboarding flow on first launch.

### Related XIPs

- **XIP0012**: Import ShareX uploaders config — Upload destination step builds on this
- **XIP0024**: Custom uploader integration — Upload step UI should accommodate custom uploaders
- **XIP0026**: Task settings UX redesign — Wizard should align with new settings patterns
- **XIP0045**: ShareX config import runtime migration — Wizard replaces the one-time import button

### External References

- [Avalonia Dialogs Documentation](https://docs.avaloniaui.net/docs/controls/window)
- [Fluent Design Onboarding Patterns](https://learn.microsoft.com/en-us/windows/apps/design/ux-templates/onboarding)
- [Apple Human Interface Guidelines: Onboarding](https://developer.apple.com/design/human-interface-guidelines/patterns/onboarding)

---

## Open Questions

1. **Should we A/B test step order?** The proposed order (Language → Save → Hotkey → Upload → OCR) prioritizes essential configuration, but we may want to test placing OCR earlier for users who specifically downloaded for text extraction.

2. **Video tutorial integration?** Should Step 1 include an optional 30-second video demonstrating XerahS capabilities?

3. **Cloud sync for settings?** Should we offer to sync wizard selections to cloud for multi-device setup?

4. **Post-wizard tips?** After wizard completion, should we show contextual tips (e.g., "Did you know you can drag files to the tray icon?") for the first week?

---

## Non-Negotiable Rules

1. **Never block the user**: Skip option always available, close button always works
2. **Preserve existing settings**: If user has partial config, pre-populate and don't overwrite
3. **Test before finish**: Every configuration should be testable before completing the wizard
4. **Respect privacy**: Upload configuration defaults to local-only; cloud upload requires explicit opt-in
5. **Work offline**: Wizard must function without internet connection (OCR languages download later)

---

## Design Specification (Sofia Novak)

### 1. Visual Direction

**Aesthetic tone**: "Precision Tool" — the visual language of a crafted instrument. Think a high-end camera body or a mechanical keyboard: purposeful, confident, quietly premium. Not playful, not corporate. The wizard should feel like opening a beautifully machined device for the first time.

**Color palette** (CSS variables):

| Token | Light | Dark | Usage |
|---|---|---|---|
| `--wiz-bg` | `#F4F5F7` | `#0F1117` | Page / backdrop |
| `--wiz-surface` | `#FFFFFF` | `#1A1D27` | Card / content area |
| `--wiz-border` | `#E2E5EA` | `#2A2D3A` | Dividers, input borders |
| `--wiz-text-primary` | `#0D0F14` | `#EEF0F5` | Headings, labels |
| `--wiz-text-secondary` | `#5C6170` | `#8B90A0` | Subtext, hints |
| `--wiz-accent` | `#00B4A6` | `#00D4C8` | CTA buttons, active steps, focus rings |
| `--wiz-accent-hover` | `#009E91` | `#00BFB5` | Hover state for accent |
| `--wiz-danger` | `#E5484D` | `#FF6B70` | Conflicts, errors |
| `--wiz-warning` | `#F5A623` | `#FFBE3D` | Warnings, conflict notices |
| `--wiz-success` | `#12A594` | `#2DD4BF` | Completion, downloaded |

**Typography**:
- **Headings / step titles**: `"Sora"` (Google Fonts) — geometric, confident, distinctive. Weights 600-700. Falls back to `"DM Sans", system-ui`.
- **Body / labels**: `"DM Sans"` — clean and readable without being generic. Weight 400-500. Falls back to `system-ui`.
- **Monospace accents** (paths, hotkeys, file sizes): `"JetBrains Mono"` — signals "this is a technical tool" without being intimidating. Weight 400.

**Spatial system**:
- Base unit: `8px`
- Card padding: `40px 48px` on desktop, `24px 20px` on compact
- Element gap: `16px` standard, `24px` between sections
- Border radius: `12px` for card, `8px` for inputs/buttons, `6px` for chips

**Motion philosophy**: Fast and purposeful. 200-280ms transitions. Nothing bouncy or playful — this isn't a consumer app. Easing: `cubic-bezier(0.25, 0.46, 0.45, 0.94)` (ease-out-quad) for entrances; `cubic-bezier(0.55, 0, 1, 0.45)` for exits.

---

### 2. Step Wireframes

**Shell layout** (applies to all steps):

```
┌─────────────────────────────────────────────────────────┐
│  [✕]                                           40px 48px card
│                                                         │
│  ●───●───○───○───○───○     Step indicator (top center)  │
│  1   2   3   4   5   6                                   │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │                                                  │  │
│  │  [Step icon]  Step Title                         │  │
│  │  Step subtitle / description                     │  │
│  │                                                  │  │
│  │  ── Content area (scrollable) ─────────────────│  │
│  │                                                  │  │
│  │                                                  │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  [← Back]     [Skip all]           [Next →]  or  [Done] │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Progress indicator details**:
- Horizontal row of circles connected by lines: `●──●──○──○──○──○`
- Completed steps: filled `--wiz-accent`, checkmark icon inside
- Current step: `--wiz-accent` ring with white fill, subtle pulse animation (scale 1→1.05→1, 2s loop)
- Future steps: `--wiz-border` filled, `--wiz-text-secondary` number
- Step label below each dot (e.g., "Language", "Save", "Hotkey") — shown at ≥800px wide, hidden below
- Connecting lines animate from `--wiz-border` to `--wiz-accent` as steps complete (width transition, 300ms)

**CTA button placement**:
- Always right-aligned in the footer: `[Back] [Skip] ............. [Next →]`
- "Done" replaces "Next" on the final step
- Primary CTA uses `--wiz-accent` background, white text, `8px` radius
- "Back" and "Skip" are ghost buttons (border only, no fill) — `--wiz-text-secondary`
- Buttons are `48px` tall, minimum `120px` wide
- "Skip all" is smaller text link, not a button: `12px`, `--wiz-text-secondary`

**Skip link**: Small `Skip this step →` text link above the primary CTA, aligned left, `--wiz-text-secondary`. Disappears on final step.

**Window dimensions**:
- Recommended: `640px × 520px`, resizable up to `800px × 640px`
- Minimum: `480px × 440px`
- Centered on screen, OS-native window frame (not custom title bar)

---

### 3. Component Inventory

#### Step Indicator

| State | Appearance |
|---|---|
| Completed | `--wiz-accent` fill, white checkmark icon, connecting line filled |
| Current | White fill, `--wiz-accent` ring (2px), subtle scale pulse |
| Future | `--wiz-border` fill, `--wiz-text-secondary` number |

#### Primary CTA Button (`<button.wiz-btn-primary>`)

| State | Appearance |
|---|---|
| Default | `--wiz-accent` bg, white text, `8px` radius, subtle shadow |
| Hover | `--wiz-accent-hover` bg, translateY(-1px), shadow grows |
| Active/Press | `--wiz-accent` bg, translateY(0), shadow shrinks — 80ms |
| Disabled | `40%` opacity, `cursor: not-allowed`, no shadow |
| Focus | `--wiz-accent` `0 0 0 3px` focus ring (outside border-radius) |

#### Ghost / Back Button (`<button.wiz-btn-ghost>`)

| State | Appearance |
|---|---|
| Default | Transparent bg, `--wiz-text-secondary` text, `--wiz-border` border |
| Hover | `--wiz-surface` bg (slight tint), `--wiz-text-primary` text |
| Active | `--wiz-border` bg |
| Disabled | `30%` opacity |

#### Radio Cards (`<label.wiz-radio-card>`)

Used for: upload destination selection (Step 4), quick-select save locations (Step 2).

```
┌─────────────────────────────────────────┐
│ [○]  Icon                    Title      │
│      Description text                    │
│                           [chip: active]│
└─────────────────────────────────────────┘
```

| State | Appearance |
|---|---|
| Default | `--wiz-surface` bg, `--wiz-border` border, `8px` radius |
| Hover | `--wiz-border` transitions to `--wiz-text-secondary`, `2px` border |
| Selected | `--wiz-accent` border (`2px`), `--wiz-accent` `0 0 0 1px` inner ring, light accent tint bg |
| Focus-within | `--wiz-accent` `0 0 0 3px` focus ring |

#### Text Input (`<input.wiz-input>`)

| State | Appearance |
|---|---|
| Default | `--wiz-surface` bg, `--wiz-border` border, `8px` radius, `48px` height |
| Hover | `--wiz-border` → `--wiz-text-secondary` |
| Focus | `--wiz-accent` border, `--wiz-accent` `0 0 0 3px` focus ring |
| Error | `--wiz-danger` border, `--wiz-danger` `0 0 0 3px` focus ring, error message below |
| Disabled | `--wiz-bg` bg, `50%` opacity |

#### Toggle Switch (`<input.wiz-toggle[type=checkbox]>`)

| State | Appearance |
|---|---|
| Off | `--wiz-border` track (40×24px, pill), white thumb |
| On | `--wiz-accent` track, white thumb |
| Hover | Track lightens slightly |
| Focus | `--wiz-accent` `0 0 0 3px` focus ring |
| Disabled | `40%` opacity |

#### Hotkey Recorder (`<div.wiz-hotkey-recorder>`)

```
┌──────────────────────────────────┐
│  Press a key combination...  ⌨  │
└──────────────────────────────────┘
```

| State | Appearance |
|---|---|
| Idle | Dashed `--wiz-border` border, placeholder text `--wiz-text-secondary` |
| Recording | Solid `--wiz-accent` border, pulsing cursor, "Press keys..." text |
| Recorded | Solid `--wiz-accent` border, shows key combo in `JetBrains Mono`, "✕" clear button |
| Conflict | `--wiz-danger` border, warning icon + conflict message below |

#### Progress Bar (OCR downloads, Step 5)

| State | Appearance |
|---|---|
| Idle | `--wiz-border` track, `4px` height, `12px` radius |
| Downloading | `--wiz-accent` fill with animated shimmer (gradient slide, 1.5s loop) |
| Done | `--wiz-success` fill, checkmark icon replaces bar |

#### Directory Picker (Step 2)

```
┌──────────────────────────────────────────────────────────┐
│  /home/user/Pictures/Screenshots           [Browse] [✕]  │
└──────────────────────────────────────────────────────────┘
```

- Text input (read-only, displays path) + "Browse" button + "Clear" button
- Browse opens OS native folder picker dialog

#### Conflict Warning Card (Step 3)

```
┌──────────────────────────────────────────────────────────┐
│ ⚠  Conflict Detected                                    │
│ "Print Screen" is bound to Windows Snipping Tool        │
│                                                          │
│ [ ] Disable Windows Snipping Tool shortcut              │
│ [ ] Choose a different hotkey                           │
└──────────────────────────────────────────────────────────┘
```

- `--wiz-warning` left border (3px), warning icon, light warning-tint bg
- Checkboxes for resolution options

---

### 4. Animation / Transition Spec

**Step transitions**: Horizontal slide. Entering step slides in from the right (or left when going Back); exiting step slides out to the left (or right when going Back). Duration: `240ms`, easing: `ease-out-quad` enter, `ease-in-quad` exit.

```
direction = forward ? "left-exit / right-enter"
               : "right-exit / left-enter"
```

**Progress indicator transitions**: Connecting line fills with `--wiz-accent` as each step completes, `300ms ease-out-quad`.

**Button micro-interactions**:
- Hover: `translateY(-1px)`, shadow grows, `120ms ease-out`
- Active: `translateY(0)`, shadow shrinks, `80ms ease-in`
- Press feedback on radio cards: `scale(0.98)` on `mousedown`, `scale(1)` on `mouseup`, `100ms`

**Focus transitions**: Focus rings appear with `0ms` delay (snappy, not animated — focus should be instant).

**Success animation (completion step)**:
- Checkmark SVG draws itself: `stroke-dashoffset` from `100` to `0`, `600ms ease-out`, then circle fills `--wiz-accent` at `400ms ease-out` with `200ms` delay
- Confetti-like particle burst (6-8 small squares, random trajectories, `800ms`, CSS keyframes)
- Card slightly lifts: `translateY(-4px)`, `300ms ease-out`

**Hotkey recorder pulse** (recording state): Border color transitions to `--wiz-accent` and a subtle `box-shadow` pulse (0→4px→0 spread, `1s` loop, infinite).

**Loading states** (e.g., testing upload connection): Spinner replaces button text, button disabled, `opacity: 0.7`. Spinner is a simple CSS rotating ring in `--wiz-accent`.

**No entrance animations on individual form elements** — they should appear instantly. The step transition itself provides the motion; nested element animations add latency without value.

---

### 5. Dark / Light Theme

Both themes are full CSS variable sets. The application handles theme switching by toggling a `data-theme="dark"` attribute on the root element.

**Light theme** (`data-theme="light"`, default):
- `--wiz-bg`: `#F4F5F7` — warm gray page
- `--wiz-surface`: `#FFFFFF` — clean card
- All tokens per the palette table above

**Dark theme** (`data-theme="dark"`):
- `--wiz-bg`: `#0F1117` — near-black, slight blue cast
- `--wiz-surface`: `#1A1D27` — elevated card
- Borders remain subtle (`#2A2D3A`) so they read as structure, not noise
- Accent shifts from `#00B4A6` → `#00D4C8` (slightly brighter against dark bg for equal perceived brightness)
- Shadows in dark mode use `rgba(0, 0, 0, 0.4)` rather than light-mode shadow values

**Theme implementation**:
```css
.wizard-card {
  background: var(--wiz-surface);
  border: 1px solid var(--wiz-border);
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);  /* light */
  /* dark: box-shadow: 0 4px 24px rgba(0, 0, 0, 0.40); */
}
```

All components listed in Section 3 have tokens that cover both themes.

---

### 6. Accessibility

**Keyboard navigation**:
- Tab order: Skip link → Back button → Next/Done button (forward); reverse on Shift+Tab
- Within content area: follows natural DOM order (logical, not visual)
- `Enter` or `Space` activates focused button or radio card
- `Escape` closes the wizard (same as ✕ button — no unsaved changes are persisted unless explicitly saved)

**Step navigation via keyboard**:
- `Ctrl+Shift+Right Arrow` / `Ctrl+Shift+Left Arrow`: Jump to next/previous step without validation (skip)
- Arrow keys cycle through radio card options within a step

**Focus management**:
- On step change: focus moves to the step title (`<h2>`) so screen readers announce the new step immediately
- Focus is NOT moved to the Next button automatically — user should control focus flow
- `aria-live="polite"` region on the step indicator container announces "Step 3 of 6: Hotkey Configuration" on each step change
- Focus trap: Tab cycles within the wizard dialog; ✕ button is the last tabbable element before cycling back to first

**Screen reader flow**:
1. Dialog opens → `role="dialog"`, `aria-labelledby` pointing to step title, `aria-modal="true"`
2. Step indicator reads: "Onboarding wizard. Step 1 of 6, Welcome and Language"
3. Step content is a single `<main>` region within the dialog
4. Radio card groups use `role="radiogroup"` with `aria-checked`
5. Hotkey recorder announces state: "Capture hotkey. Press keys to record. [state]" / "Hotkey set to Ctrl+Shift+Print Screen"
6. Completion step: announces "Setup complete. 5 settings configured."
7. Error messages: `role="alert"` so they interrupt and announce immediately

**Color and contrast**:
- All text/background combinations meet WCAG AA (4.5:1 for normal text, 3:1 for large text)
- Color is never the only indicator of state — radio cards use a combination of border color, fill, and an explicit checkmark icon
- Focus indicators use a high-contrast ring (`--wiz-accent 0 0 0 3px`) visible in both themes

**Motion**:
- Respects `prefers-reduced-motion`: all slide transitions collapse to instant, pulse animations stop, shimmer animations stop
- `prefers-reduced-motion` media query applied globally in the wizard's stylesheet

**Touch / pointer**:
- Minimum touch target: `44×44px` (WCAG 2.5.5) for all interactive elements
- Radio cards and buttons have a `cursor: pointer`
- Skip link text is `16px` (not smaller) for readability on all devices

---

*Implementation note for Viktor / Mikhail: All colors should be defined as CSS custom properties on `.wizard-root`. Theme switching is a single attribute toggle on that root element. The step transition engine should support both forward/backward directions and respect `prefers-reduced-motion`. See `src/desktop/XerahS.Desktop/Views/Onboarding/OnboardingWizard.axaml` for the shell, `Controls/` for individual component templates.*
