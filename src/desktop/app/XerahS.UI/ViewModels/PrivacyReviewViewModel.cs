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
using ShareX.ImageEditor.Presentation.Rendering;
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class PrivacyReviewViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly SKBitmap _original;
    private SKBitmap _candidate;
    private readonly PrivacyScanResult _scan;
    private readonly TaskSettingsPrivacy _policy;
    private readonly IUIService _ui;
    public string Destination { get; }
    public string ScanSummary { get; }
    public string Findings { get; }
    public IReadOnlyList<PrivacyRedactionEffect> Effects { get; } = Enum.GetValues<PrivacyRedactionEffect>();
    public Bitmap OriginalPreview { get; }
    [ObservableProperty] private Bitmap _preview;
    [ObservableProperty] private bool _canUpload;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private bool _canChangeEffect = true;
    [ObservableProperty] private PrivacyRedactionEffect _effect;
    public Action<PrivacyReviewResult>? CloseRequested { get; set; }

    public PrivacyReviewViewModel(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan, string destination,
        TaskSettingsPrivacy policy, IUIService ui)
    {
        _original = original.Copy();
        _candidate = candidate.Copy();
        _scan = scan;
        _policy = policy;
        _ui = ui;
        Destination = $"Upload destination: {destination}";
        ScanSummary = scan.Status == PrivacyScanStatus.ManuallyReviewed ? "This image was manually reviewed. Confirm sharing with this destination." : scan.Status == PrivacyScanStatus.Complete
            ? $"AI scan completed. {scan.Findings.Count} sensitive regions found. Review the entire preview before uploading."
            : "AI scan unavailable or incomplete. Use Annotate to review and redact manually before uploading.";
        if (scan.Limitation != null) ScanSummary += " " + scan.Limitation;
        Findings = string.Join(", ", scan.Findings.GroupBy(f => f.Category).Select(g => $"{g.Key}: {g.Count()}"));
        _effect = policy.Effect;
        CanUpload = scan.Status is PrivacyScanStatus.Complete or PrivacyScanStatus.ManuallyReviewed;
        CanChangeEffect = scan.Status != PrivacyScanStatus.ManuallyReviewed;
        OriginalPreview = BitmapConversionHelpers.ToAvaloniBitmap(_original);
        _preview = BitmapConversionHelpers.ToAvaloniBitmap(_candidate);
    }

    partial void OnEffectChanged(PrivacyRedactionEffect value)
    {
        if (Busy || _disposed || !CanChangeEffect) return;
        var settings = new TaskSettingsPrivacy { Effect = value, MarginPixels = _policy.MarginPixels, BlurRadius = _policy.BlurRadius };
        ReplaceCandidate(RegionRedactionRenderer.Render(_original, _scan.Findings, settings));
        // Changing effects discards manual edits, so an incomplete scan needs manual review again.
        CanUpload = _scan.Status == PrivacyScanStatus.Complete;
    }
    private void ReplaceCandidate(SKBitmap image)
    {
        if (_disposed) { image.Dispose(); return; }
        _candidate.Dispose();
        _candidate = image;
        Preview.Dispose();
        Preview = BitmapConversionHelpers.ToAvaloniBitmap(image);
    }
    [RelayCommand]
    private async Task AnnotateAsync()
    {
        Busy = true;
        try
        {
            var result = await _ui.ShowPrivacyEditorAsync(_candidate);
            if (result?.RenderedImage != null)
            {
                ReplaceCandidate(result.RenderedImage.Copy());
                CanUpload = true;
                CanChangeEffect = false;
                if (!ReferenceEquals(result.RenderedImage, _candidate)) result.RenderedImage.Dispose();
                if (!ReferenceEquals(result.SourceImage, result.RenderedImage)) result.SourceImage?.Dispose();
            }
        }
        finally { Busy = false; }
    }
    [RelayCommand] private void Upload() { if (CanUpload && !Busy) CloseRequested?.Invoke(new(PrivacyReviewDecision.Upload, _candidate.Copy())); }
    [RelayCommand] private void KeepLocal() { if (!Busy) CloseRequested?.Invoke(new(PrivacyReviewDecision.KeepLocal, _candidate.Copy())); }
    [RelayCommand] private void Cancel() => CloseRequested?.Invoke(new(PrivacyReviewDecision.Cancel, null));
    public void Dispose() { _disposed = true; _original.Dispose(); _candidate.Dispose(); OriginalPreview.Dispose(); Preview.Dispose(); }
}
