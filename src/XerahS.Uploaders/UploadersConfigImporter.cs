#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using XerahS.Common;

namespace XerahS.Uploaders
{
    /// <summary>
    /// Handles importing UploadersConfig.json from ShareX (WinForms) into ShareX Avalonia.
    /// </summary>
    public static class UploadersConfigImporter
    {
        private const string LogPrefix = "[UploadersConfigImporter]";

        /// <summary>
        /// Default ShareX configuration directory.
        /// </summary>
        private static string DefaultShareXConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShareX");

        /// <summary>
        /// Locate ShareX UploadersConfig.json file.
        /// </summary>
        public static string? FindShareXUploadersConfig()
        {
            string defaultPath = Path.Combine(DefaultShareXConfigPath, "UploadersConfig.json");
            if (File.Exists(defaultPath))
            {
                DebugHelper.WriteLine($"{LogPrefix} Found ShareX UploadersConfig at: {defaultPath}");
                return defaultPath;
            }

            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadersConfig.json");
            if (File.Exists(portablePath))
            {
                DebugHelper.WriteLine($"{LogPrefix} Found ShareX UploadersConfig (portable) at: {portablePath}");
                return portablePath;
            }

            DebugHelper.WriteLine($"{LogPrefix} ShareX UploadersConfig.json not found in default locations");
            return null;
        }

        /// <summary>
        /// Import UploadersConfig from ShareX file.
        /// </summary>
        /// <param name="sourceFilePath">Path to ShareX UploadersConfig.json.</param>
        /// <param name="targetConfig">Target UploadersConfig to populate.</param>
        /// <returns>Number of settings imported.</returns>
        public static ImportResult ImportFromFile(string sourceFilePath, UploadersConfig targetConfig)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("UploadersConfig.json not found", sourceFilePath);
            }

