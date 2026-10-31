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
using XerahS.OmaXerahs.Services;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.OmaXerahs;

/// <summary>
/// Covers the routing decisions used by <c>omaxerahs upload</c>:
/// which configured image uploader is considered usable, which one wins when
/// no explicit host is requested, and how a host name picks a specific instance
/// out of the usable set.
/// </summary>
/// <remarks>
/// These tests run against the pure helpers exposed via
/// <see cref="UploadHost.TestAccessor"/> so the production routing semantics
/// are exercised without populating <c>InstanceManager</c> /
/// <c>ProviderCatalog</c> state. The class is annotated
/// <see cref="NonParallelizableAttribute"/> because a future revision may need
/// to interact with global state for negative-path smoke checks.
/// </remarks>
[TestFixture]
[NonParallelizable]
public class UploadHostRoutingTests
{
    private const string AutoProviderId = "auto";
    private const string FtpProviderId = "ftp";
    private const string ImagurProviderId = "imgur";
    private const string S3ProviderId = "amazons3";

    private static UploaderInstance MakeInstance(
        string providerId = FtpProviderId,
        UploaderCategory category = UploaderCategory.Image,
        string displayName = "Default",
        bool isAvailable = true,
        string? instanceId = null,
        DateTime? createdAt = null)
    {
        return new UploaderInstance
        {
            InstanceId = instanceId ?? Guid.NewGuid().ToString("N"),
            ProviderId = providerId,
            Category = category,
            DisplayName = displayName,
            SettingsJson = "{}",
            IsAvailable = isAvailable,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ModifiedAt = createdAt ?? DateTime.UtcNow
        };
    }

    // ---------- IsUsableImageInstance: image-only, available, non-Auto, provider present, valid settings ----------

    [TestCase(".PNG", "capture.png", true)]
    [TestCase(" png ", "capture.PNG", true)]
    [TestCase("png", "capture.jpg", false)]
    [TestCase("zip", "capture.png", false)]
    [TestCase("ico", null, true)]
    [TestCase("webp", null, true)]
    [TestCase("", null, false)]
    public void RoutesImages_RespectsConfiguredTypes(string extension, string? path, bool expected)
    {
        var scope = new FileTypeScope { FileExtensions = [extension] };
        Assert.That(UploadHost.RoutesImages(scope, path), Is.EqualTo(expected));
        Assert.That(UploadHost.RoutesImages(null, path), Is.False);
        scope.AllFileTypes = true;
        Assert.That(UploadHost.RoutesImages(scope, path), Is.True);
    }

