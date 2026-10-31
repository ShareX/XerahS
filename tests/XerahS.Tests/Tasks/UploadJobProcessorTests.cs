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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks.Processors;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Tasks;

[TestFixture]
[NonParallelizable]
public class UploadJobProcessorTests
{
    private const string FallbackProviderId = "xerahs-tests-upload-fallback-provider";

    [SetUp]
    public void SetUp()
    {
        ClearInstances();
        RegisterFallbackProvider();
    }

    [TearDown]
    public void TearDown()
    {
        ClearInstances();
    }

    [Test]
    public void ResolveRequestedInstance_ReturnsNullWhenConfiguredInstanceIsUnavailable()
    {
        var unavailableConfigured = CreateInstance("Unavailable Configured", isAvailable: false);
        InstanceManager.Instance.AddInstance(unavailableConfigured);

        var result = UploadJobProcessor.ResolveRequestedInstance(
            InstanceManager.Instance,
            unavailableConfigured.InstanceId,
            UploaderCategory.Image);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveDefaultInstance_ReturnsNullWhenDefaultInstanceIsUnavailable()
    {
        var unavailableDefault = CreateInstance("Unavailable Default", isAvailable: false);
        InstanceManager.Instance.AddInstance(unavailableDefault);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, unavailableDefault.InstanceId);

        var result = UploadJobProcessor.ResolveDefaultInstance(InstanceManager.Instance, UploaderCategory.Image);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveRequestedInstance_PreservesAvailableConfiguredInstance()
    {
        var availableInstance = CreateInstance("Available Instance");
        InstanceManager.Instance.AddInstance(availableInstance);

        var result = UploadJobProcessor.ResolveRequestedInstance(
            InstanceManager.Instance,
            availableInstance.InstanceId,
            UploaderCategory.Image);

        Assert.That(result, Is.SameAs(availableInstance));
    }

    [Test]
    public void ApplyResolvedUploaderHost_PrefersActualSuccessfulUploaderOverConfiguredDefault()
    {
        var configuredDefault = CreateInstance("Configured Default");
        var fallbackInstance = CreateInstance("Actual Fallback");
        InstanceManager.Instance.AddInstance(configuredDefault);
        InstanceManager.Instance.AddInstance(fallbackInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, configuredDefault.InstanceId);

        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image
        };

        UploadJobProcessor.ApplyResolvedUploaderHost(info, fallbackInstance, new UploadResult
        {
            URL = "https://example.test/image.png"
        });

        Assert.That(info.UploaderHost, Is.EqualTo("Actual Fallback"));
    }

