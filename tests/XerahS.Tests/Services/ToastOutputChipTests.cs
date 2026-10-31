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

using NUnit.Framework;
using ShareX.ImageEditor.Presentation.Theming;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Services;

[TestFixture]
public class ToastOutputChipTests
{
    // isError, url, fileExists, uploadAttempted, copiedToClipboard, expected
    [TestCase(false, "https://i.example/a.png", true, true, true, ToastOutputKind.Uploaded, TestName = "Uploaded_WhenLinkExists")]
    [TestCase(false, null, true, false, true, ToastOutputKind.Local, TestName = "Local_WhenSavedAndNoUploadAttempted")]
    [TestCase(false, null, true, true, true, ToastOutputKind.UploadFailedLocal, TestName = "UploadFailedLocal_WhenUploadAttemptedButNoLink")]
    [TestCase(false, null, false, false, true, ToastOutputKind.ClipboardOnly, TestName = "ClipboardOnly_WhenNoFileNoLink")]
    [TestCase(true, "https://i.example/a.png", true, true, true, ToastOutputKind.None, TestName = "None_WhenTaskFailed")]
    [TestCase(false, null, false, false, false, ToastOutputKind.None, TestName = "None_WhenNoResult")]
    [TestCase(false, "   ", false, true, false, ToastOutputKind.None, TestName = "None_WhenBlankLinkAndNothingElse")]
    public void Resolve_PicksExpectedKind(bool isError, string? url, bool fileExists, bool uploadAttempted, bool copied, ToastOutputKind expected)
    {
        Assert.That(ToastOutputClassifier.Resolve(isError, url, fileExists, uploadAttempted, copied), Is.EqualTo(expected));
    }

    [Test]
    public void ToastConfig_DefaultsToNoChip()
    {
        Assert.That(new ToastConfig().OutputKind, Is.EqualTo(ToastOutputKind.None));
    }

    [TestCase(ToastOutputKind.Uploaded, "Online")]
    [TestCase(ToastOutputKind.Local, "Local")]
    [TestCase(ToastOutputKind.UploadFailedLocal, "Not uploaded")]
    [TestCase(ToastOutputKind.ClipboardOnly, "Clipboard")]
    [TestCase(ToastOutputKind.None, "")]
    public void ChipLabel_MatchesSpec(ToastOutputKind kind, string label)
    {
        Assert.That(ToastViewModel.GetOutputChipLabel(kind), Is.EqualTo(label));
    }

    [Test]
    public void ChipIcon_MatchesSpec()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToastViewModel.GetOutputChipIcon(ToastOutputKind.Uploaded), Is.EqualTo(LucideIcons.link));
            Assert.That(ToastViewModel.GetOutputChipIcon(ToastOutputKind.Local), Is.EqualTo(LucideIcons.hard_drive));
            Assert.That(ToastViewModel.GetOutputChipIcon(ToastOutputKind.UploadFailedLocal), Is.EqualTo(LucideIcons.cloud_off));
            Assert.That(ToastViewModel.GetOutputChipIcon(ToastOutputKind.ClipboardOnly), Is.EqualTo(LucideIcons.clipboard));
            Assert.That(ToastViewModel.GetOutputChipIcon(ToastOutputKind.None), Is.Empty);
        });
    }

    [TestCase(ToastOutputKind.Uploaded, "https://i.example/a.png", "/tmp/a.png", "Link: https://i.example/a.png")]
    [TestCase(ToastOutputKind.Local, null, "/tmp/a.png", "Saved to: /tmp/a.png")]
    [TestCase(ToastOutputKind.UploadFailedLocal, null, "/tmp/a.png", "Upload failed, saved to: /tmp/a.png")]
    [TestCase(ToastOutputKind.None, "https://i.example/a.png", null, "https://i.example/a.png")]
    [TestCase(ToastOutputKind.None, null, "/tmp/a.png", "/tmp/a.png")]
    [TestCase(ToastOutputKind.ClipboardOnly, null, null, null)]
    public void HeaderText_UsesSpecPrefixes(ToastOutputKind kind, string? url, string? filePath, string? expected)
    {
        Assert.That(ToastViewModel.FormatHeaderText(kind, url, filePath), Is.EqualTo(expected));
    }
}
