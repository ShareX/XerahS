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

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(Path.GetFileName(result.FilePath), Is.EqualTo("page.html"));
            Assert.That(Path.GetDirectoryName(Path.GetDirectoryName(result.FilePath)), Is.EqualTo(root));
            Assert.That(await File.ReadAllTextAsync(result.FilePath!), Is.EqualTo("<h1>hi</h1>"));
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

        Assert.That(result.Success, Is.False);
        Assert.That(result.GitHubTokenRequired, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("404"));
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