    [Test]
    public void ApplyResolvedUploaderHost_DoesNotReplaceConfiguredHostAfterFailedUpload()
    {
        var configuredDefault = CreateInstance("Configured Default");
        var failedFallback = CreateInstance("Failed Fallback");
        InstanceManager.Instance.AddInstance(configuredDefault);
        InstanceManager.Instance.AddInstance(failedFallback);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, configuredDefault.InstanceId);

        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image
        };

        UploadJobProcessor.ApplyResolvedUploaderHost(info, failedFallback, new UploadResult
        {
            IsSuccess = false,
            Response = "boom"
        });

        Assert.That(info.UploaderHost, Is.EqualTo("Configured Default"));
    }

    [Test]
    public void CreateHistoryItem_PreservesAllUploaderResultUrls()
    {
        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image,
            Result = new UploadResult
            {
                URL = "https://example.test/image.png",
                ThumbnailURL = "https://example.test/thumb.png",
                DeletionURL = "https://example.test/delete/image",
                ShortenedURL = "https://ex.test/i"
            }
        };
        info.FilePath = "/tmp/image.png";

        var historyItem = UploadJobProcessor.CreateHistoryItem(info, info.Result.URL!);

        Assert.Multiple(() =>
        {
            Assert.That(historyItem.URL, Is.EqualTo("https://example.test/image.png"));
            Assert.That(historyItem.ThumbnailURL, Is.EqualTo("https://example.test/thumb.png"));
            Assert.That(historyItem.DeletionURL, Is.EqualTo("https://example.test/delete/image"));
            Assert.That(historyItem.ShortenedURL, Is.EqualTo("https://ex.test/i"));
        });
    }

    [Test]
    public void CreateHistoryItem_PreservesUploaderResultMetadataAsTags()
    {
        var result = new UploadResult
        {
            URL = "https://pastebin.com/abc123",
            DeletionURL = "https://pastebin.com/api/api_post.php"
        };
        result.Metadata["Deletion.Provider"] = "Pastebin";
        result.Metadata["Deletion.PasteKey"] = "abc123";
        result.Metadata["Deletion.ApiOption"] = "delete";

        var info = new TaskInfo
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Text,
            Result = result
        };

        var historyItem = UploadJobProcessor.CreateHistoryItem(info, result.URL!);

        Assert.Multiple(() =>
        {
            Assert.That(historyItem.DeletionURL, Is.EqualTo("https://pastebin.com/api/api_post.php"));
            Assert.That(historyItem.Tags["UploadResult.Deletion.Provider"], Is.EqualTo("Pastebin"));
            Assert.That(historyItem.Tags["UploadResult.Deletion.PasteKey"], Is.EqualTo("abc123"));
            Assert.That(historyItem.Tags["UploadResult.Deletion.ApiOption"], Is.EqualTo("delete"));
        });
    }

    [Test]
    public async Task ProcessAsync_FileUploadFallsBackWhenDefaultUploaderFails()
    {
        var defaultInstance = CreateFileInstance("Default Fails", "fail:primary unavailable");
        var fallbackInstance = CreateFileInstance("Fallback Succeeds", "success:https://example.test/fallback.bin");
        InstanceManager.Instance.AddInstance(defaultInstance);
        InstanceManager.Instance.AddInstance(fallbackInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, defaultInstance.InstanceId);

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "fallback payload");

        try
        {
            var info = new TaskInfo(new TaskSettings
            {
                Job = WorkflowType.PrintScreen,
                AfterUploadJob = AfterUploadTasks.None
            })
            {
                Job = TaskJob.FileUpload,
                DataType = EDataType.File
            };
            info.FilePath = tempFile;

            var processed = await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(info.Result.URL, Is.EqualTo("https://example.test/fallback.bin"));
                Assert.That(info.Metadata.UploadURL, Is.EqualTo("https://example.test/fallback.bin"));
                Assert.That(info.UploaderHost, Is.EqualTo("Fallback Succeeds"));
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestCase("selected", "capture.png", true)]
    [TestCase("different", "capture.png", false)]
    [TestCase("selected", "capture.jpg", false)]
    public void ResolveRequestedInstance_AllowsOnlyExplicitlyRoutedFile(string allowedId, string path, bool expected)
    {
        var instance = CreateInstance("Routed File", category: UploaderCategory.File);
        instance.InstanceId = "selected";
        instance.FileTypeRouting = new FileTypeScope { FileExtensions = [".PNG"] };
        InstanceManager.Instance.AddInstance(instance);
        var result = UploadJobProcessor.ResolveRequestedInstance(InstanceManager.Instance, instance.InstanceId,
            UploaderCategory.Image, false, allowedId, path);
        Assert.That(result != null, Is.EqualTo(expected));
    }

    [Test]
    public void ResolveRequestedInstance_ReturnsNullWhenCategoryMismatchesAndFallbackDisabled()
    {
        var fileInstance = CreateInstance("File Dest", category: UploaderCategory.File);
        InstanceManager.Instance.AddInstance(fileInstance);

        var result = UploadJobProcessor.ResolveRequestedInstance(
            InstanceManager.Instance,
            fileInstance.InstanceId,
            UploaderCategory.Image,
            allowCrossCategoryFallback: false);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveRequestedInstance_ReturnsInstanceWhenCategoryMismatchesAndFallbackEnabled()
    {
        var fileInstance = CreateInstance("File Dest", category: UploaderCategory.File);
        InstanceManager.Instance.AddInstance(fileInstance);

        var result = UploadJobProcessor.ResolveRequestedInstance(
            InstanceManager.Instance,
            fileInstance.InstanceId,
            UploaderCategory.Image,
            allowCrossCategoryFallback: true);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.InstanceId, Is.EqualTo(fileInstance.InstanceId));
        Assert.That(result.Category, Is.EqualTo(UploaderCategory.File));
    }

    [Test]
    public async Task ProcessAsync_ImageUpload_DoesNotFallBackToFileWhenFallbackDisabledAndImageMissing()
    {
        var fileInstance = CreateFileInstance("File Succeeds", "success:https://example.test/file-fallback.bin");
        InstanceManager.Instance.AddInstance(fileInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, fileInstance.InstanceId);

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllTextAsync(tempFile, "image payload");

        try
        {
            var info = CreateImageUploadInfo(allowCrossCategoryFallback: false, tempFile);

            var processed = await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(info.Result.IsSuccess, Is.False);
                Assert.That(info.Result.URL, Is.Null.Or.Empty);
                Assert.That(info.Metadata.UploadURL, Is.Null.Or.Empty);
                Assert.That(info.Result.Response, Does.Contain("No uploader instance configured").And.Contain("Image"));
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ProcessAsync_ImageUpload_DoesNotFallBackToFileWhenFallbackDisabledAndImageFails()
    {
        const string fileSuccessUrl = "https://example.test/file-fallback.bin";
        var imageInstance = CreateImageInstance("Image Fails", "fail:image provider unavailable");
        var fileInstance = CreateFileInstance("File Succeeds", $"success:{fileSuccessUrl}");
        InstanceManager.Instance.AddInstance(imageInstance);
        InstanceManager.Instance.AddInstance(fileInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, imageInstance.InstanceId);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, fileInstance.InstanceId);

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllTextAsync(tempFile, "image payload");

        try
        {
            var info = CreateImageUploadInfo(allowCrossCategoryFallback: false, tempFile);

            var processed = await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(info.Result.IsSuccess, Is.False);
                Assert.That(info.Result.URL, Is.Not.EqualTo(fileSuccessUrl));
                Assert.That(info.Result.URL, Is.Null.Or.Empty);
                Assert.That(info.Metadata.UploadURL, Is.Null.Or.Empty);
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ProcessAsync_ImageUpload_FallsBackToFileWhenFallbackEnabledAndImageMissing()
    {
        const string fileSuccessUrl = "https://example.test/file-fallback.bin";
        var fileInstance = CreateFileInstance("File Succeeds", $"success:{fileSuccessUrl}");
        InstanceManager.Instance.AddInstance(fileInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, fileInstance.InstanceId);

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllTextAsync(tempFile, "image payload");

        try
        {
            var info = CreateImageUploadInfo(allowCrossCategoryFallback: true, tempFile);

            var processed = await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(info.Result.URL, Is.EqualTo(fileSuccessUrl));
                Assert.That(info.Metadata.UploadURL, Is.EqualTo(fileSuccessUrl));
                Assert.That(info.UploaderHost, Is.EqualTo("File Succeeds"));
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ProcessAsync_ImageUpload_StillFallsBackWithinImageWhenCrossCategoryDisabled()
    {
        const string imageSuccessUrl = "https://example.test/image-fallback.png";
        var failingImage = CreateImageInstance("Image Fails", "fail:primary unavailable");
        var fallbackImage = CreateImageInstance("Image Succeeds", $"success:{imageSuccessUrl}");
        var fileInstance = CreateFileInstance("File Succeeds", "success:https://example.test/file-fallback.bin");
        InstanceManager.Instance.AddInstance(failingImage);
        InstanceManager.Instance.AddInstance(fallbackImage);
        InstanceManager.Instance.AddInstance(fileInstance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, failingImage.InstanceId);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, fileInstance.InstanceId);

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllTextAsync(tempFile, "image payload");

        try
        {
            var info = CreateImageUploadInfo(allowCrossCategoryFallback: false, tempFile);

            var processed = await new UploadJobProcessor().ProcessAsync(info, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(info.Result.URL, Is.EqualTo(imageSuccessUrl));
                Assert.That(info.Metadata.UploadURL, Is.EqualTo(imageSuccessUrl));
                Assert.That(info.UploaderHost, Is.EqualTo("Image Succeeds"));
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static TaskInfo CreateImageUploadInfo(bool allowCrossCategoryFallback, string filePath)
    {
        var info = new TaskInfo(new TaskSettings
        {
            Job = WorkflowType.PrintScreen,
            AfterUploadJob = AfterUploadTasks.None,
            AllowCrossCategoryFallback = allowCrossCategoryFallback
        })
        {
            Job = TaskJob.FileUpload,
            DataType = EDataType.Image
        };
        info.FilePath = filePath;
        return info;
    }

    private static UploaderInstance CreateInstance(string name, bool isAvailable = true, UploaderCategory category = UploaderCategory.Image)
    {
        return new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = category,
            DisplayName = name,
            IsAvailable = isAvailable
        };
    }

    private static UploaderInstance CreateFileInstance(string name, string settingsJson)
    {
        return new UploaderInstance
        {
            ProviderId = FallbackProviderId,
            Category = UploaderCategory.File,
            DisplayName = name,
            SettingsJson = settingsJson,
            IsAvailable = true
        };
    }

    private static UploaderInstance CreateImageInstance(string name, string settingsJson)
    {
        return new UploaderInstance
        {
            ProviderId = FallbackProviderId,
            Category = UploaderCategory.Image,
            DisplayName = name,
            SettingsJson = settingsJson,
            IsAvailable = true
        };
    }

    private static void RegisterFallbackProvider()
    {
        if (ProviderCatalog.GetProvider(FallbackProviderId) == null)
        {
            ProviderCatalog.RegisterProvider(new FallbackTestProvider());
        }
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }

    private sealed class FallbackTestProvider : UploaderProviderBase
    {
        public override string ProviderId => FallbackProviderId;
        public override string Name => "Fallback Test Provider";
        public override string Description => "Test-only provider for upload fallback behavior.";
        public override Version Version { get; } = new(1, 0, 0);
        public override UploaderCategory[] SupportedCategories { get; } = [UploaderCategory.File, UploaderCategory.Image];
        public override Type ConfigModelType => typeof(object);

        public override Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() => new()
        {
            [UploaderCategory.File] = ["*"],
            [UploaderCategory.Image] = ["*"]
        };

        public override Uploader CreateInstance(string settingsJson) => new FallbackTestUploader(settingsJson);
    }

    private sealed class FallbackTestUploader(string outcome) : FileUploader
    {
        public override UploadResult Upload(Stream stream, string fileName)
        {
            const string successPrefix = "success:";
            if (outcome.StartsWith(successPrefix, StringComparison.Ordinal))
            {
                return new UploadResult
                {
                    IsSuccess = true,
                    URL = outcome[successPrefix.Length..]
                };
            }

            return new UploadResult
            {
                IsSuccess = false,
                Response = outcome.StartsWith("fail:", StringComparison.Ordinal)
                    ? outcome["fail:".Length..]
                    : "Upload failed"
            };
        }
    }
}
