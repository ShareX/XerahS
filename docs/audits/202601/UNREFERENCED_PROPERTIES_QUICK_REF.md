# Unused Properties - Quick Reference

## Analysis Results
- **Total Properties:** 2,151
- **Files Analyzed:** 604
- **Sample Verified:** 50 properties
- **High-Confidence Unused:** ~25 properties

## Top Candidates for Removal

### Safe to Remove (High Confidence)
1. `BalloonTipContentFormat` (TaskSettings.cs:446) - superseded by ToastService
2. `OpenRouterAPIKey` (TaskSettingsOptions.cs:457) - feature not implemented
3. `GoogleCloudStorageDomain` (UploadersConfig.cs:393) - no implementation
4. `PluginId` (PluginManifest.cs:37) - superseded by ProviderId
5. `creator_id` (Copy.cs:299) - defunct Copy.com service
6. `IsPreviewMode` (NameParser.cs:57) - never checked
7. `HoveredWindow` (SelectionStateMachine.cs:56) - not used in window detection

### Mark for Deprecation
1. FFmpeg encoder properties (x264_Bitrate, AMF_Quality, x264_Use_Bitrate, UserArgs)
2. Update manager flags (IsPortable, CheckPreReleaseUpdates)
3. UI fallback flags (HasPreviewFallback, CanDownloadFFmpeg)

### Requires Expert Review
- All UploadersConfig properties (100+) - serialization concern
- TaskSettings/TaskSettingsOptions properties (50+) - config schema
- Plugin interface properties - contract compliance

## False Positives (KEEP These)
- `HttpHomePath` - used in GetHttpHomePath()
- `HasPendingSelection` - used in OverlayWindow
- `HasAnnotations` - used throughout UI
- All OAuth2Info properties - used via reflection
- All plugin manifest properties - required by contract

## Full Reports
- Detailed: [UNREFERENCED_PROPERTIES_COMPLETE_AUDIT.md](UNREFERENCED_PROPERTIES_COMPLETE_AUDIT.md)
- Analysis: [unreferenced_members_ANALYSIS_REPORT.md](unreferenced_members_ANALYSIS_REPORT.md)
- Inventory: [properties_inventory.csv](properties_inventory.csv)
- Sample: [unused_properties_report.csv](unused_properties_report.csv)

## Analysis Tools
- `scripts/analyze_unused_properties.ps1` - extract properties
- `scripts/find_unused_properties.ps1` - find unused

---
**Last Updated:** 2026-01-31
