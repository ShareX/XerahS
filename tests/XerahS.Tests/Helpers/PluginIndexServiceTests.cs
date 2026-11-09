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

using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using NUnit.Framework;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class PluginIndexServiceTests
{
    [Test]
    public void DefaultIndexUrl_UsesShareXDevelopRegistry()
    {
        Assert.That(
            PluginIndexService.DefaultIndexUrl,
            Is.EqualTo("https://raw.githubusercontent.com/ShareX/XerahS/refs/heads/develop/plugins/destinations/plugins-index.json"));
    }

    [Test]
    public void ParseIndex_AcceptsValidCommunityPluginIndex()
    {
        string json = CreateIndexJson("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        var index = PluginIndexService.ParseIndex(json);

        Assert.Multiple(() =>
        {
            Assert.That(index.IndexVersion, Is.EqualTo("1.0"));
            Assert.That(index.Plugins, Has.Count.EqualTo(1));
            Assert.That(index.Plugins[0].PluginId, Is.EqualTo("pixelfox"));
            Assert.That(index.Plugins[0].DownloadUrl, Does.EndWith(".xsdp"));
        });
    }

    [Test]
    public void ParseIndex_RejectsNonHttpsDownloadUrl()
    {
        string json = CreateIndexJson(
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            downloadUrl: "http://example.com/pixelfox.xsdp");

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain("HTTPS is required"));
    }

    [Test]
    public void ParseIndex_RejectsMissingChecksum()
    {
        string json = CreateIndexJson(string.Empty);

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain("sha256 checksum"));
    }

    [Test]
    public void ParseIndex_AcceptsDraftPluginsWithoutPackageMetadata()
    {
        string json = CreateIndexJson(string.Empty, downloadUrl: string.Empty, isDraft: true);

        var index = PluginIndexService.ParseIndex(json);

        Assert.Multiple(() =>
        {
            Assert.That(index.Plugins, Has.Count.EqualTo(1));
            Assert.That(index.Plugins[0].IsDraft, Is.True);
        });
    }

    [Test]
    public async Task FetchIndexAsync_RequiresHttpsIndexUrl()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler("{}"));
        var service = new PluginIndexService(httpClient, "http://example.com/plugins-index.json");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchIndexAsync());

        Assert.That(ex!.Message, Does.Contain("HTTPS"));
        await Task.CompletedTask;
    }

    [Test]
    public void ParseIndex_RejectsDuplicatePluginIds()
    {
        string validChecksum = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string json = $$"""
        {
          "indexVersion": "1.0",
          "lastUpdated": "2026-04-26T00:00:00Z",
          "plugins": [
            {
              "pluginId": "pixelfox",
              "name": "Pixelfox",
              "version": "1.0.0",
              "author": "Pixelfox",
              "description": "First entry.",
              "apiVersion": "1.0",
              "supportedCategories": ["Image"],
              "homepageUrl": "https://pixelfox.cc",
              "downloadUrl": "https://example.com/pixelfox.xsdp",
              "checksum": "{{validChecksum}}",
              "isDraft": false,
              "minAppVersion": "1.0.0",
              "dependencies": []
            },
            {
              "pluginId": "pixelfox",
              "name": "Pixelfox Duplicate",
              "version": "1.0.1",
              "author": "Pixelfox",
              "description": "Duplicate entry with same pluginId.",
              "apiVersion": "1.0",
              "supportedCategories": ["Image"],
              "homepageUrl": "https://pixelfox.cc",
              "downloadUrl": "https://example.com/pixelfox2.xsdp",
              "checksum": "{{validChecksum}}",
              "isDraft": false,
              "minAppVersion": "1.0.0",
              "dependencies": []
            }
          ]
        }
        """;

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain("Duplicate pluginId"));
    }

    [Test]
    public void ParseIndex_RejectsUnsupportedApiVersion()
    {
        string json = CreateIndexJson(
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            apiVersion: "99.0");

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain("unsupported API version"));
    }

    [Test]
    public void ParseIndex_RejectsNonXsdpPackageUrl()
    {
        string json = CreateIndexJson(
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            downloadUrl: "https://example.com/pixelfox.zip");

        var ex = Assert.Throws<InvalidDataException>(() => PluginIndexService.ParseIndex(json));

        Assert.That(ex!.Message, Does.Contain(".xsdp"));
    }

    [Test]
    public async Task DownloadPackageAsync_WritesPackageWhenChecksumMatches()
    {
        byte[] packageBytes = "xsdp-package"u8.ToArray();
        var plugin = CreatePluginEntry(CreateSha256(packageBytes));
        using var httpClient = new HttpClient(new ByteArrayResponseHandler(packageBytes));
        var service = new PluginIndexService(httpClient);

        string packagePath = await service.DownloadPackageAsync(plugin);

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(packagePath), Is.True);
                Assert.That(File.ReadAllBytes(packagePath), Is.EqualTo(packageBytes));
                Assert.That(Path.GetExtension(packagePath), Is.EqualTo(".xsdp"));
            });
        }
        finally
        {
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }
        }
    }

    [Test]
    public async Task DownloadPackageAsync_RemovesPackageWhenChecksumMismatches()
    {
        byte[] packageBytes = "xsdp-package"u8.ToArray();
        var plugin = CreatePluginEntry("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        DeleteTempPackages(plugin.PluginId);
        using var httpClient = new HttpClient(new ByteArrayResponseHandler(packageBytes));
        var service = new PluginIndexService(httpClient);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadPackageAsync(plugin));

        try
        {
            Assert.That(ex!.Message, Does.Contain("checksum"));
            Assert.That(Directory.GetFiles(Path.GetTempPath(), $"{plugin.PluginId}-*.xsdp"), Is.Empty);
        }
        finally
        {
            DeleteTempPackages(plugin.PluginId);
        }
    }

    private static string CreateIndexJson(string checksum, string downloadUrl = "https://example.com/pixelfox.xsdp", bool isDraft = false, string apiVersion = "1.0")
    {
        return $$"""
        {
          "indexVersion": "1.0",
          "lastUpdated": "2026-04-26T00:00:00Z",
          "plugins": [
            {
              "pluginId": "pixelfox",
              "name": "Pixelfox",
              "version": "1.0.0",
              "author": "Pixelfox",
              "description": "Pixelfox uploader plugin.",
              "apiVersion": "{{apiVersion}}",
              "supportedCategories": ["Image"],
              "homepageUrl": "https://pixelfox.cc",
              "downloadUrl": "{{downloadUrl}}",
              "checksum": "{{checksum}}",
              "isDraft": {{isDraft.ToString().ToLowerInvariant()}},
              "minAppVersion": "1.0.0",
              "dependencies": []
            }
          ]
        }
        """;
    }

    private static CommunityPluginIndexEntry CreatePluginEntry(string checksum)
    {
        return new CommunityPluginIndexEntry
        {
            PluginId = "pixelfox-test",
            Name = "Pixelfox Test",
            Version = "1.0.0",
            Author = "Pixelfox",
            Description = "Pixelfox uploader plugin.",
            ApiVersion = "1.0",
            SupportedCategories = ["Image"],
            HomepageUrl = "https://pixelfox.cc",
            DownloadUrl = "https://example.com/pixelfox.xsdp",
            Checksum = checksum
        };
    }

    private static string CreateSha256(byte[] bytes)
    {
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    private static void DeleteTempPackages(string pluginId)
    {
        foreach (string filePath in Directory.GetFiles(Path.GetTempPath(), $"{pluginId}-*.xsdp"))
        {
            File.Delete(filePath);
        }
    }

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class ByteArrayResponseHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body)
            });
        }
    }
}
