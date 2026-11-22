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
using ShareX.AmazonS3.Plugin;
using XerahS.App;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Tests.Xip0052;
using XerahS.UI.Services;

namespace XerahS.Tests.Common;

/// <summary>Regression tests for the XIP0088 Phase 0 fixes.</summary>
[TestFixture]
[NonParallelizable]
public class Xip0088Phase0Tests
{
    private string _dir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"xerahs-xip0088-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }

    // Item 5: handed-off files are validated before upload.
    [Test]
    public async Task FileReadiness_MissingFile_ReportsMissingQuickly()
    {
        string path = Path.Combine(_dir, "screenshot-2026-09-27_06-11-45.png");

        FileReadinessStatus status = await FileReadiness.WaitUntilReadyAsync(path, TimeSpan.FromMilliseconds(300));

        Assert.That(status, Is.EqualTo(FileReadinessStatus.Missing));
        Assert.That(FileReadiness.Describe(status, path), Does.Contain("File not found").And.Contain(path));
    }

    [Test]
    public async Task FileReadiness_FileThatAppearsDuringTheWait_IsReady()
    {
        string path = Path.Combine(_dir, "late.png");
        Task writer = Task.Run(async () =>
        {
            await Task.Delay(250);
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        });

        FileReadinessStatus status = await FileReadiness.WaitUntilReadyAsync(path, TimeSpan.FromSeconds(3));
        await writer;

        Assert.That(status, Is.EqualTo(FileReadinessStatus.Ready));
    }

    [Test]
    public async Task FileReadiness_EmptyFileAndFolder_AreRejected()
    {
        string empty = Path.Combine(_dir, "empty.png");
        await File.WriteAllBytesAsync(empty, []);

        Assert.That(await FileReadiness.WaitUntilReadyAsync(empty, TimeSpan.FromMilliseconds(300)), Is.EqualTo(FileReadinessStatus.Empty));
        Assert.That(await FileReadiness.WaitUntilReadyAsync(_dir, TimeSpan.FromMilliseconds(300)), Is.EqualTo(FileReadinessStatus.NotAFile));
    }

    // Item 6: Wayland-only startup.
    [Test]
    public void DisplayBootstrap_UnsetDisplay_AdoptsLowestXWaylandSocket()
    {
        var result = LinuxDisplayBootstrap.Resolve(_ => null, () => ["/tmp/.X11-unix/X1", "/tmp/.X11-unix/X0", "/tmp/.X11-unix/lock"], _ => true);

        Assert.That(result.Usable, Is.True);
        Assert.That(result.Display, Is.EqualTo(":0"));
        Assert.That(result.DisplayAdopted, Is.True);
    }

    [Test]
    public void DisplayBootstrap_NoXServer_IsReportedReadably()
    {
        var result = LinuxDisplayBootstrap.Resolve(_ => null, () => [], _ => false);

        Assert.That(result.Usable, Is.False);
        Assert.That(LinuxDisplayBootstrap.BuildUserMessage(result), Does.Contain("XWayland").And.Contain("xwayland { enabled = true }"));
    }

    [TestCase(":0", true)]
    [TestCase(":1.0", true)]
    [TestCase("remotehost:0", true)]
    public void DisplayBootstrap_ExplicitDisplay_IsAlwaysTried(string display, bool usable)
    {
        var result = LinuxDisplayBootstrap.Resolve(k => k == "DISPLAY" ? display : null, () => [], _ => false);

        Assert.That(result.Usable, Is.EqualTo(usable));
        Assert.That(result.DisplayAdopted, Is.False);
    }

    [Test]
    public void DisplayBootstrap_SocketPathParsing()
    {
        Assert.That(LinuxDisplayBootstrap.GetX11SocketPath(":0"), Is.EqualTo("/tmp/.X11-unix/X0"));
        Assert.That(LinuxDisplayBootstrap.GetX11SocketPath(":12.0"), Is.EqualTo("/tmp/.X11-unix/X12"));
        Assert.That(LinuxDisplayBootstrap.GetX11SocketPath("host:0"), Is.Null);
    }

    [Test]
    public void DisplayBootstrap_WaylandSocketWithoutXWayland_IsUsable()
    {
        var result = LinuxDisplayBootstrap.Resolve(
            k => k switch
            {
                "WAYLAND_DISPLAY" => "wayland-1",
                "XDG_RUNTIME_DIR" => "/run/user/1000",
                _ => null
            },
            () => [],
            path => path == "/run/user/1000/wayland-1");

        Assert.That(result.Usable, Is.True);
        Assert.That(result.Display, Is.EqualTo("wayland-1"));
        Assert.That(result.DisplayAdopted, Is.False);
    }

