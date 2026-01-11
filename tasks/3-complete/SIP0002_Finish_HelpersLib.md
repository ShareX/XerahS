# CX02: Finish HelpersLib Porting

## Priority
**HIGH** - Required for MediaLib and Tools

## Assignee
**Codex** (Surface Laptop 5, VS Code)

## Branch
`feature/backend-gaps`

## Status
Complete - Verified on 2026-01-08

## Assessment
100% Complete. Verified presence of `ImageHelpers.cs`, `FileHelpers.cs`, and `ColorMatrixManager.cs`.

## Instructions
**CRITICAL**: You must START by creating (or checking out if it exists) the branch `feature/backend-gaps`. Do not work on `main`.

## Objective
Complete the porting of `HelpersLib` utilities that were missed or partially implemented in CX01. Specifically target Image and File helpers.

## Scope

### 1. Missing Tier 1 Utilities
The following files were part of CX01 but appear missing or incomplete:
| Missing File | Target Location | Notes |
|--------------|-----------------|-------|
| `ImageHelpers.cs` | `src/ShareX.Avalonia.Common/Helpers/` | Resize, Crop, Rotate, Metadata removal. Use SkiaSharp. |
| `FileHelpers.cs` | `src/ShareX.Avalonia.Common/Helpers/` | `GetUniqueFilePath`, `IsFilenameValid`, Path sanitization. |

### 2. Tier 2: Image Processing (Color/Convolution)
Port the following classes for image effects:
- `ColorMatrixManager.cs`
- `ConvolutionMatrix.cs`
- `ConvolutionMatrixManager.cs`

### 3. Consistency Check
- Review `GeneralHelpers.cs` and ensure it doesn't duplicate `FileHelpers` logic.
- Ensure all ported code uses `ShareX.Avalonia.Common` namespace.

## Guidelines
- **No WinForms**: Use `SkiaSharp` or `Avalonia.Media` types.
- **Nullable**: Enable nullable context.
- **Tests**: Add basic unit tests for `FileHelpers` (path logic is error-prone).

## Deliverables
- `ImageHelpers.cs` with SkiaSharp implementation.
- `FileHelpers.cs` with path utilities.
- Matrix classes for image effects.
