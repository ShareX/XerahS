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
using XerahS.Uploaders;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.CustomUploader;

[TestFixture]
public class CustomUploaderRepositoryTests
{
    private string _sampleUploadersPath = null!;

    [SetUp]
    public void Setup()
    {
        _sampleUploadersPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "CustomUploader", "SampleUploaders");
    }

    [Test]
    public void DiscoverUploaders_FindsSampleFiles()
    {
        // Act
        var uploaders = CustomUploaderRepository.DiscoverUploaders(_sampleUploadersPath);

        // Assert
        Assert.That(uploaders, Is.Not.Empty, "Should find sample uploaders");
        Assert.That(uploaders.Count, Is.GreaterThanOrEqualTo(3), "Should find at least 3 sample uploaders");
    }

    [Test]
    public void LoadFromFile_ImgurAnonymous_ParsesCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_sampleUploadersPath, "Imgur-Anonymous.sxcu");

        // Act
        var loaded = CustomUploaderRepository.LoadFromFile(filePath);

        // Assert
        Assert.That(loaded.IsValid, Is.True, $"Should load successfully: {loaded.LoadError}");
        Assert.That(loaded.Item.Name, Is.EqualTo("Imgur (Anonymous)"));
        Assert.That(loaded.Item.DestinationType.HasFlag(CustomUploaderDestinationType.ImageUploader), Is.True);
        Assert.That(loaded.Item.RequestMethod, Is.EqualTo(XerahS.Uploaders.HttpMethod.POST));
        Assert.That(loaded.Item.RequestURL, Is.EqualTo("https://api.imgur.com/3/image"));
        Assert.That(loaded.Item.Body, Is.EqualTo(CustomUploaderBody.MultipartFormData));
        Assert.That(loaded.Item.FileFormName, Is.EqualTo("image"));
        Assert.That(loaded.Item.Headers, Is.Not.Null);
        Assert.That(loaded.Item.Headers!.ContainsKey("Authorization"), Is.True);
        Assert.That(loaded.Item.URL, Is.EqualTo("{json:data.link}"));
    }

    [Test]
    public void LoadFromFile_Pastebin_ParsesCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_sampleUploadersPath, "Pastebin.sxcu");

        // Act
        var loaded = CustomUploaderRepository.LoadFromFile(filePath);

        // Assert
        Assert.That(loaded.IsValid, Is.True, $"Should load successfully: {loaded.LoadError}");
        Assert.That(loaded.Item.Name, Is.EqualTo("Pastebin"));
        Assert.That(loaded.Item.DestinationType.HasFlag(CustomUploaderDestinationType.TextUploader), Is.True);
        Assert.That(loaded.Item.Body, Is.EqualTo(CustomUploaderBody.FormURLEncoded));
        Assert.That(loaded.Item.Arguments, Is.Not.Null);
        Assert.That(loaded.Item.Arguments!.ContainsKey("api_paste_code"), Is.True);
        Assert.That(loaded.Item.Arguments["api_paste_code"], Is.EqualTo("{input}"));
        Assert.That(loaded.Item.URL, Is.EqualTo("{response}"));
    }

    [Test]
    public void LoadFromFile_TinyURL_ParsesCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_sampleUploadersPath, "TinyURL.sxcu");

        // Act
        var loaded = CustomUploaderRepository.LoadFromFile(filePath);

        // Assert
        Assert.That(loaded.IsValid, Is.True, $"Should load successfully: {loaded.LoadError}");
        Assert.That(loaded.Item.Name, Is.EqualTo("TinyURL"));
        Assert.That(loaded.Item.DestinationType.HasFlag(CustomUploaderDestinationType.URLShortener), Is.True);
        Assert.That(loaded.Item.Body, Is.EqualTo(CustomUploaderBody.JSON));
        Assert.That(loaded.Item.Data, Does.Contain("{input}"));
        Assert.That(loaded.Item.URL, Is.EqualTo("{json:data.tiny_url}"));
    }

    [Test]
    public void CustomUploaderProvider_CreatesValidProvider()
    {
        // Arrange
        var filePath = Path.Combine(_sampleUploadersPath, "Imgur-Anonymous.sxcu");
        var loaded = CustomUploaderRepository.LoadFromFile(filePath);

        // Act
        var provider = new CustomUploaderProvider(loaded);

        // Assert
        Assert.That(provider.ProviderId, Does.StartWith("custom_"));
        Assert.That(provider.Name, Is.EqualTo("Imgur (Anonymous)"));
        Assert.That(provider.SupportedCategories, Does.Contain(UploaderCategory.Image));
        Assert.That(provider.ConfigModelType, Is.EqualTo(typeof(CustomUploaderItem)));
    }

    [Test]
    public void CustomUploaderProvider_ConvertDestinationType_MapsCorrectly()
    {
        // Test various destination type combinations
        var imageOnly = CustomUploaderProvider.ConvertDestinationType(CustomUploaderDestinationType.ImageUploader);
        Assert.That(imageOnly, Does.Contain(UploaderCategory.Image));
        Assert.That(imageOnly.Length, Is.EqualTo(1));

        var multiple = CustomUploaderProvider.ConvertDestinationType(
            CustomUploaderDestinationType.ImageUploader | CustomUploaderDestinationType.FileUploader);
        Assert.That(multiple, Does.Contain(UploaderCategory.Image));
        Assert.That(multiple, Does.Contain(UploaderCategory.File));
        Assert.That(multiple.Length, Is.EqualTo(2));

        var urlShortener = CustomUploaderProvider.ConvertDestinationType(CustomUploaderDestinationType.URLShortener);
        Assert.That(urlShortener, Does.Contain(UploaderCategory.UrlShortener));
    }

    [Test]
    public void ValidateItem_RequiresRequestURL()
    {
        // Arrange
        var item = CustomUploaderItem.Init();
        item.Name = "Test";
        item.RequestURL = ""; // Empty URL

        // Act
        var error = CustomUploaderRepository.ValidateItem(item);

        // Assert
        Assert.That(error, Is.Not.Null);
        Assert.That(error, Does.Contain("RequestURL"));
    }

    [Test]
    public void ValidateItem_AcceptsURLWithSyntaxPlaceholders()
    {
        // Arrange
        var item = CustomUploaderItem.Init();
        item.Name = "Test";
        item.RequestURL = "https://example.com/{input}"; // URL with placeholder
        item.DestinationType = CustomUploaderDestinationType.FileUploader;

        // Act
        var error = CustomUploaderRepository.ValidateItem(item);

        // Assert
        Assert.That(error, Is.Null, "Should accept URLs with syntax placeholders");
    }

    [Test]
    public void LoadFromJson_InvalidJson_ReturnsError()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var loaded = CustomUploaderRepository.LoadFromJson(invalidJson, "test.sxcu");

        // Assert
        Assert.That(loaded.IsValid, Is.False);
        Assert.That(loaded.LoadError, Does.Contain("Invalid JSON"));
    }
}