            try
            {
                string json = File.ReadAllText(sourceFilePath);
                var sourceConfig = JsonConvert.DeserializeObject<UploadersConfig>(json);

                if (sourceConfig == null)
                {
                    throw new InvalidDataException("Failed to deserialize UploadersConfig.json");
                }

                var result = new ImportResult();
                ImportImageUploaders(sourceConfig, targetConfig, result);
                ImportTextUploaders(sourceConfig, targetConfig, result);
                ImportFileUploaders(sourceConfig, targetConfig, result);
                ImportUrlShorteners(sourceConfig, targetConfig, result);

                DebugHelper.WriteLine($"{LogPrefix} Import complete: {result.TotalImported} settings imported");
                return result;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"{LogPrefix} Import failed: {ex.Message}");
                throw;
            }
        }

        private static void ImportImageUploaders(UploadersConfig source, UploadersConfig target, ImportResult result)
        {
            if (source.ImgurOAuth2Info != null)
            {
                target.ImgurAccountType = source.ImgurAccountType;
                target.ImgurDirectLink = source.ImgurDirectLink;
                target.ImgurThumbnailType = source.ImgurThumbnailType;
                target.ImgurUseGIFV = source.ImgurUseGIFV;
                target.ImgurOAuth2Info = source.ImgurOAuth2Info;
                target.ImgurUploadSelectedAlbum = source.ImgurUploadSelectedAlbum;
                target.ImgurSelectedAlbum = source.ImgurSelectedAlbum;
                target.ImgurAlbumList = source.ImgurAlbumList;
                result.AddImported("Imgur");
            }

            if (source.ImageShackSettings != null)
            {
                target.ImageShackSettings = source.ImageShackSettings;
                result.AddImported("ImageShack");
            }

            if (source.FlickrOAuthInfo != null)
            {
                target.FlickrOAuthInfo = source.FlickrOAuthInfo;
                target.FlickrSettings = source.FlickrSettings;
                result.AddImported("Flickr");
            }

            if (source.PhotobucketOAuthInfo != null)
            {
                target.PhotobucketOAuthInfo = source.PhotobucketOAuthInfo;
                target.PhotobucketAccountInfo = source.PhotobucketAccountInfo;
                result.AddImported("Photobucket");
            }

            if (!string.IsNullOrEmpty(source.CheveretoUploader?.UploadURL))
            {
                target.CheveretoUploader = source.CheveretoUploader;
                target.CheveretoDirectURL = source.CheveretoDirectURL;
                result.AddImported("Chevereto");
            }

            if (!string.IsNullOrEmpty(source.VgymeUserKey))
            {
                target.VgymeUserKey = source.VgymeUserKey;
                result.AddImported("vgy.me");
            }
        }

        private static void ImportTextUploaders(UploadersConfig source, UploadersConfig target, ImportResult result)
        {
            if (source.PastebinSettings != null && !string.IsNullOrEmpty(source.PastebinSettings.UserKey))
            {
                target.PastebinSettings = source.PastebinSettings;
                result.AddImported("Pastebin");
            }

            if (!string.IsNullOrEmpty(source.Paste_eeUserKey))
            {
                target.Paste_eeUserKey = source.Paste_eeUserKey;
                target.Paste_eeEncryptPaste = source.Paste_eeEncryptPaste;
                result.AddImported("Paste.ee");
            }

            if (source.GistOAuth2Info != null)
            {
                target.GistOAuth2Info = source.GistOAuth2Info;
                target.GistPublishPublic = source.GistPublishPublic;
                target.GistRawURL = source.GistRawURL;
                target.GistCustomURL = source.GistCustomURL;
                result.AddImported("Gist");
            }

            if (!string.IsNullOrEmpty(source.UpasteUserKey))
            {
                target.UpasteUserKey = source.UpasteUserKey;
                target.UpasteIsPublic = source.UpasteIsPublic;
                result.AddImported("uPaste");
            }

            if (!string.IsNullOrEmpty(source.HastebinCustomDomain))
            {
                target.HastebinCustomDomain = source.HastebinCustomDomain;
                target.HastebinSyntaxHighlighting = source.HastebinSyntaxHighlighting;
                target.HastebinUseFileExtension = source.HastebinUseFileExtension;
                result.AddImported("Hastebin");
            }

            if (!string.IsNullOrEmpty(source.OneTimeSecretAPIKey))
            {
                target.OneTimeSecretAPIUsername = source.OneTimeSecretAPIUsername;
                target.OneTimeSecretAPIKey = source.OneTimeSecretAPIKey;
                result.AddImported("OneTimeSecret");
            }
        }

        private static void ImportFileUploaders(UploadersConfig source, UploadersConfig target, ImportResult result)
        {
            if (source.DropboxOAuth2Info != null)
            {
                target.DropboxOAuth2Info = source.DropboxOAuth2Info;
                target.DropboxUploadPath = source.DropboxUploadPath;
                target.DropboxAutoCreateShareableLink = source.DropboxAutoCreateShareableLink;
                target.DropboxUseDirectLink = source.DropboxUseDirectLink;
                result.AddImported("Dropbox");
            }

            if (source.FTPAccountList != null && source.FTPAccountList.Count > 0)
            {
                target.FTPAccountList = source.FTPAccountList;
                target.FTPSelectedImage = source.FTPSelectedImage;
                target.FTPSelectedText = source.FTPSelectedText;
                target.FTPSelectedFile = source.FTPSelectedFile;
                result.AddImported("FTP");
            }

            if (source.OneDriveV2OAuth2Info != null)
            {
                target.OneDriveV2OAuth2Info = source.OneDriveV2OAuth2Info;
                target.OneDriveV2SelectedFolder = source.OneDriveV2SelectedFolder;
                target.OneDriveAutoCreateShareableLink = source.OneDriveAutoCreateShareableLink;
                target.OneDriveUseDirectLink = source.OneDriveUseDirectLink;
                result.AddImported("OneDrive");
            }

            if (source.GoogleDriveOAuth2Info != null)
            {
                target.GoogleDriveOAuth2Info = source.GoogleDriveOAuth2Info;
                target.GoogleDriveUserInfo = source.GoogleDriveUserInfo;
                target.GoogleDriveIsPublic = source.GoogleDriveIsPublic;
                target.GoogleDriveDirectLink = source.GoogleDriveDirectLink;
                target.GoogleDriveUseFolder = source.GoogleDriveUseFolder;
                target.GoogleDriveFolderID = source.GoogleDriveFolderID;
                target.GoogleDriveSelectedDrive = source.GoogleDriveSelectedDrive;
                result.AddImported("Google Drive");
            }

            if (source.AmazonS3Settings != null && !string.IsNullOrEmpty(source.AmazonS3Settings.AccessKeyID))
            {
                target.AmazonS3Settings = source.AmazonS3Settings;
                result.AddImported("Amazon S3");
            }

            if (!string.IsNullOrEmpty(source.AzureStorageAccountName))
            {
                target.AzureStorageAccountName = source.AzureStorageAccountName;
                target.AzureStorageAccountAccessKey = source.AzureStorageAccountAccessKey;
                target.AzureStorageContainer = source.AzureStorageContainer;
                target.AzureStorageEnvironment = source.AzureStorageEnvironment;
                target.AzureStorageCustomDomain = source.AzureStorageCustomDomain;
                target.AzureStorageUploadPath = source.AzureStorageUploadPath;
                target.AzureStorageCacheControl = source.AzureStorageCacheControl;
                result.AddImported("Azure Storage");
            }

            if (!string.IsNullOrEmpty(source.B2ApplicationKeyId))
            {
                target.B2ApplicationKeyId = source.B2ApplicationKeyId;
                target.B2ApplicationKey = source.B2ApplicationKey;
                target.B2BucketName = source.B2BucketName;
                target.B2UploadPath = source.B2UploadPath;
                target.B2UseCustomUrl = source.B2UseCustomUrl;
                target.B2CustomUrl = source.B2CustomUrl;
                result.AddImported("Backblaze B2");
            }

            if (source.CustomUploadersList != null && source.CustomUploadersList.Count > 0)
            {
                target.CustomUploadersList = source.CustomUploadersList;
                target.CustomImageUploaderSelected = source.CustomImageUploaderSelected;
                target.CustomTextUploaderSelected = source.CustomTextUploaderSelected;
                target.CustomFileUploaderSelected = source.CustomFileUploaderSelected;
                target.CustomURLShortenerSelected = source.CustomURLShortenerSelected;
                target.CustomURLSharingServiceSelected = source.CustomURLSharingServiceSelected;
                result.AddImported($"Custom Uploaders ({source.CustomUploadersList.Count})");
            }
        }

        private static void ImportUrlShorteners(UploadersConfig source, UploadersConfig target, ImportResult result)
        {
            if (source.BitlyOAuth2Info != null)
            {
                target.BitlyOAuth2Info = source.BitlyOAuth2Info;
                target.BitlyDomain = source.BitlyDomain;
                result.AddImported("bit.ly");
            }

            if (!string.IsNullOrEmpty(source.YourlsAPIURL))
            {
                target.YourlsAPIURL = source.YourlsAPIURL;
                target.YourlsSignature = source.YourlsSignature;
                target.YourlsUsername = source.YourlsUsername;
                target.YourlsPassword = source.YourlsPassword;
                result.AddImported("YOURLS");
            }

            if (!string.IsNullOrEmpty(source.PolrAPIHostname))
            {
                target.PolrAPIHostname = source.PolrAPIHostname;
                target.PolrAPIKey = source.PolrAPIKey;
                target.PolrIsSecret = source.PolrIsSecret;
                target.PolrUseAPIv1 = source.PolrUseAPIv1;
                result.AddImported("Polr");
            }

            if (!string.IsNullOrEmpty(source.FirebaseWebAPIKey))
            {
                target.FirebaseWebAPIKey = source.FirebaseWebAPIKey;
                target.FirebaseDynamicLinkDomain = source.FirebaseDynamicLinkDomain;
                target.FirebaseIsShort = source.FirebaseIsShort;
                result.AddImported("Firebase Dynamic Links");
            }

            if (source.KuttSettings != null && !string.IsNullOrEmpty(source.KuttSettings.APIKey))
            {
                target.KuttSettings = source.KuttSettings;
                result.AddImported("Kutt");
            }
        }
    }

    /// <summary>
    /// Result of import operation.
    /// </summary>
    public class ImportResult
    {
        private const string LogPrefix = "[UploadersConfigImporter]";

        public List<string> ImportedUploaders { get; } = new List<string>();

        public int TotalImported => ImportedUploaders.Count;

        public void AddImported(string uploaderName)
        {
            ImportedUploaders.Add(uploaderName);
            DebugHelper.WriteLine($"{LogPrefix} Imported: {uploaderName}");
        }

        public string GetSummary()
        {
            if (TotalImported == 0)
            {
                return "No uploader settings found to import.";
            }

            return $"Successfully imported {TotalImported} uploader(s):{Environment.NewLine}" +
                   string.Join(Environment.NewLine, ImportedUploaders.Select(u => $"- {u}"));
        }
    }
}