    [Test]
    public void IsUsable_AcceptsAnImageInstanceWithAValidProvider()
    {
        var instance = MakeInstance(providerId: FtpProviderId);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: false,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.True);
    }

    [Test]
    public void IsUsable_RejectsNonImageCategoryInstances()
    {
        var instance = MakeInstance(providerId: FtpProviderId, category: UploaderCategory.Text);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: false,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsable_RejectsUnavailableInstancesEvenWhenEverythingElseIsValid()
    {
        var instance = MakeInstance(providerId: FtpProviderId, isAvailable: false);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: false,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsable_RejectsTheAutoProviderRoute()
    {
        // The Auto provider resolves destinations on its own; it must never be
        // returned as a usable image instance by routing.
        var instance = MakeInstance(providerId: AutoProviderId);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: true,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsable_RejectsInstancesWhoseProviderIsMissingFromTheCatalog()
    {
        var instance = MakeInstance(providerId: "ghost-provider");

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: false,
            providerExists: false,
            validateSettings: true);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsable_RejectsInstancesWhoseSettingsValidationReturnsFalse()
    {
        // Provider present, validation rejects (e.g. Amazon S3 without a bucket,
        // Ftp without a host, or any other provider-level invariant).
        var instance = MakeInstance(providerId: S3ProviderId);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: false,
            providerExists: true,
            validateSettings: false);

        Assert.That(ok, Is.False);
    }

    // ---------- File fallback: the XerahS app uploads images to File destinations when no Image one exists ----------

    [Test]
    public void IsUsableForFile_AcceptsAFileCategoryS3Instance()
    {
        var instance = MakeInstance(providerId: S3ProviderId, category: UploaderCategory.File);

        bool ok = UploadHost.TestAccessor.IsUsableInstance(
            instance,
            UploaderCategory.File,
            isAutoProvider: false,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.True);
    }

    [Test]
    public void IsUsableForFile_RejectsTheAutoFileInstance()
    {
        // "Auto (File)" only picks among other destinations; on its own it cannot upload anything.
        var instance = MakeInstance(providerId: AutoProviderId, category: UploaderCategory.File);

        bool ok = UploadHost.TestAccessor.IsUsableInstance(
            instance,
            UploaderCategory.File,
            isAutoProvider: true,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsableForFile_RejectsImageCategoryInstances()
    {
        var instance = MakeInstance(providerId: S3ProviderId, category: UploaderCategory.Image);

        bool ok = UploadHost.TestAccessor.IsUsableInstance(
            instance,
            UploaderCategory.File,
            isAutoProvider: false,
            providerExists: true,
            validateSettings: true);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsable_ShortCircuitsOnCategoryBeforeAskingAboutProviders()
    {
        // Non-Image category short-circuits; any provider/settings state is moot.
        var instance = MakeInstance(providerId: FtpProviderId, category: UploaderCategory.File);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: false,
            providerExists: false,
            validateSettings: false);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void IsUsable_ShortCircuitsOnAutoBeforeAskingAboutProviders()
    {
        var instance = MakeInstance(providerId: AutoProviderId);

        bool ok = UploadHost.TestAccessor.IsUsableImageInstance(
            instance,
            isAutoProvider: true,
            providerExists: false,
            validateSettings: false);

        Assert.That(ok, Is.False);
    }

    // ---------- RouteByDisplayName: host routing used by UploadCommand.ResolveUploadedInstance ----------

    [Test]
    public void RouteByDisplayName_MatchesTheFirstInstanceCaseInsensitively()
    {
        var s3 = MakeInstance(displayName: "My Bucket", instanceId: "s3");
        var imagur = MakeInstance(displayName: "Imagur", instanceId: "img");
        var ftp = MakeInstance(displayName: "Work FTP", instanceId: "ftp");

        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName(
            [s3, imagur, ftp], "IMAGUR");

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.InstanceId, Is.EqualTo("img"));
    }

    [Test]
    public void RouteByDisplayName_ReturnsNullWhenHostIsNull()
    {
        var first = MakeInstance(displayName: "Anything", instanceId: "a");

        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName([first], host: null);

        Assert.That(match, Is.Null);
    }

    [Test]
    public void RouteByDisplayName_ReturnsNullWhenHostIsWhitespace()
    {
        var first = MakeInstance(displayName: "Anything", instanceId: "a");

        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName([first], host: "   ");

        Assert.That(match, Is.Null);
    }

    [Test]
    public void RouteByDisplayName_ReturnsNullWhenNothingMatches()
    {
        var s3 = MakeInstance(displayName: "Bucket A", instanceId: "a");
        var imagur = MakeInstance(displayName: "Imagur", instanceId: "b");

        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName([s3, imagur], "NotConfigured");

        Assert.That(match, Is.Null);
    }

    [Test]
    public void RouteByDisplayName_PrefersTheFirstMatchWhenMultipleInstancesShareAName()
    {
        var primary = MakeInstance(displayName: "Duplicate", instanceId: "first");
        var secondary = MakeInstance(displayName: "Duplicate", instanceId: "second");

        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName(
            [primary, secondary], "duplicate");

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.InstanceId, Is.EqualTo("first"));
    }

    [Test]
    public void RouteByDisplayName_DoesNotThrowOnEmptyInstanceList()
    {
        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName(
            Array.Empty<UploaderInstance>(), "anything");

        Assert.That(match, Is.Null);
    }

    // ---------- PreferDefault: default-instance routing, with newest-instance fallback ----------

    [Test]
    public void PreferDefault_ReturnsTheMarkedDefaultInstance()
    {
        var older = MakeInstance(displayName: "Old", createdAt: DateTime.UtcNow.AddDays(-3));
        var markedDefault = MakeInstance(displayName: "Default", instanceId: "default-id");
        var newer = MakeInstance(displayName: "Newest", createdAt: DateTime.UtcNow.AddDays(1));

        UploaderInstance? picked = UploadHost.TestAccessor.PreferDefault(
            [older, markedDefault, newer],
            defaultInstanceId: "default-id");

        Assert.That(picked, Is.Not.Null);
        Assert.That(picked!.InstanceId, Is.EqualTo("default-id"));
    }

    [Test]
    public void PreferDefault_FallsBackToTheNewestInstanceWhenNoDefaultIsConfigured()
    {
        var older = MakeInstance(displayName: "Old", createdAt: DateTime.UtcNow.AddDays(-3), instanceId: "old");
        var newer = MakeInstance(displayName: "Newest", createdAt: DateTime.UtcNow.AddDays(1), instanceId: "new");

        UploaderInstance? picked = UploadHost.TestAccessor.PreferDefault(
            [older, newer],
            defaultInstanceId: null);

        Assert.That(picked, Is.Not.Null);
        Assert.That(picked!.InstanceId, Is.EqualTo("new"));
    }

    [Test]
    public void PreferDefault_FallsBackToNewestWhenDefaultIdRefersToAnUnknownInstance()
    {
        var older = MakeInstance(displayName: "Old", createdAt: DateTime.UtcNow.AddDays(-3), instanceId: "old");
        var newer = MakeInstance(displayName: "Newest", createdAt: DateTime.UtcNow.AddDays(1), instanceId: "new");

        UploaderInstance? picked = UploadHost.TestAccessor.PreferDefault(
            [older, newer],
            defaultInstanceId: "stale-id-not-in-list");

        Assert.That(picked, Is.Not.Null);
        Assert.That(picked!.InstanceId, Is.EqualTo("new"));
    }

    [Test]
    public void PreferDefault_ReturnsNullForAnEmptyUsableSet()
    {
        UploaderInstance? picked = UploadHost.TestAccessor.PreferDefault(
            Array.Empty<UploaderInstance>(),
            defaultInstanceId: "anything");

        Assert.That(picked, Is.Null);
    }

    [Test]
    public void PreferDefault_DistinguishesInstancesUsingExactInstanceIdMatch()
    {
        // The default pointer is an InstanceId, not a DisplayName. Two instances
        // sharing a DisplayName must still pick the right one by id.
        var duplicatelyNamed = MakeInstance(displayName: "Same Name", instanceId: "first");
        var defaultOne = MakeInstance(displayName: "Same Name", instanceId: "default-id");

        UploaderInstance? picked = UploadHost.TestAccessor.PreferDefault(
            [duplicatelyNamed, defaultOne],
            defaultInstanceId: "default-id");

        Assert.That(picked, Is.Not.Null);
        Assert.That(picked!.InstanceId, Is.EqualTo("default-id"));
    }

    // ---------- End-to-end shape: combining usable-set filter with host routing ----------

    [Test]
    public void Routing_HostNameDoesNotResolveToAnUnusableInstance()
    {
        // Auto-provider instances are filtered out by IsUsableImageInstance so
        // routing must never reach them via host match. This mirrors the order
        // UploadCommand uses: GetUsableImageInstances() THEN host-match.
        var auto = MakeInstance(providerId: AutoProviderId, displayName: "MyHost", instanceId: "auto-id");
        var real = MakeInstance(providerId: ImagurProviderId, displayName: "MyHost", instanceId: "real-id");

        var usable = new[] { auto, real }
            .Where(i => UploadHost.TestAccessor.IsUsableImageInstance(
                i,
                isAutoProvider: string.Equals(i.ProviderId, AutoProviderId, StringComparison.OrdinalIgnoreCase),
                providerExists: true,
                validateSettings: true))
            .ToList();

        UploaderInstance? match = UploadHost.TestAccessor.RouteByDisplayName(usable, "MyHost");

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.InstanceId, Is.EqualTo("real-id"));
    }

    [Test]
    public void Routing_HostMatchStillFallsThroughToTheDefaultWhenNothingMatches()
    {
        var older = MakeInstance(displayName: "Old", instanceId: "old");
        var newest = MakeInstance(displayName: "Newest", instanceId: "new");

        var usable = new[] { older, newest };

        UploaderInstance? routed = UploadHost.TestAccessor.RouteByDisplayName(usable, "Unconfigured Host");
        UploaderInstance? fallback = UploadHost.TestAccessor.PreferDefault(usable, defaultInstanceId: null);

        Assert.Multiple(() =>
        {
            Assert.That(routed, Is.Null, "no instance matches the host");
            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback!.InstanceId, Is.EqualTo("new"),
                "caller falls through to the most recently created instance");
        });
    }
}
