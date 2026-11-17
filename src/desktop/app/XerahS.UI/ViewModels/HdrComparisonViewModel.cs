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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Common;
using XerahS.UI.Services;

namespace XerahS.UI.ViewModels;

/// <summary>One HDR → SDR rendering of the bundled photograph, listed in the comparison window.</summary>
public sealed class HdrComparisonEntry
{
    public HdrComparisonEntry(HdrToneMappingMode mode)
    {
        Mode = mode;
        Label = HdrPhotoPreviewCache.GetLabel(mode);
    }

    public HdrToneMappingMode Mode { get; }

    public string Label { get; }

    public Bitmap? Image => HdrPhotoPreviewCache.Get(Mode);
}

/// <summary>
/// Backs the dedicated HDR comparison window: a large view of the selected method, the list of
/// methods, and a way to open the untouched HDR image in an HDR-capable viewer.
/// </summary>
public sealed class HdrComparisonViewModel : ObservableObject
{
    private HdrComparisonEntry _selected;

    public HdrComparisonViewModel()
    {
        Entries = HdrPhotoPreviewCache.Modes.Select(mode => new HdrComparisonEntry(mode)).ToList();
        _selected = Entries.First(entry => entry.Mode == HdrToneMappingMode.TonemapHable);
        OpenInHdrViewerCommand = new RelayCommand(OpenInHdrViewer);
    }

    public IReadOnlyList<HdrComparisonEntry> Entries { get; }

    public HdrComparisonEntry Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(SelectedImage));
            }
        }
    }

    public Bitmap? SelectedImage => Selected.Image;

    /// <summary>
    /// True when the platform can embed an HDR surface next to the tone-mapped view, which is what
    /// makes comparing them possible without raising a second window.
    /// </summary>
    public bool NativePreviewAvailable =>
        XerahS.Platform.Abstractions.PlatformServices.HdrSurfaces is { IsSupported: true };

    public string Legend => XerahS.Common.Resources.GetString("TaskSettings_HdrPhotoLegend");

    public string OpenHint => XerahS.Common.Resources.GetString("TaskSettings_HdrOpenPreviewHint");

    public IRelayCommand OpenInHdrViewerCommand { get; }

    private void OpenInHdrViewer()
    {
        HdrPhotoPreviewCache.OpenInHdrViewer(XerahS.Common.Resources.GetString("HdrComparison_HdrWindowTitle"));
    }
}
