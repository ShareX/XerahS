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
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using XerahS.Core;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class UrlDownloadHelperTests
{
    [Test]
    public void ParseGitHubFile_ReadsBlobRawAndRawHostLinks()
    {
        var expected = new GitHubFileReference("KovaForge", "bot-config", "main", "docs/architecture/kovaforge-architecture.html");

        Assert.That(UrlDownloadHelper.ParseGitHubFile(new Uri("https://github.com/KovaForge/bot-config/blob/main/docs/architecture/kovaforge-architecture.html")), Is.EqualTo(expected));
        Assert.That(UrlDownloadHelper.ParseGitHubFile(new Uri("https://github.com/KovaForge/bot-config/raw/main/docs/architecture/kovaforge-architecture.html")), Is.EqualTo(expected));
        Assert.That(UrlDownloadHelper.ParseGitHubFile(new Uri("https://raw.githubusercontent.com/KovaForge/bot-config/main/docs/architecture/kovaforge-architecture.html")), Is.EqualTo(expected));
        Assert.That(UrlDownloadHelper.ParseGitHubFile(new Uri("https://github.com/KovaForge/bot-config")), Is.Null);
        Assert.That(UrlDownloadHelper.ParseGitHubFile(new Uri("https://example.com/KovaForge/bot-config/blob/main/a.html")), Is.Null);
    }

    [Test]
    public void GitHubUris_PointAtRawFileAndContentsApi()
    {
        var file = new GitHubFileReference("KovaForge", "bot-config", "main", "docs/my notes.md");

        Assert.That(UrlDownloadHelper.GetGitHubRawUri(file).AbsoluteUri, Is.EqualTo("https://raw.githubusercontent.com/KovaForge/bot-config/main/docs/my%20notes.md"));
        Assert.That(UrlDownloadHelper.GetGitHubContentsApiUri(file).AbsoluteUri, Is.EqualTo("https://api.github.com/repos/KovaForge/bot-config/contents/docs/my%20notes.md?ref=main"));
    }

    [Test]
    public void TryParseDownloadUrl_AcceptsOnlyHttpAndHttps()
    {
        Assert.That(UrlDownloadHelper.TryParseDownloadUrl(" https://example.com/a.png ", out _), Is.True);
        Assert.That(UrlDownloadHelper.TryParseDownloadUrl("http://example.com/a.png", out _), Is.True);
        Assert.That(UrlDownloadHelper.TryParseDownloadUrl("file:///etc/passwd", out _), Is.False);
        Assert.That(UrlDownloadHelper.TryParseDownloadUrl("example.com/a.png", out _), Is.False);
        Assert.That(UrlDownloadHelper.TryParseDownloadUrl("", out _), Is.False);
    }

    [Test]
    public void GetFileName_IsSafeAndGainsExtensionFromContentType()
    {
        Assert.That(UrlDownloadHelper.GetFileName(new Uri("https://example.com/dir/report%20v2.html"), null, "text/html"), Is.EqualTo("report v2.html"));
        Assert.That(UrlDownloadHelper.GetFileName(new Uri("https://example.com/download"), "\"../../evil.txt\"", null), Is.EqualTo("evil.txt"));
        Assert.That(UrlDownloadHelper.GetFileName(new Uri("https://example.com/"), null, "image/png"), Is.EqualTo("download.png"));
        Assert.That(UrlDownloadHelper.GetFileName(new Uri("https://example.com/page"), null, "text/html; charset=utf-8"), Is.EqualTo("page"));
        Assert.That(UrlDownloadHelper.GetFileName(new Uri("https://example.com/page"), null, "text/html"), Is.EqualTo("page.html"));
        Assert.That(UrlDownloadHelper.GetFileName(new Uri("https://example.com/..hidden"), null, null), Is.EqualTo("hidden"));
    }

    [Test]
    public async Task DownloadAsync_SavesTheResponseUnderANewFolder()
    {
        using var server = LocalServer.Start(HttpStatusCode.OK, "<h1>hi</h1>", "text/html");
        string root = Path.Combine(Path.GetTempPath(), "xerahs-tests", Guid.NewGuid().ToString("N"));
        try
        {
            UrlDownloadResult result = await UrlDownloadHelper.DownloadAsync($"{server.Prefix}docs/page", root, gitHubToken: null);

            if (result is not UrlDownloaded { FilePath: var filePath })
            {
                Assert.Fail(result is UrlDownloadFailed failedResult ? failedResult.Message : "Unexpected download result.");
                return;
            }

            Assert.That(Path.GetFileName(filePath), Is.EqualTo("page.html"));
            Assert.That(Path.GetDirectoryName(Path.GetDirectoryName(filePath)), Is.EqualTo(root));
            Assert.That(await File.ReadAllTextAsync(filePath), Is.EqualTo("<h1>hi</h1>"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task DownloadAsync_ReportsHttpErrorsWithoutAskingForAToken()
    {
        using var server = LocalServer.Start(HttpStatusCode.NotFound, "no", "text/plain");
        string root = Path.Combine(Path.GetTempPath(), "xerahs-tests", Guid.NewGuid().ToString("N"));

        UrlDownloadResult result = await UrlDownloadHelper.DownloadAsync($"{server.Prefix}missing.png", root, gitHubToken: null);

        Assert.That(result is UrlDownloadFailed, Is.True);
        if (result is not UrlDownloadFailed failed)
        {
            return;
        }

        Assert.That(failed.GitHubTokenRequired, Is.False);
        Assert.That(failed.Message, Does.Contain("404"));
        Assert.That(Directory.Exists(root), Is.False);
    }

    private sealed class LocalServer : IDisposable
    {
        private readonly HttpListener _listener;

        private LocalServer(HttpListener listener, string prefix)
        {
            _listener = listener;
            Prefix = prefix;
        }

        public string Prefix { get; }

        public static LocalServer Start(HttpStatusCode status, string body, string contentType)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            string prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            _ = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await listener.GetContextAsync(); }
                    catch { return; }

                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.StatusCode = (int)status;
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            });
            return new LocalServer(listener, prefix);
        }

        public void Dispose() => _listener.Close();
    }
}
