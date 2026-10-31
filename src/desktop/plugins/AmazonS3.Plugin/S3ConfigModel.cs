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

using System;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Configuration model for Amazon S3 uploader
/// </summary>
public class S3ConfigModel
{
    public S3AuthMode AuthMode { get; set; } = S3AuthMode.AccessKeys;

    public string SecretKey { get; set; } = Guid.NewGuid().ToString("N");

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public string ObjectPrefix { get; set; } = "ShareX/%y/%mo";

    /// <summary>Media Explorer starts at the bucket root instead of the static part of <see cref="ObjectPrefix"/>.</summary>
    public bool ExplorerBrowseWholeBucket { get; set; } = false;

    public bool UseCustomCNAME { get; set; } = false;

    public string CustomDomain { get; set; } = string.Empty;

    public S3StorageClass StorageClass { get; set; } = S3StorageClass.Standard;

    public bool SetPublicACL { get; set; } = false;

    public bool SetPublicPolicy { get; set; } = false;

    public bool UsePathStyleUrl { get; set; } = false;

    public bool SignedPayload { get; set; } = true;

    public string Endpoint { get; set; } = "s3.amazonaws.com";

    public bool RemoveExtensionImage { get; set; } = false;

    public bool RemoveExtensionVideo { get; set; } = false;

    public bool RemoveExtensionText { get; set; } = false;

    /// <summary>
    /// Minimum file size to trigger multipart upload. Default: 50 MiB.
    /// </summary>
    public long MultipartThresholdBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>
    /// Size of each multipart chunk. Default: 10 MiB.
    /// </summary>
    public long MultipartPartSizeBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Maximum number of multipart chunks uploaded in parallel.
    /// </summary>
    public int MultipartMaxConcurrency { get; set; } = 4;

    public string SsoStartUrl { get; set; } = string.Empty;

    public string SsoRegion { get; set; } = "us-east-1";

    public string SsoAccountId { get; set; } = string.Empty;

    public string SsoRoleName { get; set; } = string.Empty;
}
