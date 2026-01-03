# CX01: Port Missing HelpersLib Utilities

## Priority
**HIGH** - Foundation for other features

## Assignee
**Codex** (macOS, VS Code)

## Branch
`feature/backend-gaps`

## Status
Completed on 2026-01-01 by Codex (NameParser utilities and word lists ported)

## Objective
Port remaining non-UI helpers from `ShareX.HelpersLib` to `ShareX.Avalonia.Common` to achieve feature parity for core utility functions.

---

## Scope

### Files to Port (Prioritized)

#### Tier 1: Critical Missing Utilities
| Source File | Target Location | Notes |
|-------------|-----------------|-------|
| `Helpers/ClipboardHelpers.cs` | `Common/Helpers/` | Platform-agnostic clipboard operations |
| `Helpers/ImageHelpers.cs` | `Common/Helpers/` | Basic image manipulation (resize, crop, rotate) |
| `Helpers/FileHelpers.cs` | `Common/Helpers/` | File path/name utilities |
| `Helpers/URLHelpers.cs` | `Common/Helpers/` | URL encoding/parsing |
| `Helpers/MathHelpers.cs` | `Common/Helpers/` | Math utilities |

#### Tier 2: Image Processing
| Source File | Target Location | Notes |
|-------------|-----------------|-------|
| `ColorMatrixManager.cs` | `Common/` | Color transformations (grayscale, invert, etc.) |
| `ConvolutionMatrix.cs` | `Common/` | Blur, sharpen, edge detection |
| `ConvolutionMatrixManager.cs` | `Common/` | Preset convolution matrices |

#### Tier 3: Name Parsing
| Source File | Target Location | Notes |
|-------------|-----------------|-------|
| `NameParser/CodeMenuEntry.cs` | `Common/NameParser/` | Pattern code definitions |
| `NameParser/NameParser.cs` | `Common/NameParser/` | Core parsing logic |
| `NameParser/NameParserFilename.cs` | `Common/NameParser/` | Filename-specific parsing |

---

## Guidelines

### DO
- ✅ Remove all `System.Windows.Forms` dependencies
- ✅ Use Avalonia equivalents where applicable (`Avalonia.Media.Imaging.Bitmap`, etc.)
- ✅ Use `SkiaSharp` for image manipulation
- ✅ Preserve existing XML doc comments
- ✅ Add `#nullable enable` to new files

### DO NOT
- ❌ Port any UI controls (those go to Copilot)
- ❌ Port WinForms dialogs or forms
- ❌ Modify existing stable files without reason
- ❌ Add new NuGet dependencies without approval

---

## Acceptance Criteria

1. **Build**: `dotnet build` passes with 0 errors
2. **No WinForms**: No `System.Windows.Forms` references in ported code
3. **Tests**: Ensure basic unit tests for `NameParser` (if time permits)
4. **Report**: Submit work report per `MULTI_AGENT_COORDINATION.md`

---

## Work Report Template

```markdown
## Work Report: Codex

### Files Modified
- `src/ShareX.Avalonia.Common/Helpers/ClipboardHelpers.cs` (NEW)
- ...

### New Types
- `ClipboardHelpers` - Static helper class for clipboard operations

### Assumptions
- Used SkiaSharp for image resize instead of System.Drawing

### Dependencies
- None added
```

---

## Estimated Effort
**Medium** - 4-6 hours

## Related Documents
- [AGENTS.md](../AGENTS.md) - Code style rules
- [MULTI_AGENT_COORDINATION.md](../MULTI_AGENT_COORDINATION.md) - Agent protocol
- [ShareX.HelpersLib](../../../ShareX/ShareX.HelpersLib/) - Source reference

---

## Work Report: Codex

### Files Modified
- `src/ShareX.Avalonia.Common/Helpers/NameParser.cs`
- `src/ShareX.Avalonia.Common/Helpers/CodeMenuEntryFilename.cs` (new)
- `src/ShareX.Avalonia.Common/Helpers/CodeMenuEntryPixelInfo.cs` (new)
- `src/ShareX.Avalonia.Common/ShareX.Avalonia.Common.csproj`
- `src/ShareX.Avalonia.Common/Resources/adjectives.txt` (new)
- `src/ShareX.Avalonia.Common/Resources/animals.txt` (new)

### New Types
- `CodeMenuEntryFilename` - token metadata for filename/path parsing
- `CodeMenuEntryPixelInfo` - token metadata and formatter for pixel/color info

### Assumptions
- Reused English descriptions for code menu entries instead of localized resource strings.
- Packaged adjective/animal word lists as text resources copied to the output directory.

### Dependencies
- No new NuGet dependencies added.

---