    [Test]
    public void DisplayBootstrap_WaylandDisabledWithoutXWayland_RequiresX11()
    {
        var result = LinuxDisplayBootstrap.Resolve(
            k => k switch
            {
                "XERAHS_DISABLE_WAYLAND" => "1",
                "WAYLAND_DISPLAY" => "wayland-1",
                "XDG_RUNTIME_DIR" => "/run/user/1000",
                _ => null
            },
            () => [],
            path => path == "/run/user/1000/wayland-1");

        Assert.That(result.Usable, Is.False);
        Assert.That(LinuxDisplayBootstrap.BuildUserMessage(result), Does.Contain("XWayland").And.Contain("xwayland { enabled = true }"));
    }

    // Item 7: Media Browser S3 PermanentRedirect.
    [Test]
    public void S3Redirect_PermanentRedirectBody_YieldsRegionFromEndpoint()
    {
        const string body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>PermanentRedirect</Code>" +
            "<Message>The bucket you are attempting to access must be addressed using the specified endpoint.</Message>" +
            "<Endpoint>my-bucket.s3.ap-southeast-2.amazonaws.com</Endpoint><Bucket>my-bucket</Bucket></Error>";

        var redirect = S3ExplorerListHelper.TryParseRegionRedirect(body, null);

        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.Region, Is.EqualTo("ap-southeast-2"));
        Assert.That(redirect.EndpointHost, Is.EqualTo("my-bucket.s3.ap-southeast-2.amazonaws.com"));
        Assert.That(S3ExplorerListHelper.BuildWrongRegionMessage("my-bucket", redirect, "S3 request failed: PermanentRedirect"),
            Does.Contain("s3.ap-southeast-2.amazonaws.com"));
    }

    [Test]
    public void S3Redirect_HeaderWinsAndAccessDeniedIsNotARedirect()
    {
        const string wrongRegion = "<Error><Code>AuthorizationHeaderMalformed</Code><Region>eu-west-1</Region></Error>";
        const string denied = "<Error><Code>AccessDenied</Code><Message>not authorized to perform: s3:ListBucket</Message></Error>";

        Assert.That(S3ExplorerListHelper.TryParseRegionRedirect(wrongRegion, "eu-central-1")!.Region, Is.EqualTo("eu-central-1"));
        Assert.That(S3ExplorerListHelper.TryParseRegionRedirect(denied, "us-east-1"), Is.Null);
        Assert.That(S3ExplorerListHelper.TryParseRegionRedirect("not xml", null), Is.Null);
    }

    [TestCase("s3.us-west-2.amazonaws.com", "us-west-2")]
    [TestCase("bucket.s3-eu-west-1.amazonaws.com", "eu-west-1")]
    [TestCase("minio.local", null)]
    public void S3Redirect_RegionFromEndpoint(string host, string? expected)
    {
        Assert.That(S3ExplorerListHelper.RegionFromEndpoint(host), Is.EqualTo(expected));
    }

    // Item 4: startup ordering.
    [Test]
    public void UiViewModelFactory_DefersCallbacksUntilConfigured()
    {
        UiViewModelFactoryAccessor.Reset();
        try
        {
            int calls = 0;
            UiViewModelFactoryAccessor.RunWhenAvailable(() => calls++);
            Assert.That(calls, Is.Zero);

            UiViewModelFactoryAccessor.Configure(new FakeUiViewModelFactory());
            Assert.That(calls, Is.EqualTo(1));

            UiViewModelFactoryAccessor.RunWhenAvailable(() => calls++);
            Assert.That(calls, Is.EqualTo(2), "runs immediately once available");
        }
        finally
        {
            UiViewModelFactoryAccessor.Reset();
        }
    }

    [Test]
    public void PlatformServices_DefersCallbacksUntilInitialized_AndResetDropsThem()
    {
        PlatformServices.Reset();
        try
        {
            int calls = 0;
            PlatformServices.RunWhenInitialized(() => calls++);

            Assert.That(PlatformServices.IsInitialized, Is.False);
            Assert.That(calls, Is.Zero, "must not run (or throw) before bootstrap completes");
        }
        finally
        {
            PlatformServices.Reset();
        }
    }
}
