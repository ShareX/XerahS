#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using XerahS.Common;
using XerahS.Uploaders;
using System.ComponentModel;

namespace ShareX.UploadersLib.FileUploaders
{
    public class FTPAccount : XerahS.Uploaders.FileUploaders.FTPAccount
    {
    }

    public class AmazonS3Settings : XerahS.Uploaders.FileUploaders.AmazonS3Settings
    {
    }

    public class PomfUploader : XerahS.Uploaders.FileUploaders.PomfUploader
    {
        public PomfUploader()
        {
        }

        public PomfUploader(string uploadURL, string resultURL = null)
            : base(uploadURL, resultURL)
        {
        }
    }

    public class LocalhostAccount : ICloneable
    {
        [Category("Localhost"), Description("Shown in the list as: Name - LocalhostRoot:Port")]
        public string Name { get; set; }

        [Category("Localhost"), Description(@"Root folder, e.g. C:\Inetpub\wwwroot")]
        public string LocalhostRoot { get; set; }

        [Category("Localhost"), Description("Port Number"), DefaultValue(80)]
        public int Port { get; set; }

        [Category("Localhost")]
        public string UserName { get; set; }

        [Category("Localhost"), PasswordPropertyText(true), JsonEncrypt]
        public string Password { get; set; }

        [Category("Localhost"), Description("Localhost Sub-folder Path, e.g. screenshots, %y = year, %mo = month. SubFolderPath will be automatically appended to HttpHomePath if HttpHomePath does not start with @")]
        public string SubFolderPath { get; set; }

        [Category("Localhost"), Description("HTTP Home Path, %host = Host e.g. google.com without http:// because you choose that in Remote Protocol.\nURL = HttpHomePath + SubFolderPath + FileName\nURL = Host + SubFolderPath + FileName (if HttpHomePath is empty)")]
        public string HttpHomePath { get; set; }

        [Category("Localhost"), Description("Automatically add sub folder path to end of http home path"), DefaultValue(true)]
        public bool HttpHomePathAutoAddSubFolderPath { get; set; }

        [Category("Localhost"), Description("Don't add file extension to URL"), DefaultValue(false)]
        public bool HttpHomePathNoExtension { get; set; }

        [Category("Localhost"), Description("Choose an appropriate protocol to be accessed by the browser. Use 'file' for Shared Folders. RemoteProtocol will always be 'file' if HTTP Home Path is empty. "), DefaultValue(BrowserProtocol.file)]
        public BrowserProtocol RemoteProtocol { get; set; }

        [Category("Localhost"), Description("file://Host:Port"), Browsable(false)]
        public string LocalUri
        {
            get
            {
                if (string.IsNullOrEmpty(LocalhostRoot))
                {
                    return string.Empty;
                }

                return new Uri(FileHelpers.ExpandFolderVariables(LocalhostRoot)).AbsoluteUri;
            }
        }

        private string exampleFileName = "screenshot.jpg";

        [Category("Localhost"), Description("Preview of the Localhost Path based on the settings above")]
        public string PreviewLocalPath => GetLocalhostUri(exampleFileName);

        [Category("Localhost"), Description("Preview of the HTTP Path based on the settings above")]
        public string PreviewRemotePath => GetUriPath(exampleFileName);

        public LocalhostAccount()
        {
            Name = "New account";
            LocalhostRoot = "";
            Port = 80;
            SubFolderPath = "";
            HttpHomePath = "";
            HttpHomePathAutoAddSubFolderPath = true;
            HttpHomePathNoExtension = false;
            RemoteProtocol = BrowserProtocol.file;
        }

        public string GetSubFolderPath()
        {
            return NameParser.Parse(NameParserType.URL, SubFolderPath.Replace("%host", FileHelpers.ExpandFolderVariables(LocalhostRoot)));
        }

        public string GetHttpHomePath()
        {
            // @ deprecated
            if (!string.IsNullOrEmpty(HttpHomePath) && HttpHomePath.StartsWith("@", StringComparison.Ordinal))
            {
                HttpHomePath = HttpHomePath.Substring(1);
                HttpHomePathAutoAddSubFolderPath = false;
            }

            string httpHomePath = URLHelpers.RemovePrefixes(HttpHomePath);

            return NameParser.Parse(NameParserType.URL, httpHomePath.Replace("%host", FileHelpers.ExpandFolderVariables(LocalhostRoot)));
        }

