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

using Avalonia.Input;
using XerahS.Common;
using HotkeyInfo = XerahS.Platform.Abstractions.HotkeyInfo;

namespace XerahS.Core.Hotkeys;

/// <summary>
/// Links a hotkey binding to an action type
/// </summary>
public class WorkflowSettings
{
    /// <summary>
    /// Unique identifier for this workflow (SHA-1 hash)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The key binding for this hotkey
    /// </summary>
    public HotkeyInfo HotkeyInfo { get; set; }

    /// <summary>
    /// The action to execute when this hotkey is triggered
    /// </summary>
    /// <summary>
    /// The action to execute when this hotkey is triggered.
    /// Proxies to TaskSettings.Job.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public HotkeyType Job
    {
        get => TaskSettings.Job;
        set => TaskSettings.Job = value;
    }

    /// <summary>
    /// Configuration for the task to execute
    /// </summary>
    public TaskSettings TaskSettings { get; set; }

    /// <summary>
    /// Optional display name for this hotkey
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this hotkey is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    public WorkflowSettings()
    {
        HotkeyInfo = new HotkeyInfo();
        TaskSettings = new TaskSettings();
    }

    public WorkflowSettings(HotkeyType job, HotkeyInfo hotkeyInfo) : this()
    {
        TaskSettings.Job = job;
        HotkeyInfo = hotkeyInfo;
        // Generate ID after properties are set
        EnsureId();
    }

    public WorkflowSettings(HotkeyType job, Key key) : this()
    {
        TaskSettings.Job = job;
        HotkeyInfo = new HotkeyInfo(key);
        // Generate ID after properties are set
        EnsureId();
    }

    public override string ToString()
    {
        return $"{EnumExtensions.GetDescription(Job)}: {HotkeyInfo}";
    }

    /// <summary>
    /// Generate a SHA-1 hash for this workflow based on its content
    /// </summary>
    public string GenerateId()
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();

        // Create a unique string representation of the workflow
        var workflowString = $"{Name ?? string.Empty}|{Job}|{Enabled}|{TaskSettings?.Job}|{TaskSettings?.Description ?? string.Empty}";

        var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(workflowString));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 8);
    }

    /// <summary>
    /// Ensure this workflow has a valid Id, generating one if necessary
    /// </summary>
    public void EnsureId()
    {
        if (string.IsNullOrEmpty(Id))
        {
            Id = GenerateId();
        }
    }
}
