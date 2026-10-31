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

using Avalonia.Threading;
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Services;

public sealed class AvaloniaPrivacyReviewService(IUIService ui) : IPrivacyReviewService
{
    public async Task<bool> ConfirmCloudAnalysisAsync(string provider, CancellationToken token)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var vm = new SimplePromptViewModel
            {
                Title = "Allow AI sensitivity scan?",
                Message = $"Send this unredacted screenshot to {provider} for sensitivity analysis? It may contain private data. " +
                    "The provider's data policy applies and usage may be billed. This permission applies only to this scan. " +
                    "Cancel to use local manual annotation instead. Destination upload requires separate approval.",
                ShowCancel = true, PrimaryButtonText = "Allow this scan", CancelButtonText = "Use manual review"
            };
            using var cancellation = token.Register(() => Dispatcher.UIThread.Post(() => vm.CloseRequested?.Invoke(false)));
            return await ModalDialogHost.ShowAsync(vm, set => vm.CloseRequested = set, false, "PrivacyConsent");
        });
    }
    public async Task<PrivacyReviewResult> ReviewAsync(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan,
        string destination, TaskSettingsPrivacy policy, CancellationToken token)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            using var vm = new PrivacyReviewViewModel(original, candidate, scan, destination, policy, ui);
            using var cancellation = token.Register(() => Dispatcher.UIThread.Post(() => vm.CancelCommand.Execute(null)));
            return await ModalDialogHost.ShowAsync(vm, set => vm.CloseRequested = set,
                new PrivacyReviewResult(PrivacyReviewDecision.Cancel, null), "PrivacyReview");
        });
    }
}