        public string GetUriPath(string fileName)
        {
            if (string.IsNullOrEmpty(LocalhostRoot))
            {
                return string.Empty;
            }

            if (HttpHomePathNoExtension)
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            fileName = URLHelpers.URLEncode(fileName);

            string subFolderPath = GetSubFolderPath();
            subFolderPath = URLHelpers.URLEncode(subFolderPath, true);

            string httpHomePath = GetHttpHomePath();

            string path;

            if (string.IsNullOrEmpty(httpHomePath))
            {
                RemoteProtocol = BrowserProtocol.file;
                path = LocalUri.Replace("file://", "", StringComparison.Ordinal);
            }
            else
            {
                path = URLHelpers.URLEncode(httpHomePath, true);
            }

            if (Port != 80)
            {
                path = $"{path}:{Port}";
            }

            if (HttpHomePathAutoAddSubFolderPath)
            {
                path = URLHelpers.CombineURL(path, subFolderPath);
            }

            path = URLHelpers.CombineURL(path, fileName);

            string remoteProtocol = EnumExtensions.GetDescription(RemoteProtocol);

            if (!path.StartsWith(remoteProtocol, StringComparison.Ordinal))
            {
                path = remoteProtocol + path;
            }

            return path;
        }

        public string GetLocalhostPath(string fileName)
        {
            if (string.IsNullOrEmpty(LocalhostRoot))
            {
                return string.Empty;
            }

            return Path.Combine(Path.Combine(FileHelpers.ExpandFolderVariables(LocalhostRoot), GetSubFolderPath()), fileName);
        }

        public string GetLocalhostUri(string fileName)
        {
            string localhostAddress = LocalUri;

            if (string.IsNullOrEmpty(localhostAddress))
            {
                return string.Empty;
            }

            return URLHelpers.CombineURL(localhostAddress, GetSubFolderPath(), fileName);
        }

        public override string ToString()
        {
            return $"{Name} - {EnumExtensions.GetDescription(RemoteProtocol)}:{Port}";
        }

        public LocalhostAccount Clone()
        {
            return MemberwiseClone() as LocalhostAccount;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }

    public class PlikSettings
    {
        public string URL { get; set; } = "";
        [JsonEncrypt]
        public string APIKey { get; set; } = "";
        public bool IsSecured { get; set; } = false;
        public string Login { get; set; } = "";
        [JsonEncrypt]
        public string Password { get; set; } = "";
        public bool Removable { get; set; } = false;
        public bool OneShot { get; set; } = false;
        public int TTLUnit { get; set; } = 2;
        public decimal TTL { get; set; } = 30;
        public bool HasComment { get; set; } = false;
        public string Comment { get; set; } = "";
    }

    public class PushbulletDevice
    {
        public string Key { get; set; }
        public string Name { get; set; }
    }

    public class PushbulletSettings
    {
        [JsonEncrypt]
        public string UserAPIKey { get; set; } = "";
        public List<PushbulletDevice> DeviceList { get; set; } = new List<PushbulletDevice>();
        public int SelectedDevice { get; set; } = 0;

        public PushbulletDevice CurrentDevice
        {
            get
            {
                if (DeviceList != null && SelectedDevice >= 0 && SelectedDevice < DeviceList.Count)
                {
                    return DeviceList[SelectedDevice];
                }

                return null;
            }
        }
    }

    public class LambdaSettings
    {
        [JsonEncrypt]
        public string UserAPIKey { get; set; } = "";
        public string UploadURL { get; set; } = "https://lbda.net/";
    }

    public class LobFileSettings
    {
        [JsonEncrypt]
        public string UserAPIKey { get; set; } = "";
    }

    public class OneDriveFileInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string webUrl { get; set; }
    }

    public class GoogleDriveSharedDrive
    {
        public string id { get; set; }
        public string name { get; set; }

        public override string ToString()
        {
            return name;
        }
    }

    public class BoxFileEntry
    {
        public string type { get; set; }
        public string id { get; set; }
        public string sequence_id { get; set; }
        public string etag { get; set; }
        public string name { get; set; }
        public BoxFileEntry parent { get; set; }
    }

    public static class OneDrive
    {
        public static OneDriveFileInfo RootFolder { get; } = new OneDriveFileInfo
        {
            id = "",
            name = "Root folder"
        };
    }

    public static class GoogleDrive
    {
        public static GoogleDriveSharedDrive MyDrive { get; } = new GoogleDriveSharedDrive
        {
            id = "",
            name = "My Drive"
        };
    }

    public static class Box
    {
        public static BoxFileEntry RootFolder { get; } = new BoxFileEntry
        {
            type = "folder",
            id = "0",
            name = "Root folder"
        };
    }
}

