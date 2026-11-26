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

using Avalonia.Media;
using NUnit.Framework;
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.Core;
using XerahS.UI.Services;

namespace XerahS.Tests.Editor;

[TestFixture]
[NonParallelizable]
public class PersistedEditorOptionsTests
{
    [Test]
    public void GetPersistedImageEditorOptions_ReturnsTheStoredAnnotationOptions()
    {
        var stored = SettingsManager.DefaultTaskSettings.CaptureSettings.RegionCaptureOptions.AnnotationOptions;

        var options = ThemeService.GetPersistedImageEditorOptions(showExitConfirmation: false);

        Assert.That(options, Is.SameAs(stored));
        Assert.That(options.ShowExitConfirmation, Is.False);
    }

    [Test]
    public void ToolOptionChangedInOneEditor_IsUsedByTheNextEditor()
    {
        var options = ThemeService.GetPersistedImageEditorOptions();
        Color originalColor = options.SpeechBalloonFillColor;
        float originalSize = options.SpeechBalloonFontSize;

        try
        {
            var first = new MainViewModel(options);
            first.Options.SpeechBalloonFillColor = Color.FromArgb(255, 0, 170, 0);
            first.Options.SpeechBalloonFontSize = 30;

            var second = new MainViewModel(ThemeService.GetPersistedImageEditorOptions());

            Assert.That(second.Options.SpeechBalloonFillColor, Is.EqualTo(Color.FromArgb(255, 0, 170, 0)));
            Assert.That(second.Options.SpeechBalloonFontSize, Is.EqualTo(30f));
        }
        finally
        {
            options.SpeechBalloonFillColor = originalColor;
            options.SpeechBalloonFontSize = originalSize;
        }
    }
}
