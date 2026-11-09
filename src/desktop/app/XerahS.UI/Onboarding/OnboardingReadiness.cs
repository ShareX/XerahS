#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Onboarding;

public enum ReadinessLevel
{
    /// <summary>Works as expected.</summary>
    Ok,

    /// <summary>Something will not work until the user acts.</summary>
    Attention,

    /// <summary>Good to know; nothing to fix.</summary>
    Info
}

public sealed record ReadinessItem(string Title, string Detail, ReadinessLevel Level)
{
    public string Marker => Level switch
    {
        ReadinessLevel.Ok => "✓",
        ReadinessLevel.Attention => "!",
        _ => "i"
    };

    public bool NeedsAttention => Level == ReadinessLevel.Attention;
}

/// <summary>Facts the first-run "Ready" step reports on. Plain values so the rules can be tested.</summary>
public sealed record ReadinessInput(
    string SaveFolder,
    bool SaveFolderWritable,
    int HotkeysConfigured,
    int HotkeysFailed,
    string? RegionHotkey,
    bool IsMacOS,
    bool? MacScreenRecordingGranted,
    bool IsLinux,
    string? LinuxSessionType,
    bool FfmpegAvailable);

/// <summary>
/// Turns the machine's real state into the checklist shown at the end of the first-run wizard, so a new user
/// learns up front what works and what needs a step, instead of finding out when a hotkey does nothing.
/// </summary>
public static class OnboardingReadiness
{
    public static IReadOnlyList<ReadinessItem> Evaluate(ReadinessInput input)
    {
        List<ReadinessItem> items = [];

        items.Add(input.SaveFolderWritable
            ? new ReadinessItem("Captures are saved here", input.SaveFolder, ReadinessLevel.Ok)
            : new ReadinessItem(
                "Cannot save to the capture folder",
                $"XerahS cannot write to {input.SaveFolder}. Pick another folder in Application Settings before you capture.",
                ReadinessLevel.Attention));

        if (input.HotkeysConfigured == 0)
        {
            items.Add(new ReadinessItem(
                "No hotkeys are set",
                "Start captures from the tray icon or from Workflows → Run Workflow.",
                ReadinessLevel.Info));
        }
        else if (input.HotkeysFailed > 0)
        {
            items.Add(new ReadinessItem(
                "Some hotkeys did not register",
                $"{input.HotkeysFailed} of {input.HotkeysConfigured} hotkeys could not be registered, usually because another app or the desktop already uses them. " +
                "Change them in Workflows, or start captures from the tray icon.",
                ReadinessLevel.Attention));
        }
        else
        {
            string detail = string.IsNullOrWhiteSpace(input.RegionHotkey)
                ? $"All {input.HotkeysConfigured} hotkeys are active."
                : $"All {input.HotkeysConfigured} hotkeys are active. Press {input.RegionHotkey} to capture a region.";
            items.Add(new ReadinessItem("Hotkeys are ready", detail, ReadinessLevel.Ok));
        }

        if (input.IsMacOS)
        {
            items.Add(input.MacScreenRecordingGranted == false
                ? new ReadinessItem(
                    "Screen Recording permission needed",
                    "macOS shows only your wallpaper until you allow XerahS under System Settings → Privacy & Security → Screen Recording, then restart XerahS. " +
                    "Hotkeys also need Accessibility permission.",
                    ReadinessLevel.Attention)
                : new ReadinessItem(
                    "Screen Recording is allowed",
                    "If hotkeys do nothing, also allow XerahS under Privacy & Security → Accessibility.",
                    ReadinessLevel.Ok));
        }

        if (input.IsLinux && string.Equals(input.LinuxSessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new ReadinessItem(
                "You are on Wayland",
                "Captures go through the desktop portal, so your desktop may ask for permission the first time. " +
                "Screen recording is the least finished part on Linux; screenshots are the most reliable.",
                ReadinessLevel.Info));
        }

        if (!input.FfmpegAvailable)
        {
            items.Add(new ReadinessItem(
                "FFmpeg is not installed",
                "Screen recording, GIFs and some image formats need it. XerahS can download it for you from Application Settings.",
                ReadinessLevel.Info));
        }

        return items;
    }

    /// <summary>Reads the live machine state.</summary>
    public static ReadinessInput Gather()
    {
        string saveFolder = string.Empty;
        bool writable = false;
        try
        {
            saveFolder = TaskHelpers.GetScreenshotsFolder();
            FileHelpers.CreateDirectory(saveFolder);
            string probe = Path.Combine(saveFolder, $".xerahs-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            writable = true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[Onboarding] Save folder check failed: {ex.Message}");
        }

        List<WorkflowSettings> workflows = SettingsManager.WorkflowsConfig?.Hotkeys ?? [];
        List<WorkflowSettings> withHotkey = workflows.Where(w => w.Enabled && w.HotkeyInfo.IsValid).ToList();
        int failed = withHotkey.Count(w => w.HotkeyInfo.Status == XerahS.Platform.Abstractions.HotkeyStatus.Failed);
        string? regionHotkey = withHotkey
            .FirstOrDefault(w => w.Job is WorkflowType.RectangleRegion or WorkflowType.RectangleTransparent)
            ?.HotkeyInfo.ToString();

        bool? macGranted = null;
        if (OperatingSystem.IsMacOS())
        {
            macGranted = MacPermissionProbe?.Invoke();
        }

        bool ffmpeg = false;
        try
        {
            string path = PathsManager.GetFFmpegPath();
            ffmpeg = !string.IsNullOrEmpty(path) && File.Exists(path);
        }
        catch
        {
            // Treated as not installed.
        }

        return new ReadinessInput(
            saveFolder,
            writable,
            withHotkey.Count,
            failed,
            regionHotkey,
            OperatingSystem.IsMacOS(),
            macGranted,
            OperatingSystem.IsLinux(),
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            ffmpeg);
    }

    /// <summary>
    /// Set by the macOS platform layer at startup to answer "does XerahS hold Screen Recording permission",
    /// so this UI project does not depend on the macOS assembly.
    /// </summary>
    public static Func<bool>? MacPermissionProbe { get; set; }
}
