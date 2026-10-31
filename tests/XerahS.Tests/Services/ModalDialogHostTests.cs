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

using System.Reflection;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.UI.Services;

namespace XerahS.Tests.Services;

[TestFixture, NonParallelizable]
public class ModalDialogHostTests
{
    [AvaloniaTest]
    public async Task CompletingNestedModalRestoresParentWithoutDismissingItsAwaiter()
    {
        using var host = new HostScope();
        var parent = new object();
        var child = new object();
        Action<bool> closeParent = null!;
        Action<string?> closeChild = null!;
        Task<bool> parentTask = ModalDialogHost.ShowAsync(parent, close => closeParent = close, false, "ParentTest");
        Dispatcher.UIThread.RunJobs();
        Task<string?> childTask = ModalDialogHost.ShowAsync<string?>(child, close => closeChild = close, null, "ChildTest");
        Dispatcher.UIThread.RunJobs();

        Assert.That(host.ViewModel.ModalContent, Is.SameAs(child));
        closeChild("selected effect");

        Assert.Multiple(() =>
        {
            Assert.That(host.ViewModel.IsModalOpen, Is.True);
            Assert.That(host.ViewModel.ModalContent, Is.SameAs(parent));
            Assert.That(parentTask.IsCompleted, Is.False, "Selecting an effect must not dismiss the workflow editor.");
        });
        Assert.That(await childTask, Is.EqualTo("selected effect"));
        closeParent(true);
        Assert.That(await parentTask, Is.True);
        Assert.That(host.ViewModel.IsModalOpen, Is.False);
        Assert.That(host.ViewModel.ModalContent, Is.Null);
    }

    [AvaloniaTest]
    public async Task DismissingOverlayCompletesParentAndChildWithTheirDismissResults()
    {
        using var host = new HostScope();
        Task<bool> parentTask = ModalDialogHost.ShowAsync(new object(), _ => { }, false, "ParentTest");
        Dispatcher.UIThread.RunJobs();
        Task<string?> childTask = ModalDialogHost.ShowAsync<string?>(new object(), _ => { }, null, "ChildTest");
        Dispatcher.UIThread.RunJobs();

        // Escape and backdrop clicks both execute the host's CloseModal command.
        host.ViewModel.CloseModalCommand.Execute(null);

        Assert.That(await parentTask, Is.False);
        Assert.That(await childTask, Is.Null);
        Assert.That(host.ViewModel.IsModalOpen, Is.False);
        Assert.That(host.ViewModel.ModalContent, Is.Null);
    }

    [AvaloniaTest]
    public async Task CompletingSingleModalClosesOverlayAndReturnsItsResult()
    {
        using var host = new HostScope();
        Action<bool> close = null!;
        Task<bool> result = ModalDialogHost.ShowAsync(new object(), callback => close = callback, false, "SingleTest");
        Dispatcher.UIThread.RunJobs();

        close(true);

        Assert.That(await result, Is.True);
        Assert.That(host.ViewModel.IsModalOpen, Is.False);
        Assert.That(host.ViewModel.ModalContent, Is.Null);
    }

    private sealed class HostScope : IDisposable
    {
        private readonly MainViewModel? _previous = MainViewModel.Current;
        public MainViewModel ViewModel { get; } = new();

        public void Dispose()
        {
            ViewModel.CloseModalCommand.Execute(null);
            ViewModel.Dispose();
            typeof(MainViewModel).GetProperty(nameof(MainViewModel.Current), BindingFlags.Public | BindingFlags.Static)!
                .SetValue(null, _previous);
        }
    }
}
