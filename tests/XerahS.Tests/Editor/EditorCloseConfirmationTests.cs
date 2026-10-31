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

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using ShareX.ImageEditor.Core.ImageEffects.Filters;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Tests.Xip0052;
using XerahS.UI.Services;
using EmbeddedEditorView = ShareX.ImageEditor.Presentation.Views.EditorView;
using HostEditorWindow = XerahS.UI.Views.EditorWindow;
using MainWindow = XerahS.UI.Views.MainWindow;

namespace XerahS.Tests.Editor;

[TestFixture]
[NonParallelizable]
public class EditorCloseConfirmationTests
{
    [SetUp]
    public void SetUp()
    {
        // ApplicationSettingsView (instantiated during NavigateToSettings) requires a
        // registered IUiViewModelFactory. Tests in this fixture navigate to the settings
        // page to exercise the shell modal overlay logic, so we install a fake factory
        // before each test and reset that narrow accessor afterwards. NonParallelizable
        // keeps the accessor state from racing other fixtures.
        UiViewModelFactoryAccessor.Configure(new FakeUiViewModelFactory());
    }

    [TearDown]
    public void TearDown()
    {
        UiViewModelFactoryAccessor.Reset();
    }
    [Test]
    public void RequestClose_DoesNotCreateDuplicateConfirmation_WhenModalAlreadyOpen()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions
        {
            ShowExitConfirmation = true
        })
        {
            IsDirty = true
        };

        viewModel.RequestClose();
        object? initialModal = viewModel.ModalContent;

        viewModel.RequestClose();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsModalOpen, Is.True);
            Assert.That(initialModal, Is.Not.Null);
            Assert.That(viewModel.ModalContent, Is.SameAs(initialModal));
        });
    }

    [AvaloniaTest]
    public void MainWindow_Hides_Shell_ModalOverlay_For_Embedded_Editor_Modal()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions
        {
            ShowExitConfirmation = true
        })
        {
            IsDirty = true
        };
        var taskManager = new FakeDesktopTaskManager();

        var window = new MainWindow(taskManager)
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        try
        {
            window.Show();
            viewModel.RequestClose();

            var contentFrame = window.FindControl<ContentControl>("ContentFrame");
            var overlay = window.FindControl<Grid>("MainWindowModalOverlay");

            Assert.Multiple(() =>
            {
                Assert.That(contentFrame?.Content, Is.TypeOf<EmbeddedEditorView>());
                Assert.That(viewModel.IsModalOpen, Is.True);
                Assert.That(overlay?.IsVisible, Is.False);
            });
        }
        finally
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }

    [Test]
    public void AppModal_IsHostedByShell_NotByEditorOverlay()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions());
        var appDialog = new object();

        viewModel.ModalContent = appDialog;
        viewModel.IsModalOpen = true;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEditorModalOpen, Is.False);
            Assert.That(viewModel.EditorModalContent, Is.Null);
            Assert.That(viewModel.ShellModalContent, Is.SameAs(appDialog));
        });
    }

    [Test]
    public void EditorModal_IsHostedByEditorOverlay_NotByShell()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions());
        var dialog = new ConfirmationDialogViewModel(() => { }, () => { }, () => { });

        viewModel.ModalContent = dialog;
        viewModel.IsModalOpen = true;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEditorModalOpen, Is.True);
            Assert.That(viewModel.EditorModalContent, Is.SameAs(dialog));
            Assert.That(viewModel.ShellModalContent, Is.Null);
        });
    }

    [AvaloniaTest]
    public void MainWindow_Shows_Shell_ModalOverlay_For_App_Dialog_While_Editor_Is_Open()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions());
        var taskManager = new FakeDesktopTaskManager();
        var appDialog = new object();
        var window = new MainWindow(taskManager)
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        try
        {
            window.Show();
            viewModel.ModalContent = appDialog;
            viewModel.IsModalOpen = true;

            var contentFrame = window.FindControl<ContentControl>("ContentFrame");
            var overlay = window.FindControl<Grid>("MainWindowModalOverlay");
            var shellHost = window.FindControl<ContentControl>("ShellModalContentHost");
            var editor = contentFrame?.Content as EmbeddedEditorView;
            var editorOverlay = editor?.FindControl<Border>("EditorModalOverlay");
            var editorHost = editor?.FindControl<ContentControl>("EditorModalContentHost");

            Assert.Multiple(() =>
            {
                Assert.That(contentFrame?.Content, Is.TypeOf<EmbeddedEditorView>());
                Assert.That(overlay?.IsVisible, Is.True);
                Assert.That(shellHost?.Content, Is.SameAs(appDialog));
                Assert.That(editorOverlay?.IsVisible, Is.False);
                Assert.That(editorHost?.Content, Is.Null);
            });
        }
        finally
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }

    [AvaloniaTest]
    public void MainWindow_Shows_Shell_ModalOverlay_For_NonEditor_Content()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions());
        var taskManager = new FakeDesktopTaskManager();
        var window = new MainWindow(taskManager)
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        try
        {
            window.Show();
            window.NavigateToSettings();

            viewModel.ModalContent = new object();
            viewModel.IsModalOpen = true;

            var contentFrame = window.FindControl<ContentControl>("ContentFrame");
            var overlay = window.FindControl<Grid>("MainWindowModalOverlay");

            Assert.Multiple(() =>
            {
                Assert.That(contentFrame?.Content, Is.Not.TypeOf<EmbeddedEditorView>());
                Assert.That(overlay?.IsVisible, Is.True);
            });
        }
        finally
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }

    [AvaloniaTest]
    public void HostEditorWindow_Shows_ExitConfirmation_When_User_Closes_Dirty_Window()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions
        {
            ShowExitConfirmation = true
        })
        {
            IsDirty = true
        };
        var window = new HostEditorWindow
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        try
        {
            window.Show();
            window.Close();

            Assert.Multiple(() =>
            {
                Assert.That(window.IsVisible, Is.True);
                Assert.That(viewModel.IsModalOpen, Is.True);
            });
        }
        finally
        {
            if (viewModel.IsModalOpen)
            {
                viewModel.CloseModalCommand.Execute(null);
            }

            viewModel.IsDirty = false;
            viewModel.RequestClose();

            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }

    [AvaloniaTest]
    public void HostEditorWindow_Shows_ExitConfirmation_After_BorderEffect_Edit()
    {
        var viewModel = new MainViewModel(new ImageEditorOptions
        {
            ShowExitConfirmation = true
        });
        var window = new HostEditorWindow
        {
            Width = 1200,
            Height = 800,
            DataContext = viewModel
        };

        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.CornflowerBlue);

        try
        {
            viewModel.UpdatePreview(bitmap.Copy());
            viewModel.ImageFilePath = "C:\\temp\\history-image.png";
            viewModel.IsDirty = false;

            window.Show();

            viewModel.StartEffectPreview();
            viewModel.ApplyEffect(new BorderImageEffect().Apply, "Applied Border");

            Assert.That(viewModel.IsDirty, Is.True, "Applying a border effect should mark the editor dirty.");

            window.Close();

            Assert.Multiple(() =>
            {
                Assert.That(window.IsVisible, Is.True);
                Assert.That(viewModel.IsModalOpen, Is.True);
            });
        }
        finally
        {
            if (viewModel.IsModalOpen)
            {
                viewModel.CloseModalCommand.Execute(null);
            }

            viewModel.IsDirty = false;
            viewModel.RequestClose();

            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }
}
