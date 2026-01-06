# SIP0015: Complete Workflow Configuration

## Status
**✅ DONE** - Implemented

## Priority
**MEDIUM** - Required for full workflow customization

## Branch
`feature/workflow-configuration`

---

## Objective

Ensure each workflow in `WorkflowsConfig.json` contains a complete `TaskSettings` with all workflow components from [workflow_overview.md](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/docs/plans/workflow_overview.md).

---

## Workflow Coverage Checklist

| # | Workflow Component | Property | Status |
|---|-------------------|----------|--------|
| 1 | **Job (Trigger)** | `TaskSettings.Job` | ✅ Complete |
| 2 | **After Capture Tasks** | `TaskSettings.AfterCaptureJob` | ✅ Complete |
| 3 | **After Upload Tasks** | `TaskSettings.AfterUploadJob` | ✅ Complete |
| 4 | **Destinations** | | ✅ Complete |
| | - ImageDestination | `TaskSettings.ImageDestination` | ✅ |
| | - TextDestination | `TaskSettings.TextDestination` | ✅ |
| | - FileDestination | `TaskSettings.FileDestination` | ✅ |
| | - URLShortener | `TaskSettings.URLShortenerDestination` | ✅ |
| | - URLSharingService | `TaskSettings.URLSharingServiceDestination` | ✅ |
| 5 | **Image Effects** | `TaskSettings.ImageSettings.*` | ⚠️ **Needs Fix** |
| 6 | **Hotkey Binding** | `HotkeySettings.HotkeyInfo` | ✅ Complete |

---

## Changes Required

### 1. Rename HotkeysConfig.json → WorkflowsConfig.json

**Files to update:**

| File | Change |
|------|--------|
| [SettingManager.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Core/Managers/SettingManager.cs) | Rename `HotkeysConfigFileName` → `WorkflowsConfigFileName` |
| [HotkeySettings.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Core/Models/HotkeySettings.cs) | Rename `HotkeysConfig` → `WorkflowsConfig` |
| [ApplicationConfig.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Core/Models/ApplicationConfig.cs) | Rename `CustomHotkeysConfigPath` → `CustomWorkflowsConfigPath` |

```csharp
// SettingManager.cs
public const string WorkflowsConfigFileName = "WorkflowsConfig.json";
```

---

### 2. Add Missing Image Effects Properties to TaskSettingsImage

**File:** [TaskSettings.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Core/Models/TaskSettings.cs#L156-L181)

```csharp
#region Image / Effects

public List<ImageEffectPreset> ImageEffectPresets = new() { ImageEffectPreset.GetDefaultPreset() };
public int SelectedImageEffectPreset = 0;
public bool ShowImageEffectsWindowAfterCapture = false;
public bool ImageEffectOnlyRegionCapture = false;
public bool UseRandomImageEffect = false;

#endregion Image / Effects
```

---

### 3. Implement Full ImageEffectPreset

**File:** [TaskSettingsOptions.cs](file:///c:/Users/liveu/source/repos/ShareX%20Team/ShareX.Avalonia/src/ShareX.Avalonia.Core/Models/TaskSettingsOptions.cs#L17-L21)

```csharp
using ShareX.Editor.ImageEffects;

public class ImageEffectPreset
{
    public string Name { get; set; } = "";

    [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
    public List<ImageEffect> Effects { get; set; } = new();

    public SKBitmap ApplyEffects(SKBitmap bmp)
    {
        var result = bmp.Copy();
        foreach (var effect in Effects.Where(x => x.Enabled))
        {
            result = effect.Apply(result);
            if (result == null) break;
        }
        return result;
    }

    public static ImageEffectPreset GetDefaultPreset() => new() { Name = "Default" };
}
```

---

### 4. Add Project Reference

**File:** `ShareX.Avalonia.Core.csproj`

```xml
<ProjectReference Include="..\..\..\..\ShareX.Editor\src\ShareX.Editor\ShareX.Editor.csproj" />
```

---

## After Implementation

| # | Workflow Component | Status |
|---|-------------------|--------|
| 1 | Job (Trigger) | ✅ |
| 2 | After Capture Tasks | ✅ |
| 3 | After Upload Tasks | ✅ |
| 4 | Destinations | ✅ |
| 5 | Image Effects | ✅ |
| 6 | Hotkey Binding | ✅ |

---

## Verification

```powershell
dotnet build ShareX.Avalonia.sln
```

- Verify `WorkflowsConfig.json` is created on first run
- Existing `HotkeysConfig.json` should be migrated (optional)

---

## Estimated Effort
**4-5 hours**
