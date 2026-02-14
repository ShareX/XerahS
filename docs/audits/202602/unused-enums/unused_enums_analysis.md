# Unused Enums Analysis & Recommendations

**Generated on:** 2026-02-15
**Context:** This report analyzes the "unused" enums found in XerahS against the ShareX codebase to determine if they should be kept (for compatibility/upstream sync) or removed.

## Summary
- **Total Unused Enums in XerahS:** 27
- **False Positives:** 1 (`maps`)
- **Used in ShareX:** 26
- **Not Used in ShareX:** 0

## Recommendations
Since XerahS is a cross-platform port/fork of ShareX, maintaining enum parity is generally recommended to facilitate code merging and porting of new features from ShareX. 

| Enum Name | ShareX Usage | Recommendation | Context |
|---|---|---|---|
| **InstallType** | 8 | **Keep** | Used in ShareX installers/shared logic. |
| **PrintType** | 10 | **Keep** | Used in ShareX printing logic. |
| **ArrowHeadDirection** | 15 | **Keep** | Core annotation logic in ShareX. |
| **StepType** | 13 | **Keep** | Workflow/Task logic in ShareX. |
| **CutOutEffectType** | 16 | **Keep** | Image effect logic in ShareX. |
| **ShareXBuild** | 7 | **Keep** | Versioning/Build info. |
| **CaptureType** | 1 | **Keep** | Legacy or specific capture mode. |
| **ScreenRecordStartMethod** | 19 | **Keep** | Recording logic parity. |
| **RegionCaptureType** | 21 | **Keep** | Core capture logic. |
| **ScreenTearingTestMode** | 6 | **Keep** | Utility tool. |
| **StartupState** | 22 | **Keep** | App lifecycle. |
| **BalloonTipClickAction** | 4 | **Keep** | Notification logic (Windows specific). |
| **NativeMessagingAction** | 7 | **Keep** | Browser extension integration. |
| **NotificationSound** | 37 | **Keep** | Sound feedback logic. |
| **BitmapCompressionMode** | 2 | **Keep** | Image processing. |
| **maps** | 0 | **Ignore** | **False Positive**: Identified as `// Avalonia Key enum maps...` (Comment). |
| **ScreenRecordOutput** | 21 | **Keep** | Recording output logic. |
| **ScreenRecordGIFEncoding** | 1 | **Keep** | GIF encoding options. |
| **RegionResult** | 42 | **Keep** | Capture result handling. |
| **NodePosition** | 65 | **Keep** | Image Editor / Region Capture shapes. |
| **NodeShape** | 19 | **Keep** | Image Editor / Region Capture shapes. |
| **FFmpegTune** | 3 | **Keep** | Screen recording encoding settings. |
| **ShapeCategory** | 12 | **Keep** | Image Editor tools. |
| **ImageInsertMethod** | 11 | **Keep** | Image manipulation. |
| **BorderStyle** | 110 | **Keep** | Image effects / UI. |
| **LinkFormatEnum** | 17 | **Keep** | Upload/URL copying logic. |
| **OAuthLoginStatus** | 21 | **Keep** | Authentication flows. |

## Conclusion
- **Action:** Remove/Ignore `maps` from any future consideration (it's a comment).
- **Action:** **Retain all other enums.** Removing them would likely break future ports of features from ShareX or complicate "diffing" against the upstream repository. Even if currently unused in XerahS, they represent functionality that might be ported or enabled in the future.
