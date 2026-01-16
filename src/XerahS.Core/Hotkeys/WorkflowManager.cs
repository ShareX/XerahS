#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using XerahS.Platform.Abstractions;
using System.Diagnostics;

namespace XerahS.Core.Hotkeys;

/// <summary>
/// High-level hotkey management - orchestrates registration and triggering
/// </summary>
public class WorkflowManager : IDisposable
{
    private readonly IHotkeyService _hotkeyService;
    private readonly Dictionary<ushort, WorkflowSettings> _hotkeyMap = new();
    private bool _disposed;

    /// <summary>
    /// List of all configured hotkeys
    /// </summary>
    public List<WorkflowSettings> Workflows { get; private set; } = new();

    /// <summary>
    /// When true, hotkeys are temporarily disabled
    /// </summary>
    public bool IgnoreHotkeys
    {
        get => _hotkeyService.IsSuspended;
        set => _hotkeyService.IsSuspended = value;
    }

    /// <summary>
    /// Fired when a hotkey is triggered
    /// </summary>
    public event EventHandler<WorkflowSettings>? HotkeyTriggered;

    /// <summary>
    /// Fired when the workflows list is modified (added, removed, reordered)
    /// </summary>
    public event EventHandler? WorkflowsChanged;

    public WorkflowManager(IHotkeyService hotkeyService)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _hotkeyService.HotkeyTriggered += OnHotkeyServiceTriggered;
    }

    private void OnHotkeyServiceTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (_hotkeyMap.TryGetValue(e.HotkeyInfo.Id, out var settings))
        {
            Debug.WriteLine($"HotkeyManager: Triggering {settings}");
            HotkeyTriggered?.Invoke(this, settings);
        }
    }

    /// <summary>
    /// Update hotkeys from configuration
    /// </summary>
    public void UpdateHotkeys(List<WorkflowSettings> hotkeys, bool showFailedHotkeys = false)
    {
        UnregisterAllHotkeys();
        Workflows = hotkeys ?? new List<WorkflowSettings>();
        RegisterAllHotkeys();

        if (showFailedHotkeys)
        {
            ShowFailedHotkeys();
        }

        WorkflowsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Register a single hotkey
    /// </summary>
    public bool RegisterHotkey(WorkflowSettings settings)
    {
        if (!settings.Enabled || !settings.HotkeyInfo.IsValid)
        {
            settings.HotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        // Always try to unregister first if this hotkey has an ID
        // Ignore failures - the hotkey might not have been registered yet
        if (settings.HotkeyInfo.Id != 0)
        {
            UnregisterHotkey(settings); // Don't check result
        }

        bool result = _hotkeyService.RegisterHotkey(settings.HotkeyInfo);

        if (result)
        {
            _hotkeyMap[settings.HotkeyInfo.Id] = settings;
            // Debug.WriteLine($"HotkeyManager: Registered {settings}");
            XerahS.Common.DebugHelper.WriteLine($"Hotkey registered: {settings}");

            if (settings.Job == HotkeyType.CustomWindow)
            {
                XerahS.Common.DebugHelper.WriteLine($"[DEBUG] Registering CustomWindow hotkey. Title='{settings.TaskSettings?.CaptureSettings?.CaptureCustomWindow}'");
            }
        }
        else
        {
            Debug.WriteLine($"HotkeyManager: Failed to register {settings}");
        }

        if (!Workflows.Contains(settings))
        {
            Workflows.Add(settings);
            WorkflowsChanged?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    /// <summary>
    /// Unregister a single hotkey
    /// </summary>
    public bool UnregisterHotkey(WorkflowSettings settings)
    {
        if (settings.HotkeyInfo.Id == 0)
            return false;

        bool result = _hotkeyService.UnregisterHotkey(settings.HotkeyInfo);
        _hotkeyMap.Remove(settings.HotkeyInfo.Id);

        if (Workflows.Contains(settings))
        {
            Workflows.Remove(settings);
            WorkflowsChanged?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    /// <summary>
    /// Register all hotkeys in the list
    /// </summary>
    public void RegisterAllHotkeys()
    {
        foreach (var settings in Workflows.ToList())
        {
            RegisterHotkey(settings);
        }
    }

    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    public void UnregisterAllHotkeys()
    {
        _hotkeyService.UnregisterAll();
        _hotkeyMap.Clear();

        foreach (var settings in Workflows)
        {
            settings.HotkeyInfo.Status = HotkeyStatus.NotConfigured;
            settings.HotkeyInfo.Id = 0;
        }
    }

    /// <summary>
    /// Toggle hotkeys on/off
    /// </summary>
    public void ToggleHotkeys(bool disabled)
    {
        IgnoreHotkeys = disabled;
    }

    /// <summary>
    /// Get list of hotkeys that failed to register
    /// </summary>
    public List<WorkflowSettings> GetFailedHotkeys()
    {
        return Workflows.Where(h => h.HotkeyInfo.Status == HotkeyStatus.Failed).ToList();
    }

    /// <summary>
    /// Show warning for failed hotkeys (placeholder - will be UI-specific)
    /// </summary>
    private void ShowFailedHotkeys()
    {
        var failed = GetFailedHotkeys();
        if (failed.Count > 0)
        {
            Debug.WriteLine($"Warning: {failed.Count} hotkey(s) failed to register:");
            foreach (var h in failed)
            {
                Debug.WriteLine($"  - {h}");
            }
        }
    }

    /// <summary>
    /// Move a workflow from one index to another
    /// </summary>
    public void MoveWorkflow(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Workflows.Count || newIndex < 0 || newIndex >= Workflows.Count)
            return;

        var item = Workflows[oldIndex];
        Workflows.RemoveAt(oldIndex);
        Workflows.Insert(newIndex, item);

        WorkflowsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Get a workflow by its unique ID
    /// </summary>
    /// <param name="id">The workflow ID (SHA-1 hash)</param>
    /// <returns>The workflow settings if found, null otherwise</returns>
    public WorkflowSettings? GetWorkflowById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return Workflows.FirstOrDefault(w => w.Id == id);
    }

    /// <summary>
    /// Get the default hotkey list
    /// </summary>
    public static List<WorkflowSettings> GetDefaultWorkflowList()
    {
        return WorkflowsConfig.GetDefaultWorkflowList();
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnregisterAllHotkeys();
        _hotkeyService.HotkeyTriggered -= OnHotkeyServiceTriggered;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