namespace ShareX.UploadersLib.ImageUploaders
{
    public enum ImgurThumbnailType
    {
        Small_Square,
        Big_Square,
        Small_Thumbnail,
        Medium_Thumbnail,
        Large_Thumbnail,
        Huge_Thumbnail
    }

    public class ImgurAlbumData
    {
        public string id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int datetime { get; set; }
        public string cover { get; set; }
        public string cover_width { get; set; }
        public string cover_height { get; set; }
        public string account_url { get; set; }
        public long? account_id { get; set; }
        public string privacy { get; set; }
        public string layout { get; set; }
        public int views { get; set; }
        public string link { get; set; }
        public bool favorite { get; set; }
        public bool? nsfw { get; set; }
        public string section { get; set; }
        public int order { get; set; }
        public string deletehash { get; set; }
        public int images_count { get; set; }
        public ImgurImageData[] images { get; set; }
    }

    public class ImgurImageData
    {
        public string id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int datetime { get; set; }
        public string type { get; set; }
        public bool animated { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int size { get; set; }
        public int views { get; set; }
        public long bandwidth { get; set; }
        public string deletehash { get; set; }
        public string name { get; set; }
        public string section { get; set; }
        public string link { get; set; }
        public string gifv { get; set; }
        public string mp4 { get; set; }
        public string webm { get; set; }
        public bool looping { get; set; }
        public bool favorite { get; set; }
        public bool? nsfw { get; set; }
        public string vote { get; set; }
        public string comment_preview { get; set; }
    }

    public class ImageShackOptions
    {
        public string Username { get; set; }
        [JsonEncrypt]
        public string Password { get; set; }
        public bool IsPublic { get; set; }
        public string Auth_token { get; set; }
        public int ThumbnailWidth { get; set; } = 256;
        public int ThumbnailHeight { get; set; }
    }

    public class FlickrSettings
    {
        public bool DirectLink { get; set; } = true;

        [Description("The title of the photo.")]
        public string Title { get; set; }

        [Description("A description of the photo. May contain some limited HTML.")]
        public string Description { get; set; }

        [Description("A space-seperated list of tags to apply to the photo.")]
        public string Tags { get; set; }

        [Description("Set to 0 for no, 1 for yes. Specifies who can view the photo.")]
        public string IsPublic { get; set; }

        [Description("Set to 0 for no, 1 for yes. Specifies who can view the photo.")]
        public string IsFriend { get; set; }

        [Description("Set to 0 for no, 1 for yes. Specifies who can view the photo.")]
        public string IsFamily { get; set; }

        [Description("Set to 1 for Safe, 2 for Moderate, or 3 for Restricted.")]
        public string SafetyLevel { get; set; }

        [Description("Set to 1 for Photo, 2 for Screenshot, or 3 for Other.")]
        public string ContentType { get; set; }

        [Description("Set to 1 to keep the photo in global search results, 2 to hide from public searches.")]
        public string Hidden { get; set; }
    }

    public class PhotobucketAccountInfo
    {
        public string Subdomain { get; set; }
        public string AlbumID { get; set; }
        public List<string> AlbumList { get; set; } = new List<string>();
        public int ActiveAlbumID { get; set; } = 0;

        public string ActiveAlbumPath => AlbumList[ActiveAlbumID];
    }
}

namespace ShareX.UploadersLib.TextUploaders
{
    public enum PastebinPrivacy
    {
        Public,
        Unlisted,
        Private
    }

    public enum PastebinExpiration
    {
        N,
        M10,
        H1,
        D1,
        W1,
        W2,
        M1
    }

    public class PastebinSettings
    {
        public string Username { get; set; }
        [JsonEncrypt]
        public string Password { get; set; }
        public PastebinPrivacy Exposure { get; set; } = PastebinPrivacy.Unlisted;
        public PastebinExpiration Expiration { get; set; } = PastebinExpiration.N;
        public string Title { get; set; }
        public string TextFormat { get; set; } = "text";
        [JsonEncrypt]
        public string UserKey { get; set; }
        public bool RawURL { get; set; }
    }
}

namespace ShareX.UploadersLib.URLShorteners
{
    public class KuttSettings
    {
        public string Host { get; set; } = "https://kutt.it";
        [JsonEncrypt]
        public string APIKey { get; set; }
        [JsonEncrypt]
        public string Password { get; set; }
        public bool Reuse { get; set; }
        public string Domain { get; set; }
    }
}

namespace ShareX.UploadersLib
{
}
