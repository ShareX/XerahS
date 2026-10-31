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

using Avalonia.Headless.NUnit;
using NUnit.Framework;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Core.ImageEffects.Filters;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.Views.Dialogs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XerahS.Core;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;
using XerahS.UI.Views.Dialogs;

namespace XerahS.Tests.Editor;

[TestFixture]
public class CreativeFilterDialogWiringTests
{
    private static readonly (string Id, System.Type EffectType)[] CreativeFilters =
    [
        ("heat_haze_refraction", typeof(HeatHazeRefractionImageEffect)),
        ("luminance_contour_lines", typeof(LuminanceContourLinesImageEffect)),
        ("nebula_starfield", typeof(NebulaStarfieldImageEffect)),
        ("paper_stencil_mask", typeof(PaperStencilMaskImageEffect)),
        ("riso_print", typeof(RisoPrintImageEffect))
    ];

    [AvaloniaTest]
    public void EffectDialogRegistry_Creates_NewCreativeFilterDialogs()
    {
        Assert.Multiple(() =>
        {
            foreach ((string id, _) in CreativeFilters)
            {
                Assert.That(EffectDialogRegistry.TryCreate(id, out var dialog), Is.True, id);
                Assert.That(dialog, Is.Not.Null, id);
                Assert.That(dialog, Is.AssignableTo<IEffectDialog>(), id);
            }
        });
    }

    [AvaloniaTest]
    public void EffectBrowserPanel_Lists_NewCreativeFilters()
    {
        var panel = new EffectBrowserPanel();
        var filtersCategory = panel.Categories.Single(category => category.Name == "Filters");
        var effectIds = filtersCategory.AllEffects
            .Select(effect => effect.EffectId)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        Assert.Multiple(() =>
        {
            foreach ((string id, _) in CreativeFilters)
            {
                Assert.That(effectIds, Does.Contain(id), id);
            }
        });
    }

    [AvaloniaTest]
    public void ImageEffectsViewModel_Maps_NewCreativeFilterBrowserIds()
    {
        using var editorCore = new EditorCore();
        var viewModel = new ImageEffectsViewModel(new TaskSettingsImage(), editorCore, new NullViewDialogService());

        Assert.Multiple(() =>
        {
            foreach ((string id, System.Type effectType) in CreativeFilters)
            {
                int effectCount = viewModel.Effects.Count;

                Assert.That(viewModel.TryAddEffectByBrowserId(id), Is.True, id);
                Assert.That(viewModel.Effects, Has.Count.EqualTo(effectCount + 1), id);
                Assert.That(viewModel.Effects.Last(), Is.TypeOf(effectType), id);
            }
        });
    }

    private sealed class NullViewDialogService : IViewDialogService
    {
        public Task ShowDialogAsync<TWindow>(object dataContext) where TWindow : class, new() => Task.CompletedTask;
        public Task<TResult?> ShowDialogAsync<TWindow, TResult>(object dataContext) where TWindow : class, new() => Task.FromResult(default(TResult));
        public Task<bool> ShowPluginInstallerAsync(PluginInstallerViewModel viewModel) => Task.FromResult(false);
        public Task<bool> ShowCustomUploaderEditorAsync(CustomUploaderEditorViewModel viewModel) => Task.FromResult(false);
        public Task<bool> ShowWorkflowEditorAsync(WorkflowEditorViewModel viewModel) => Task.FromResult(false);
        public Task ShowImageEffectsBrowserAsync(ImageEffectsViewModel viewModel) => Task.CompletedTask;
        public Task ShowFFmpegOptionsAsync(FFmpegOptionsViewModel viewModel) => Task.CompletedTask;
        public Task ShowMediaBrowserAsync(MediaBrowserViewModel viewModel) => Task.CompletedTask;
        public Task ShowQrCodeGeneratorAsync(QrCodeGeneratorViewModel viewModel) => Task.CompletedTask;
        public Task ShowShareLogsAsync(ShareLogsViewModel viewModel) => Task.CompletedTask;
        public Task<bool> ShowWatchFolderEditorAsync(WatchFolderEditViewModel viewModel) => Task.FromResult(false);
        public Task<OpenImageChoice> ShowOpenImageChoiceAsync() => Task.FromResult(OpenImageChoice.Cancel);
        public Task<string?> ShowFilePickerAsync(string title, IEnumerable<string>? filters = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string defaultExtension, IEnumerable<string>? filters = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowSecretInputAsync(string title, string label) => Task.FromResult<string?>(null);
        public Task<string?> ShowFolderPickerAsync(string title) => Task.FromResult<string?>(null);
        public object? GetMainWindow() => null;
        public IEnumerable<object> GetOpenWindows() => [];
    }
}
