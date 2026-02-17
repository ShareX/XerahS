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

using Newtonsoft.Json;
using NUnit.Framework;
using ShareX.UploadersLib.FileUploaders;
using XerahS.Uploaders;
using XerahS.Uploaders.Configuration;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class UploadersConfigPolymorphicTests
{
    [Test]
    public void EnsurePolymorphicSettingsInitialized_CreatesPilotConfigurations()
    {
        UploadersConfig config = new UploadersConfig
        {
            ImgurDirectLink = false,
            DropboxUploadPath = "Uploads/%y",
            DropboxAutoCreateShareableLink = false,
            FTPSelectedFile = 2,
            AmazonS3Settings = new AmazonS3Settings
            {
                Bucket = "xerahs-tests",
                ObjectPrefix = "tests/%y/%mo"
            }
        };

        config.FTPAccountList.Add(new FTPAccount { Name = "Primary account" });

        CustomUploaderItem custom = CustomUploaderItem.Init();
        custom.Name = "Sample custom";
        config.CustomUploadersList.Add(custom);

        config.EnsurePolymorphicSettingsInitialized();

        Assert.That(config.ServiceSettings.ContainsKey(UploaderType.Imgur), Is.True);
        Assert.That(config.ServiceSettings.ContainsKey(UploaderType.Dropbox), Is.True);
        Assert.That(config.ServiceSettings.ContainsKey(UploaderType.FTP), Is.True);
        Assert.That(config.ServiceSettings.ContainsKey(UploaderType.AmazonS3), Is.True);

        var ftpConfig = config.GetServiceSettings<FtpConfig>(UploaderType.FTP);
        Assert.That(ftpConfig, Is.Not.Null);
        Assert.That(ftpConfig!.AccountList.Count, Is.EqualTo(1));
        Assert.That(ftpConfig.SelectedFile, Is.EqualTo(2));

        var s3Config = config.GetServiceSettings<S3Config>(UploaderType.AmazonS3);
        Assert.That(s3Config, Is.Not.Null);
        Assert.That(s3Config!.Settings.Bucket, Is.EqualTo("xerahs-tests"));

        Assert.That(config.CustomUploaders.Count, Is.EqualTo(1));
        Assert.That(config.CustomUploaders[0].Item.Name, Is.EqualTo("Sample custom"));
    }

    [Test]
    public void PolymorphicSettings_SerializeAndDeserialize_WithConcreteTypes()
    {
        UploadersConfig config = new UploadersConfig();

        config.SetServiceSettings(UploaderType.Dropbox, new DropboxConfig
        {
            Name = "Dropbox profile",
            UploadPath = "ShareX/uploads",
            AutoCreateShareableLink = false,
            UseDirectLink = true
        });

        CustomUploaderItem custom = CustomUploaderItem.Init();
        custom.Name = "Polymorphic custom";

        config.CustomUploaders = new List<CustomUploaderConfig>
        {
            new CustomUploaderConfig
            {
                Name = "Custom profile",
                Item = custom
            }
        };

        config.SyncLegacySettingsFromPolymorphic();
        config.SyncPolymorphicSettingsFromLegacy();

        JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        string json = JsonConvert.SerializeObject(config, Formatting.Indented, serializerSettings);
        UploadersConfig? loaded = JsonConvert.DeserializeObject<UploadersConfig>(json, serializerSettings);

        Assert.That(loaded, Is.Not.Null);

        loaded!.EnsurePolymorphicSettingsInitialized();

        Assert.That(loaded.ServiceSettings[UploaderType.Dropbox], Is.TypeOf<DropboxConfig>());

        var loadedDropbox = loaded.GetServiceSettings<DropboxConfig>(UploaderType.Dropbox);
        Assert.That(loadedDropbox, Is.Not.Null);
        Assert.That(loadedDropbox!.UploadPath, Is.EqualTo("ShareX/uploads"));
        Assert.That(loadedDropbox.UseDirectLink, Is.True);

        Assert.That(loaded.CustomUploaders.Count, Is.EqualTo(1));
        Assert.That(loaded.CustomUploaders[0].Item.Name, Is.EqualTo("Polymorphic custom"));
    }
}
