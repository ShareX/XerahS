# Multi-Agent Development Coordination

## Context

This document defines the coordination rules for parallel development on ShareX.Avalonia using three AI developer agents.

---

## Agent Roles

| Agent | Platform | Role | Primary Scope |
|-------|----------|------|---------------|
| **Antigravity** | Windows (Claude) | Lead Developer | Architecture, platform abstraction, integration, merge decisions |
| **Codex** | Surface Laptop 5 (VS Code) | Backend Developer | Core logic, Helpers, Media, History, CLI, Settings |
| **Copilot** | Surface Laptop 7 (VS2026 IDE) | UI Developer | ViewModels, Views, Services, UI wiring |

---

## Antigravity Responsibilities

- Own main branch direction and architecture
- Decide task boundaries and assign work
- Review high-level changes before merge
- Prevent overlapping file modifications
- Final conflict resolution

---

## Task Distribution Rules

1. **Assign by project/folder boundaries, not individual files**
2. **Never assign two agents to the same project simultaneously**
3. **Prefer vertical slices over shared utilities**

---

## Recommended Task Split

### Codex (Backend)
- ✅ ShareX.Avalonia.Core (logic only, not Models exposed to UI)
- ✅ ShareX.Avalonia.Common (Helpers, utilities) - **Priority: Port Gaps**
- ✅ ShareX.Avalonia.Media (encoding, FFmpeg)
- ✅ ShareX.Avalonia.History (persistence, managers)
- ✅ ShareX.Avalonia.Annotations (Logic/Models - Phase 2)
- ❌ NO Avalonia UI projects or XAML

