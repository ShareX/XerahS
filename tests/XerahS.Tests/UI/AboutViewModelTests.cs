using System.Reflection;
using NUnit.Framework;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class AboutViewModelTests
{
    [Test]
    public void Constructor_DetectsShareXAvaloniaForkMetadataAndAssemblies()
    {
        var vm = new AboutViewModel();

        LoadedAssemblyInfoViewModel? avaloniaBase = vm.ThirdPartyAssemblies
            .FirstOrDefault(info => string.Equals(info.Name, "Avalonia.Base", StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsCustomAvaloniaFork, Is.True);
            Assert.That(vm.AvaloniaVersion, Is.EqualTo("12.1.7-sharex.1"));
            Assert.That(vm.AvaloniaSummary, Does.Contain("12.1.7-sharex.1"));
            Assert.That(vm.AvaloniaSummary, Does.Contain("ShareX Fork"));
            Assert.That(vm.AvaloniaDisplayText, Does.Contain("12.1.7-sharex.1"));
            Assert.That(vm.AvaloniaDisplayText, Does.Contain("ShareX Fork"));
            Assert.That(vm.AvaloniaRepositoryUrl, Does.Contain("github.com/ShareX/Avalonia"));
            Assert.That(vm.WindowingBackend, Is.Not.Null.And.Not.Empty);
            Assert.That(vm.SkiaSharpVersion, Does.StartWith("4.155.0"));
            Assert.That(vm.DotNetRuntime, Is.Not.Null.And.Not.Empty);
            Assert.That(avaloniaBase, Is.Not.Null);
            Assert.That(avaloniaBase!.Version, Is.EqualTo("12.1.7-sharex.1"));
        });
    }

    [Test]
    public void FormatAssemblyVersion_PreservesPrereleaseAndFallsBackCleanly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AboutViewModel.FormatAssemblyVersion("12.1.7-sharex.1+d893ea498", new Version(12, 1, 7, 0)),
                Is.EqualTo("12.1.7-sharex.1"));
            Assert.That(
                AboutViewModel.FormatAssemblyVersion("10.0.5+abcdef", new Version(10, 0, 5, 0)),
                Is.EqualTo("10.0.5"));
            Assert.That(
                AboutViewModel.FormatAssemblyVersion(null, new Version(4, 155, 0, 0)),
                Is.EqualTo("4.155.0"));
            Assert.That(
                AboutViewModel.FormatAssemblyVersion("", new Version(1, 2, 3, 4)),
                Is.EqualTo("1.2.3.4"));
        });
    }
}
