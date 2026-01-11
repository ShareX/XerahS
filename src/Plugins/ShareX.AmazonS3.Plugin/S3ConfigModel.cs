#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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

using XerahS.Uploaders.FileUploaders;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Configuration model for Amazon S3 uploader
/// </summary>
public class S3ConfigModel
{
    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public string ObjectPrefix { get; set; } = "ShareX/%y/%mo";

    public bool UseCustomCNAME { get; set; } = false;

    public string CustomDomain { get; set; } = string.Empty;

    public AmazonS3StorageClass StorageClass { get; set; } = AmazonS3StorageClass.Standard;

    public bool SetPublicACL { get; set; } = true;

    public bool UsePathStyleUrl { get; set; } = false;

    public bool SignedPayload { get; set; } = false;

    public string Endpoint { get; set; } = "s3.amazonaws.com";

    public bool RemoveExtensionImage { get; set; } = false;

    public bool RemoveExtensionVideo { get; set; } = false;

    public bool RemoveExtensionText { get; set; } = false;
}