### Copilot (UI)
- ✅ ShareX.Avalonia.UI/ViewModels/*
- ✅ ShareX.Avalonia.UI/Views/*
- ✅ ShareX.Avalonia.UI/Controls/AnnotationCanvas - **Priority: Phase 2**
- ✅ UI wiring and MVVM bindings
- ❌ NO Core logic or backend processing (unless directed)

### Antigravity (Architecture)
- ✅ ShareX.Avalonia.Platform.Abstractions
- ✅ ShareX.Avalonia.Platform.Windows/Linux/macOS
- ✅ Project structure and solution files
- ✅ Merge Review & Documentation

---

## Git Workflow

### Branch Naming
```
feature/annotation-canvas  # Copilot: Canvas implementation
feature/backend-gaps       # Codex: HelpersLib porting
feature/automation-tasks   # Codex: Automation engine
```

### Commit Rules
- **Small, focused commits**
- **Message format**:
  ```
  [Project] Brief description

  - Change 1
  - Change 2
  
  Touched: folder/file1.cs, folder/file2.cs
  ```

### Push Frequency
- Push frequently (at least after each logical change)
- Never rebase shared branches
- Pull before starting work

---

## Conflict Avoidance

### Protected Resources (Single Agent Only)
| Resource | Owner |
|----------|-------|
| `AGENTS.md` | Antigravity |
| `NEXT_STEPS.md` | Antigravity |
| `*.sln` files | Antigravity |
| `*.csproj` files | Antigravity (or assigned agent for that project) |
| Shared interfaces | Antigravity approval required |
| Shared enums | Antigravity approval required |

### Escalation Rules
If conflict is likely:
1. Pause one agent
2. Redirect to different task
3. Antigravity resolves ownership

---

## Communication Protocol

### Agent Reports Must Include
1. Files modified (list)
2. New types added (class/interface names)
3. Assumptions made
4. Dependencies introduced

### Report Format
```markdown
## Work Report: [Agent Name]

### Files Modified
- `src/Project/Folder/File.cs`

### New Types
- `ClassName` - purpose

### Assumptions
- Assumed X because Y

### Dependencies
- Added reference to ProjectZ
```

### Antigravity Response
- ✅ Approve and continue
- 🔄 Redirect to different task
- ⏸️ Pause and clarify

---

## Stop Conditions

Agents MUST stop and ask Antigravity if:

1. **Unclear ownership** - Which project should this go in?
2. **Architectural ambiguity** - Is this the right pattern?
3. **Potential conflict** - Another agent might need this file
4. **New shared type needed** - Interface, enum, or model
5. **AGENTS.md violation** - Proposed change breaks rules

**No shortcuts that violate AGENTS.md rules are permitted.**

---

## Current Task Assignments

| Task | Agent | Branch | Status |
|------|-------|--------|--------|
| **SIP0001: Port HelpersLib Utilities** | Codex | `feature/backend-gaps` | 📋 [Assigned](tasks/SIP0001_Port_HelpersLib_Utilities.md) |
| **Annotation Canvas (Phase 2)** | Copilot | `feature/annotation-canvas` | 🔥 **Next Priority** |
| **Backend Gap Filling** | Codex | `feature/backend-gaps` | 🔥 **Next Priority** |
| Plugin System | Antigravity | `feature/uploaders` | ✅ Complete |
| History UI | Copilot | `feature/ui-history` | 📋 Planned |
| Screen Recorder Logic | Codex | `feature/recorder-core` | 📋 Planned |

---

## 🎯 Comprehensive Gap Analysis & Detailed Scope

This section outlines the specific missing features compared to ShareX (WinForms) and assigns them to agents.

### 1. `ShareX.HelpersLib` (Foundation)
**Status**: ~60% Ported.
**Missing**:
- Advanced image manipulators (ColorMatrix, ConvolutionMatrix).
- System integration helpers (verified platform agnostic).
- **Assignment**: **Codex** (`feature/backend-gaps`)
  - *Goal*: Port remaining non-UI helpers to `ShareX.Avalonia.Common`.

### 2. `ShareX.ScreenCaptureLib` (Capture Engine)
**Status**: Region/Fullscreen implemented.
**Missing**:
- **Screen Recording**: FFmpeg integration, command line generation.
- **Scrolling Capture**: Image stitching logic.
- **OCR**: Text recognition integration.
- **Assignment**:
  - **Logic**: **Codex** (`feature/recorder-core`) - FFmpeg wrapper, stitching logic.
  - **UI**: **Copilot** - Recording overlay, region selection updates.

### 3. `ShareX.MediaLib` (Processing)
**Status**: Basic.
**Missing**:
- Image Combiner.
- Video Converter (UI & Logic).
- GIF Encoding optimization.
- **Assignment**: **Codex** - Port core logic to `ShareX.Avalonia.Media`.

### 4. `ShareX` (Application Tools)
**Status**: Main Window & Settings done.
**Missing Tools**:
- Color Picker.
- Screen Ruler.
- Image Editor (Annotation Canvas).
- QR Code Generator/Decoder.
- DNS Changer / Hash Check (Low priority).
- **Assignment**: **Copilot** (`feature/tools-ui`)
  - *Goal*: Create `ShareX.Avalonia.UI/Tools/*`.

### 5. `ShareX.HistoryLib` (Persistence)
**Status**: Basic Manager exists.
**Missing**:
- Advanced History View (search, filter, thumbnails).
- **Assignment**: **Copilot** (`feature/ui-history`) - Build the grid view.

### 6. `ShareX.UploadersLib` (Plugins)
**Status**: Architecture Done. Imgur/S3 Done.
**Strategy**: **Do not port all 50+ uploaders.**
- Wait for community contributions via the new Plugin System.
- Implement only highly requested ones on demand.

---

## 🚀 Execution Plan (Next 3 Sprints)

### Sprint 1: The Editor & The Foundation
- **Copilot**: Build `AnnotationCanvas` (Phase 2). This is the "Image Editor".
- **Codex**: Port `HelpersLib` gaps and `MediaLib` basics.

### Sprint 2: Tools & Recorder
- **Copilot**: Color Picker, Ruler, QR Code UI.
- **Codex**: Screen Recorder logic (FFmpeg piping).

### Sprint 3: Polish & History
- **Copilot**: History Window & Image History.
- **Codex**: Scrolling Capture logic.

---

## Outcome

This structure enables:
- ✅ Parallel development
- ✅ Minimal Git conflicts
- ✅ Centralized architectural control
- ✅ Clear ownership boundaries
- ✅ Efficient code review
