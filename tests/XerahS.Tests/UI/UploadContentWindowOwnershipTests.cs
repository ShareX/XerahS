using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Tests.Xip0052;
using XerahS.UI.Services;
using XerahS.UI.Views;

namespace XerahS.Tests.UI;

[TestFixture, NonParallelizable]
public class UploadContentWindowOwnershipTests
{
    [AvaloniaTest]
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Open_UsesOnlyAVisibleOwner_AndRespectsBackgroundMode(bool ownerVisible, bool background)
    {
        UiViewModelFactoryAccessor.Configure(new FakeUiViewModelFactory());
        var owner = new Window { Width = 300, Height = 200 };
        UploadContentWindow? opened = null;
        using var subscription = Window.WindowOpenedEvent.AddClassHandler<UploadContentWindow>((window, _) => opened = window);
        try
        {
            if (ownerVisible) owner.Show();
            await UploadContentToolService.HandleWorkflowAsync(WorkflowType.None, owner, background);
            Assert.That(opened, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(opened!.IsVisible, Is.True);
                Assert.That(opened.Owner, ownerVisible ? Is.SameAs(owner) : Is.Null);
                Assert.That(opened.ShowActivated, Is.EqualTo(!background));
                Assert.That(owner.IsVisible, Is.EqualTo(ownerVisible), "Opening the clipboard viewer must not raise the tray-hidden main window.");
            });
        }
        finally
        {
            opened?.Close();
            (opened?.DataContext as IDisposable)?.Dispose();
            owner.Close();
            UiViewModelFactoryAccessor.Reset();
        }
    }
}
