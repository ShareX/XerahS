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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

/// <summary>An uploader instance an extension can be sent to.</summary>
public sealed class OverrideTarget
{
    public OverrideTarget(UploaderInstance instance)
    {
        Instance = instance;
        InstanceId = instance.InstanceId;
        Label = InstanceManager.DescribeInstance(instance);
    }

    public UploaderInstance Instance { get; }

    public string InstanceId { get; }

    /// <summary>The name, with the category only when the name does not already say it.</summary>
    public string Label { get; }

    public string DisplayName => Instance.DisplayName;

    public bool Fits(string extension) => InstanceManager.CanTakeExtension(Instance, extension, out _);

    public override string ToString() => Label;
}

/// <summary>One override row: an extension and where its files go.</summary>
public partial class FileTypeOverrideRowViewModel : ObservableObject
{
    private readonly FileTypeOverridesViewModel _owner;
    private bool _loading;

    public FileTypeOverrideRowViewModel(FileTypeOverridesViewModel owner, string extension, IEnumerable<OverrideTarget> targets, OverrideTarget? target)
    {
        _owner = owner;
        Extension = extension;
        foreach (var t in targets) Targets.Add(t);
        _loading = true;
        Target = target;
        _loading = false;
    }

    public string Extension { get; }

    public string ExtensionLabel => "." + Extension;

    /// <summary>The uploaders that can take this extension, plus the current one if it no longer fits.</summary>
    public ObservableCollection<OverrideTarget> Targets { get; } = new();

    [ObservableProperty]
    private OverrideTarget? _target;

    partial void OnTargetChanged(OverrideTarget? value)
    {
        if (!_loading && value != null)
        {
            _owner.Save(Extension, value);
        }
    }

    [RelayCommand]
    private void Remove() => _owner.Remove(this);
}

/// <summary>
/// File type overrides on the Destinations page: send every file with an extension to one instance,
/// whether it is uploaded as text or as a file.
/// </summary>
public partial class FileTypeOverridesViewModel : ObservableObject
{
    private readonly List<OverrideTarget> _all = new();

    public ObservableCollection<FileTypeOverrideRowViewModel> Rows { get; } = new();

    /// <summary>The uploaders offered for a new override: every one, or only those that fit the typed extension.</summary>
    public ObservableCollection<OverrideTarget> NewTargets { get; } = new();

    [ObservableProperty]
    private string _newExtension = string.Empty;

    [ObservableProperty]
    private OverrideTarget? _newTarget;

    [ObservableProperty]
    private string? _message;

    public bool HasRows => Rows.Count > 0;

    partial void OnNewExtensionChanged(string value) => RefreshNewTargets();

    /// <summary>Reads instances and overrides from settings again.</summary>
    public void Reload()
    {
        var manager = InstanceManager.Instance;
        _all.Clear();
        foreach (var i in manager.GetInstances()
                     .Where(i => !InstanceManager.IsAutoProvider(i.ProviderId))
                     .OrderBy(i => i.Category).ThenBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            _all.Add(new OverrideTarget(i));
        }

        Rows.Clear();
        foreach (var o in manager.GetFileTypeOverrides())
        {
            var current = _all.FirstOrDefault(t => string.Equals(t.InstanceId, o.InstanceId, StringComparison.OrdinalIgnoreCase));
            var fitting = _all.Where(t => t.Fits(o.Extension) || t == current);
            Rows.Add(new FileTypeOverrideRowViewModel(this, o.Extension, fitting, current));
        }

        RefreshNewTargets();
        OnPropertyChanged(nameof(HasRows));
    }

    private void RefreshNewTargets()
    {
        var keep = NewTarget?.InstanceId;
        var ext = InstanceManager.NormalizeOverrideExtension(NewExtension);
        NewTargets.Clear();
        foreach (var t in _all.Where(t => ext == null || t.Fits(ext)))
        {
            NewTargets.Add(t);
        }

        NewTarget = NewTargets.FirstOrDefault(t => t.InstanceId == keep) ?? NewTargets.FirstOrDefault();
    }

    [RelayCommand]
    private void Add()
    {
        var ext = InstanceManager.NormalizeOverrideExtension(NewExtension);
        if (ext == null)
        {
            Message = "Enter an extension such as md, pdf or tar.gz.";
            return;
        }

        var target = NewTarget;
        if (target == null)
        {
            Message = $"No uploader can take .{ext} files. Any File uploader can take every type; add one in File Uploaders.";
            return;
        }

        if (!InstanceManager.CanTakeExtension(target.Instance, ext, out var reason))
        {
            Message = reason;
            return;
        }

        if (!Save(ext, target)) return;
        NewExtension = string.Empty;
        Reload();
        Message = $"Every .{ext} file now goes to {target.Label}.";
    }

    internal bool Save(string extension, OverrideTarget target)
    {
        try
        {
            InstanceManager.Instance.SetFileTypeOverride(extension, target.InstanceId);
            Message = null;
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Failed to save the file type override for .{extension}");
            Message = $"Could not save the override for .{extension}: {ex.Message}";
            return false;
        }
    }

    internal void Remove(FileTypeOverrideRowViewModel row)
    {
        try
        {
            InstanceManager.Instance.RemoveFileTypeOverride(row.Extension);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Failed to remove the file type override for .{row.Extension}");
            Message = $"Could not remove the override for .{row.Extension}: {ex.Message}";
            return;
        }

        Rows.Remove(row);
        OnPropertyChanged(nameof(HasRows));
        Message = $".{row.Extension} files follow the normal destinations again.";
    }
}
