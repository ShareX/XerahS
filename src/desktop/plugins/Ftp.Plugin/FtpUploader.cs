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

using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using FluentFTP;
using FluentFTP.Exceptions;
using Renci.SshNet;
using Renci.SshNet.Common;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.FileUploaders;

namespace ShareX.Ftp.Plugin;

/// <summary>
/// File uploader for FTP, FTPS (FluentFTP), and SFTP (SSH.NET).
/// Uses FluentFTP for FTP/FTPS (modern, robust, SSL/TLS) and SSH.NET for SFTP.
/// </summary>
public sealed class FtpUploader : FileUploader, IDisposable
{
    private readonly FTPAccount _account;
    private FtpClient? _ftpClient;
    private SftpClient? _sftpClient;

    public FtpUploader(FTPAccount account)
    {
        _account = account ?? throw new ArgumentNullException(nameof(account));
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        UploadResult result = new UploadResult();

        if (string.IsNullOrWhiteSpace(_account.Host))
        {
            Errors.Add("FTP host is required.");
            return result;
        }

        string remoteFileName = GetSafeRemoteFileName(fileName);
        string subFolderPath = _account.GetSubFolderPath(null, NameParserType.FilePath);
        string remotePath = URLHelpers.CombineURL(subFolderPath, remoteFileName);
        string url = _account.GetUriPath(remoteFileName, subFolderPath);

        OnEarlyURLCopyRequested(url);

        try
        {
            IsUploading = true;

            bool ok = _account.Protocol switch
            {
                FTPProtocol.FTP or FTPProtocol.FTPS => UploadFtp(stream, remotePath),
                FTPProtocol.SFTP => UploadSftp(stream, remotePath),
                _ => false
            };

            if (ok && !StopUploadRequested && !IsError)
            {
                result.URL = url;
            }
        }
        finally
        {
            Dispose();
            IsUploading = false;
        }

        return result;
    }

    public override void StopUpload()
    {
        if (IsUploading && !StopUploadRequested)
        {
            StopUploadRequested = true;
            try
            {
                _ftpClient?.Disconnect();
                _sftpClient?.Disconnect();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);
            }
        }
    }

    private static string NormalizeHost(string? host) => host?.Trim() ?? string.Empty;

    private static string GetSafeRemoteFileName(string? fileName)
    {
        string normalized = (fileName ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        string candidate = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        candidate = candidate.Trim();

        return string.IsNullOrEmpty(candidate) || candidate == "." || candidate == ".." ? "upload" : candidate;
    }

    private bool UploadFtp(Stream stream, string remotePath)
    {
        try
        {
            _ftpClient = CreateFtpClient();
            if (!ConnectFtp())
                return false;

            try
            {
                return UploadFtpInternal(stream, remotePath);
            }
            catch (FtpCommandException ex) when (ex.CompletionCode == "550" || ex.CompletionCode == "553")
            {
                CreateMultiDirectoryFtp(GetRemoteDirectoryPath(remotePath));
                return UploadFtpInternal(stream, remotePath);
            }
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            return false;
        }
    }

    private FtpClient CreateFtpClient() => FtpClientFactory.CreateFtp(_account);

    private bool ConnectFtp()
    {
        if (_ftpClient == null) return false;
        if (!_ftpClient.IsConnected)
            _ftpClient.Connect();
        return _ftpClient.IsConnected;
    }

    private bool UploadFtpInternal(Stream stream, string remotePath)
    {
        if (_ftpClient == null || !_ftpClient.IsConnected) return false;
        using (Stream remoteStream = _ftpClient.OpenWrite(remotePath))
        {
            if (!TransferData(stream, remoteStream))
                return false;
        }
        return _ftpClient.GetReply().Success;
    }

    private static string GetRemoteDirectoryPath(string remotePath)
    {
        int lastSlash = remotePath.LastIndexOf('/');
        return lastSlash > 0 ? remotePath.Substring(0, lastSlash) : string.Empty;
    }

    private void CreateMultiDirectoryFtp(string remotePath)
    {
        if (_ftpClient == null || string.IsNullOrEmpty(remotePath)) return;
        List<string> paths = URLHelpers.GetPaths(remotePath);
        foreach (string path in paths)
        {
            if (!string.IsNullOrEmpty(path) && !_ftpClient.DirectoryExists(path))
            {
                try { _ftpClient.CreateDirectory(path); } catch { /* ignore */ }
            }
        }
    }

    private bool UploadSftp(Stream stream, string remotePath)
    {
        try
        {
            _sftpClient = CreateSftpClient();
            if (!ConnectSftp())
                return false;

            try
            {
                return UploadSftpInternal(stream, remotePath);
            }
            catch (SftpPathNotFoundException)
            {
                CreateMultiDirectorySftp(GetRemoteDirectoryPath(remotePath));
                return UploadSftpInternal(stream, remotePath);
            }
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            return false;
        }
    }

    private SftpClient? CreateSftpClient()
    {
        SftpClient? client = FtpClientFactory.CreateSftp(_account, out string? error);
        if (client == null && error != null)
        {
            Errors.Add(error);
        }

        return client;
    }

    private bool ConnectSftp()
    {
        if (_sftpClient == null) return false;
        if (!_sftpClient.IsConnected)
            _sftpClient.Connect();
        _sftpClient.BufferSize = (uint)BufferSize;
        return _sftpClient.IsConnected;
    }

    private bool UploadSftpInternal(Stream stream, string remotePath)
    {
        if (_sftpClient == null || !_sftpClient.IsConnected) return false;

        long fileSize = stream.CanSeek ? stream.Length : -1;
        ProgressManager? progress = fileSize > 0 ? new ProgressManager(fileSize) : null;
        ulong lastUploadedBytes = 0;
        Lock progressLock = new();

        // We have to use a lock here because UploadFile fires progress callbacks concurrently from multiple threads.
        _sftpClient.UploadFile(stream, remotePath, canOverride: true, uploadedBytes =>
        {
            if (StopUploadRequested)
            {
                _sftpClient.Disconnect();
                return;
            }

            if (AllowReportProgress && progress != null)
            {
                lock (progressLock)
                {
                    long delta = (long)(uploadedBytes - lastUploadedBytes);

                    if (delta > 0)
                    {
                        lastUploadedBytes = uploadedBytes;

                        if (progress.UpdateProgress(delta))
                        {
                            OnProgressChanged(progress);
                        }
                    }
                }
            }
        });

        return !StopUploadRequested;
    }

    private void CreateMultiDirectorySftp(string path)
    {
        if (_sftpClient == null || string.IsNullOrEmpty(path)) return;
        List<string> paths = URLHelpers.GetPaths(path);
        foreach (string dir in paths)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            try
            {
                if (!_sftpClient.Exists(dir))
                    _sftpClient.CreateDirectory(dir);
            }
            catch (SftpPermissionDeniedException) { }
        }
    }

    public void Dispose()
    {
        try
        {
            _ftpClient?.Dispose();
            _ftpClient = null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
        }
        try
        {
            _sftpClient?.Dispose();
            _sftpClient = null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
        }
    }
}
