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
using System.Text.Json;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;
using XerahS.OmaXerahs.Commands;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
[NonParallelizable]
public class UploadCommandTests
{
    [TestCase(UploaderCategory.Image)]
    [TestCase(UploaderCategory.File)]
    public void CreateUploadTaskSettings_PinsOnlyTheInspectedDestination(UploaderCategory category)
    {
        var source = new TaskSettings { AfterCaptureJob = AfterCaptureTasks.UploadImageToHost };
        var instance = new UploaderInstance { Category = category, InstanceId = "selected" };
        var settings = UploadCommand.CreateUploadTaskSettings(source, instance);
        Assert.That(settings.DestinationInstanceId, Is.EqualTo("selected"));
        Assert.That(settings.AllowCrossCategoryFallback, Is.False);
        Assert.That(settings.RoutedImageFileDestinationId, Is.EqualTo(category == UploaderCategory.File ? "selected" : null));
        Assert.That(settings.AfterCaptureJob, Is.EqualTo(AfterCaptureTasks.None));
        Assert.That(source.AfterCaptureJob, Is.EqualTo(AfterCaptureTasks.UploadImageToHost));
        Assert.That(settings.GeneralSettings, Is.Not.SameAs(source.GeneralSettings));
    }

    [Test]
    public void TryValidateFilePath_AcceptsMarkdownAndRejectsMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omaxerahs-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "# Report");
        try
        {
            Assert.That(UploadCommand.TryValidateFilePath(path, out string canonical, out _, out _), Is.True);
            Assert.That(canonical, Is.EqualTo(path));
        }
        finally
        {
            File.Delete(path);
        }

        Assert.That(UploadCommand.TryValidateFilePath(path, out _, out string code, out _), Is.False);
        Assert.That(code, Is.EqualTo(CliErrorCodes.InvalidPath));
        Assert.That(UploadCommand.TryValidateFilePath(null, out _, out code, out _), Is.False);
        Assert.That(code, Is.EqualTo(CliErrorCodes.Usage));
    }

    [TestCase("abcdef123456", "KovaForge Docs (File)")]
    [TestCase("ABCDEF", "KovaForge Docs (File)")]
    [TestCase("KOVAFORGE DOCS (FILE)", "KovaForge Docs (File)")]
    [TestCase("kovaforge", "KovaForge Docs (File)")]
    [TestCase("amazons3", "Amazon S3 (File)")]
    public void MatchDestination_FindsOneInstance(string query, string expected)
    {
        var matches = UploadHost.MatchDestination(Destinations(), query);
        Assert.That(matches.Select(i => i.DisplayName), Is.EqualTo(new[] { expected }));
    }

    [TestCase("nothing")]
    [TestCase("abc")]
    [TestCase("  ")]
    public void MatchDestination_ReturnsNothingForUnknownOrShortNames(string query)
    {
        Assert.That(UploadHost.MatchDestination(Destinations(), query), Is.Empty);
    }

    [Test]
    public void MatchDestination_ReportsEveryInstanceOfASharedProvider()
    {
        var instances = Destinations();
        instances.Add(new UploaderInstance { InstanceId = "fedcba654321", ProviderId = "kovaforge", DisplayName = "Team Docs" });
        Assert.That(UploadHost.MatchDestination(instances, "kovaforge"), Has.Count.EqualTo(2));
    }

    private static List<UploaderInstance> Destinations() =>
    [
        new UploaderInstance { InstanceId = "abcdef123456", ProviderId = "kovaforge", DisplayName = "KovaForge Docs (File)" },
        new UploaderInstance { InstanceId = "998877665544", ProviderId = "amazons3", DisplayName = "Amazon S3 (File)" }
    ];

    [Test]
    public void TryValidateImagePath_RejectsMissingPathAsUsage()
    {
        bool ok = UploadCommand.TryValidateImagePath(null, out _, out string errorCode, out string errorMessage);

        Assert.That(ok, Is.False);
        Assert.That(errorCode, Is.EqualTo(CliErrorCodes.Usage));
        Assert.That(errorMessage, Does.Contain("--"));
    }

    [Test]
    public void TryValidateImagePath_RejectsNonImageAsUnsupportedType()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "omaxerahs-test-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tempFile, "not an image");

        try
        {
            bool ok = UploadCommand.TryValidateImagePath(tempFile, out _, out string errorCode, out string errorMessage);

            Assert.That(ok, Is.False);
            Assert.That(errorCode, Is.EqualTo(CliErrorCodes.UnsupportedType));
            Assert.That(errorMessage, Does.Contain("image"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void TryValidateImagePath_DefersAMissingImageSoAWatchFolderMoveCanBeFollowed()
    {
        string missing = Path.Combine(Path.GetTempPath(), "screenshot-missing-" + Guid.NewGuid().ToString("N") + ".png");

        bool ok = UploadCommand.TryValidateImagePath(missing, out string canonical, out string errorCode, out string errorMessage);

        Assert.That(ok, Is.True, errorMessage);
        Assert.That(errorCode, Is.Empty);
        Assert.That(canonical, Is.EqualTo(Path.GetFullPath(missing)));
    }

    [Test]
    public void TryValidateImagePath_RejectsMissingNonImageAsInvalidPath()
    {
        string missing = Path.Combine(Path.GetTempPath(), "omaxerahs-missing-" + Guid.NewGuid().ToString("N") + ".txt");

        bool ok = UploadCommand.TryValidateImagePath(missing, out _, out string errorCode, out _);

        Assert.That(ok, Is.False);
        Assert.That(errorCode, Is.EqualTo(CliErrorCodes.InvalidPath));
    }

    [Test]
    public void TryValidateImagePath_AcceptsPng()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "screenshot-2026-09-02_14-22-05.png");
        File.WriteAllBytes(tempFile, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        try
        {
            bool ok = UploadCommand.TryValidateImagePath(tempFile, out string canonical, out string errorCode, out string errorMessage);

            Assert.That(ok, Is.True, errorMessage);
            Assert.That(errorCode, Is.Empty);
            Assert.That(canonical, Is.EqualTo(Path.GetFullPath(tempFile)));
            Assert.That(Path.GetFileName(canonical), Is.EqualTo("screenshot-2026-09-02_14-22-05.png"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private const string Pattern = "20261125T0745_ab12cd34ef";

    [TestCase("page.html", ".html", "page_" + Pattern + ".html")]
    [TestCase("my.report.v2.md", ".md", "my.report.v2_" + Pattern + ".md")]
    [TestCase("backup.tar.gz", ".tar.gz", "backup_" + Pattern + ".tar.gz")]
    [TestCase("README", "", "README_" + Pattern)]
    [TestCase(".env", ".env", ".env_" + Pattern)]
    public void WithNamePattern_AppendsBeforeTheExtension(string name, string extension, string expected)
    {
        Assert.That(UploadCommand.WithNamePattern(name, extension, Pattern), Is.EqualTo(expected));
    }

    [Test]
    public void WithNamePattern_ShortensTheStemNotTheSuffix()
    {
        string name = new string('a', 300) + ".md";
        string result = UploadCommand.WithNamePattern(name, ".md", Pattern);

        Assert.That(System.Text.Encoding.UTF8.GetByteCount(result), Is.EqualTo(255));
        Assert.That(result, Does.EndWith("_" + Pattern + ".md"));
        Assert.That(result, Does.StartWith("aaaa"));
    }

    [Test]
    public void BuildNamePatternSuffix_DefaultPatternHasTimestampAndRandomText()
    {
        var settings = new TaskSettings();
        settings.UploadSettings.NameFormatPattern = "%y%mo%dT%h%mi_%ra{10}";
        string first = UploadCommand.BuildNamePatternSuffix(settings);
        string second = UploadCommand.BuildNamePatternSuffix(settings);

        Assert.That(first, Does.Match(@"^\d{8}T\d{4}_[A-Za-z0-9]{10}$"));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void BuildNamePatternSuffix_FixedTextPatternIsUsedExactly()
    {
        var settings = new TaskSettings();
        settings.UploadSettings.NameFormatPattern = "v2";
        Assert.That(UploadCommand.BuildNamePatternSuffix(settings), Is.EqualTo("v2"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void BuildNamePatternSuffix_EmptyPatternFallsBackToRandomText(string pattern)
    {
        var settings = new TaskSettings();
        settings.UploadSettings.NameFormatPattern = pattern;
        Assert.That(UploadCommand.BuildNamePatternSuffix(settings), Does.Match("^[A-Za-z0-9]{10}$"));
    }

    [Test]
    public void KeepNameOption_ParsesPresentAndAbsent()
    {
        var command = UploadCommand.Create();

        Assert.That(command.Parse(["--destination", "kovaforge", "--keep-name", "--", "a.md"]).Errors, Is.Empty);
        Assert.That(command.Parse(["--destination", "kovaforge", "--", "a.md"]).Errors, Is.Empty);
        var option = command.Options.Single(o => o.Name == "--keep-name");
        Assert.That(command.Parse(["--keep-name", "--", "a.md"]).GetValue((System.CommandLine.Option<bool>)option), Is.True);
        Assert.That(command.Parse(["--", "a.md"]).GetValue((System.CommandLine.Option<bool>)option), Is.False);
    }

    [TestCase("backup.tar.gz", ".tar.gz")]
    [TestCase("a.b.tar.zst", ".tar.zst")]
    [TestCase("A.TAR.XZ", ".TAR.XZ")]
    [TestCase("notes.gz", ".gz-fallback")]
    [TestCase(".tar.gz", ".gz-fallback")]
    public void GetExtensionForRename_KeepsTarCompressionWhole(string name, string expected)
    {
        string result = UploadCommand.GetExtensionForRename("/tmp/" + name, p => Path.GetExtension(p) + "-fallback");
        Assert.That(result, Is.EqualTo(expected));
        if (!expected.EndsWith("-fallback"))
        {
            Assert.That(UploadCommand.WithNamePattern(name, result, "S"), Does.StartWith(name[..^result.Length] + "_S"));
        }
    }

    [Test]
    public void WithNamePattern_ExtensionAndSuffixOverBudgetNeverExceedLimit()
    {
        string suffix = new string('s', 300);
        string result = UploadCommand.WithNamePattern("page.html", ".html", suffix);

        Assert.That(System.Text.Encoding.UTF8.GetByteCount(result), Is.LessThanOrEqualTo(255));
        Assert.That(result, Does.StartWith("_s"));
        Assert.That(result, Does.EndWith(".html"));
    }

    private sealed class UploadRun
    {
        public string? ProcessedName;
        public int ProcessCalls;
        public string Stdout = "";
        public int ExitCode;
    }

    private static UploadRun RunUpload(string fileName, string[] extraArgs, string? pattern = "v2")
    {
        string dir = Path.Combine(Path.GetTempPath(), "omaxerahs-up-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, "# Report");

        var oldProcess = UploadCommand.ProcessUploadAsync;
        var oldBoot = UploadCommand.EnsureBootstrapped;
        var oldDest = UploadCommand.GetFileDestinations;
        var oldOut = Console.Out;
        bool oldJson = JsonStdout.Enabled, oldUrl = JsonStdout.UrlOnly;
        var run = new UploadRun();
        var writer = new StringWriter();
        try
        {
            UploadCommand.EnsureBootstrapped = () => Task.CompletedTask;
            UploadCommand.GetFileDestinations = () =>
                [new UploaderInstance { InstanceId = "abcdef123456", ProviderId = "kovaforge", DisplayName = "KovaForge Docs (File)", Category = UploaderCategory.File }];
            UploadCommand.ProcessUploadAsync = (info, _) =>
            {
                run.ProcessCalls++;
                run.ProcessedName = info.FileName;
                info.Result = new UploadResult { IsSuccess = true, URL = "https://example.test/" + info.FileName };
                return Task.CompletedTask;
            };
            Console.SetOut(writer);
            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            string? oldPattern = workflow.TaskSettings.UploadSettings.NameFormatPattern;
            workflow.TaskSettings.UploadSettings.NameFormatPattern = pattern ?? "";
            try
            {
                run.ExitCode = UploadCommand.Create().Parse([.. extraArgs, "--json", "--", path]).Invoke();
            }
            finally
            {
                workflow.TaskSettings.UploadSettings.NameFormatPattern = oldPattern;
            }
        }
        finally
        {
            Console.SetOut(oldOut);
            UploadCommand.ProcessUploadAsync = oldProcess;
            UploadCommand.EnsureBootstrapped = oldBoot;
            UploadCommand.GetFileDestinations = oldDest;
            JsonStdout.Enabled = oldJson;
            JsonStdout.UrlOnly = oldUrl;
            Assert.That(File.Exists(path), Is.True, "source file must stay in place");
            Directory.Delete(dir, true);
        }

        run.Stdout = writer.ToString();
        return run;
    }

    [Test]
    public void UploadToDestination_DefaultAppendsNamePattern()
    {
        var run = RunUpload("page.html", ["--destination", "kovaforge"]);

        Assert.That(run.ExitCode, Is.EqualTo(0), run.Stdout);
        Assert.That(run.ProcessedName, Is.EqualTo("page_v2.html"));
        using var json = JsonDocument.Parse(run.Stdout);
        Assert.That(json.RootElement.GetProperty("filename").GetString(), Is.EqualTo("page_v2.html"));
        Assert.That(json.RootElement.GetProperty("sourceFilename").GetString(), Is.EqualTo("page.html"));
        Assert.That(json.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public void UploadToDestination_KeepNameKeepsTheExactName()
    {
        var run = RunUpload("page.html", ["--destination", "kovaforge", "--keep-name"]);

        Assert.That(run.ExitCode, Is.EqualTo(0), run.Stdout);
        Assert.That(run.ProcessedName, Is.EqualTo("page.html"));
        using var json = JsonDocument.Parse(run.Stdout);
        Assert.That(json.RootElement.GetProperty("filename").GetString(), Is.EqualTo("page.html"));
        Assert.That(json.RootElement.GetProperty("sourceFilename").GetString(), Is.EqualTo("page.html"));
    }

    [Test]
    public void UploadToDestination_EmptyPatternFallsBackToRandomSuffix()
    {
        var run = RunUpload("page.html", ["--destination", "kovaforge"], pattern: "");

        Assert.That(run.ProcessedName, Does.Match(@"^page_[A-Za-z0-9]{10}\.html$"));
    }

    [Test]
    public void KeepNameWithoutDestination_IsAUsageErrorAndUploadsNothing()
    {
        var run = RunUpload("a.png", ["--keep-name"]);

        Assert.That(run.ExitCode, Is.EqualTo(1));
        Assert.That(run.ProcessCalls, Is.EqualTo(0));
        using var json = JsonDocument.Parse(run.Stdout);
        Assert.That(json.RootElement.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(CliErrorCodes.Usage));
    }
}
