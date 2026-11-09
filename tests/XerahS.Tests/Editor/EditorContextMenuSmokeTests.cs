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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;

namespace XerahS.Tests.Editor;

[TestFixture]
[SetUICulture("en-US")]
public class EditorContextMenuSmokeTests
{
    [AvaloniaTest]
    public void AnnotationToolbar_TooltipUsesTextFont_WhileButtonUsesIconFont()
    {
        AssertToolbarTooltipFonts(includeHostStyles: false);
    }

    [AvaloniaTest]
    public void AnnotationToolbar_HostedTooltipUsesTextFont_WhileButtonUsesIconFont()
    {
        AssertToolbarTooltipFonts(includeHostStyles: true);
    }

    [AvaloniaTest]
    public void EditorView_Uses_ContextMenu_For_Context_Actions()
    {
        var view = new EditorView
        {
            DataContext = new MainViewModel()
        };

        Assert.That(view.Resources["EditorContextMenu"], Is.InstanceOf<ContextMenu>());
    }

    private static void AssertToolbarTooltipFonts(bool includeHostStyles)
    {
        var toolbar = new AnnotationToolbar
        {
            DataContext = new EditorToolbarAdapter(new MainViewModel()),
            ShowToolOptionsPanel = false
        };
        var host = new Grid { Children = { toolbar } };
        var window = new Window { Width = 1400, Height = 200, Content = host };
        window.Styles.Add(new StyleInclude(new Uri("avares://ShareX.ImageEditor/"))
        {
            Source = new Uri("avares://ShareX.ImageEditor/Presentation/Theming/ImageEditorStyles.axaml")
        });

        if (includeHostStyles)
        {
            host.Classes.Add("xerahs-editor-host");
            window.Styles.Add(new StyleInclude(new Uri("avares://XerahS.UI/"))
            {
                Source = new Uri("avares://XerahS.UI/Themes/ThemeResources.axaml")
            });
        }

        Button? button = null;
        try
        {
            window.Show();
            window.UpdateLayout();
            button = toolbar.GetVisualDescendants().OfType<Button>().Single(control =>
                control.Tag is ToolbarCustomizationItemViewModel { Tool: EditorTool.Blur });
            Assert.That(ToolTip.GetTip(button), Is.EqualTo("Blur (B, NumPad8)"));

            // Keep the real toolbar content and open a real popup so ancestor selectors
            // can affect its generated text just as they do when hovering the button.
            var tooltip = new ToolTip { Content = ToolTip.GetTip(button) };
            ToolTip.SetTip(button, tooltip);
            ToolTip.SetIsOpen(button, true);
            Dispatcher.UIThread.RunJobs();
            tooltip.UpdateLayout();

            var tooltipText = tooltip.GetVisualDescendants().OfType<TextBlock>().Single();
            var iconText = button.GetVisualDescendants().OfType<TextBlock>().Single();
            var iconFont = (FontFamily)toolbar.FindResource("ShareX.FontFamily.Icon")!;
            var textFont = (FontFamily)toolbar.FindResource("ShareX.FontFamily.Default")!;

            Assert.Multiple(() =>
            {
                Assert.That(tooltipText.Text, Is.EqualTo("Blur (B, NumPad8)"));
                Assert.That(tooltipText.FontFamily, Is.EqualTo(textFont));
                Assert.That(iconText.FontFamily, Is.EqualTo(iconFont));
            });
        }
        finally
        {
            if (button != null)
            {
                ToolTip.SetIsOpen(button, false);
            }

            window.Close();
        }
    }
}
