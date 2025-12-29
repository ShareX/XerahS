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

using System;
using System.Collections.Generic;

namespace ShareX.UploadersLib.FileUploaders
{
    public class FTPAccount
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseSFTP { get; set; }
        public string? Keypath { get; set; }
        public string? Passphrase { get; set; }

        public string GetSubFolderPath()
        {
            return string.Empty;
        }

        public string GetUriPath(string path)
        {
            return path;
        }
    }

    public class LocalhostAccount
    {
        public string? Description { get; set; }
    }

    public class AmazonS3Settings
    {
        public string? ObjectPrefix { get; set; }
    }

    public class PlikSettings
    {
    }

    public class PushbulletSettings
    {
        public string? UserAPIKey { get; set; }
    }

    public class LambdaSettings
    {
        public string? FunctionName { get; set; }
    }

    public class LobFileSettings
    {
        public string? ApiKey { get; set; }
    }

    public class PomfUploader
    {
    }

    public class OneDriveFileInfo
    {
        public string? Id { get; set; }
    }

    public class GoogleDriveSharedDrive
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class BoxFileEntry
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public static class OneDrive
    {
        public static OneDriveFileInfo RootFolder { get; } = new OneDriveFileInfo();
    }

    public static class GoogleDrive
    {
        public static GoogleDriveSharedDrive MyDrive { get; } = new GoogleDriveSharedDrive();
    }

    public static class Box
    {
        public static BoxFileEntry RootFolder { get; } = new BoxFileEntry();
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
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class ImageShackOptions
    {
        public string? Username { get; set; }
    }

    public class FlickrSettings
    {
        public string? UserId { get; set; }
    }

    public class PhotobucketAccountInfo
    {
        public string? Username { get; set; }
    }
}

namespace ShareX.UploadersLib.TextUploaders
{
    public class PastebinSettings
    {
        public string? UserKey { get; set; }
    }
}

namespace ShareX.UploadersLib.URLShorteners
{
    public class KuttSettings
    {
        public string? ApiKey { get; set; }
    }
}

namespace ShareX.UploadersLib
{
}
