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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;

namespace XerahS.UI.ViewModels;

public partial class DownloaderWindowViewModel : ViewModelBase, IDisposable
{
    private readonly FileDownloader _downloader;
    private readonly UpdateChecker _updateChecker;
    private bool _disposed;

    [ObservableProperty]
    private string _statusText = Resources.GetString("Downloader_PreparingDownload");

    [ObservableProperty]
    private double _downloadPercentage;

    [ObservableProperty]
    private string _downloadSpeedText = string.Empty;

    [ObservableProperty]
    private string _downloadedSizeText = string.Empty;

    [ObservableProperty]
    private string _totalSizeText = string.Empty;

    [ObservableProperty]
    private bool _canCancel = true;

    [ObservableProperty]
    private DownloaderFormStatus _status = DownloaderFormStatus.Waiting;

    [ObservableProperty]
    private string _fileName = string.Empty;

    public bool? DialogResult { get; private set; }

    public Action<bool?>? RequestClose { get; set; }

    public string DownloadLocation { get; private set; } = string.Empty;

    public DownloaderWindowViewModel(UpdateChecker updateChecker)
    {
        _updateChecker = updateChecker;
        _downloader = new FileDownloader();
        _downloader.FileSizeReceived += OnFileSizeReceived;
        _downloader.ProgressChanged += OnProgressChanged;

        FileName = updateChecker.FileName;
    }

    public async Task StartDownloadAsync()
    {
        if (Status != DownloaderFormStatus.Waiting)
            return;

        if (string.IsNullOrEmpty(_updateChecker.DownloadURL))
        {
            StatusText = Resources.GetString("Downloader_UrlNotAvailable");
            return;
        }

        Status = DownloaderFormStatus.DownloadStarted;
        StatusText = Resources.GetString("Downloader_GettingFileSize");

        // Create temp folder
        string folderPath = Path.Combine(Path.GetTempPath(), AppResources.AppName);
        Directory.CreateDirectory(folderPath);
        DownloadLocation = Path.Combine(folderPath, _updateChecker.FileName);

        _downloader.URL = _updateChecker.DownloadURL;
        _downloader.DownloadLocation = DownloadLocation;

        // Set Accept header for GitHub API downloads
        if (_updateChecker is GitHubUpdateChecker)
        {
            _downloader.AcceptHeader = "application/octet-stream";
        }

        DebugHelper.WriteLine($"Downloading: \"{_updateChecker.DownloadURL}\" -> \"{DownloadLocation}\"");

        try
        {
            bool success = await _downloader.StartDownload();

            if (success)
            {
                StatusText = Resources.GetString("Downloader_CompletedStartingInstaller");
                Status = DownloaderFormStatus.DownloadCompleted;
                CanCancel = false;

                await LaunchInstallerAsync();
            }
            else if (_downloader.IsCanceled)
            {
                StatusText = Resources.GetString("Downloader_Canceled");
            }
            else
            {
                StatusText = Resources.GetString("Downloader_Failed");
            }
        }
        catch (Exception ex)
        {
            StatusText = Resources.GetString("Downloader_FailedWithMessage", ex.Message);
            DebugHelper.WriteException(ex, "Update download failed");
        }
    }

    private void OnFileSizeReceived()
    {
        Dispatcher.UIThread.Post(() =>
        {
            TotalSizeText = _downloader.FileSize.ToSizeString();
            StatusText = Resources.GetString("Downloader_Downloading", FileName);
        });
    }

    private void OnProgressChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            DownloadPercentage = _downloader.DownloadPercentage;
            DownloadSpeedText = $"{((long)_downloader.DownloadSpeed).ToSizeString()}/s";
            DownloadedSizeText = _downloader.DownloadedSize.ToSizeString();
        });
    }

    private async Task LaunchInstallerAsync()
    {
        Status = DownloaderFormStatus.InstallStarted;

        // Small delay to allow UI to update
        await Task.Delay(500);

        if (!VerifyInstallerSignature(DownloadLocation))
        {
            StatusText = Resources.GetString("Downloader_SignatureVerificationFailed");
            DebugHelper.WriteLine("Installer launch aborted: Authenticode signature is missing or invalid.");
            CanCancel = true;
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = DownloadLocation,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /UPDATE",
                UseShellExecute = true
            };

            Process.Start(startInfo);

            // Close the downloader window - the app will be shut down by UpdateService
            DialogResult = true;
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusText = Resources.GetString("Downloader_StartInstallerFailed", ex.Message);
            DebugHelper.WriteException(ex, "Failed to launch installer");
            CanCancel = true;
        }
    }

    private static readonly Guid WinTrustActionGenericVerifyV2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, IntPtr pWVTData);

    private static bool VerifyInstallerSignature(string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            DebugHelper.WriteLine("Installer Authenticode verification skipped: not running on Windows.");
            return true;
        }

        try
        {
            // 1) Authenticode content verification via WinVerifyTrust - verifies the PE content
            //    (hash of the file bytes), not just the certificate chain.
            if (!VerifyAuthenticodeContent(filePath))
            {
                DebugHelper.WriteLine("Installer Authenticode content verification failed (WinVerifyTrust).");
                return false;
            }

            // 2) Certificate chain verification.
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(filePath);

            if (!certificate.Verify())
            {
                DebugHelper.WriteLine($"Installer Authenticode signature is invalid. Publisher: {certificate.Subject}");
                return false;
            }

            // 3) Log the publisher.
            DebugHelper.WriteLine($"Installer Authenticode signature verified. Publisher: {certificate.Subject}");
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Installer Authenticode verification failed: {ex.Message}");
            DebugHelper.WriteException(ex, "Installer Authenticode verification failed.");
            return false;
        }
    }

    private static bool VerifyAuthenticodeContent(string filePath)
    {
        IntPtr fileInfoPtr = IntPtr.Zero;
        IntPtr dataPtr = IntPtr.Zero;

        try
        {
            WINTRUST_FILE_INFO fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            WINTRUST_DATA data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = 2, // WTD_UI_NONE
                fdwRevocationChecks = 0, // WTD_REVOKE_NONE
                dwUnionChoice = 1, // WTD_CHOICE_FILE
                dwStateAction = 0, // WTD_STATEACTION_IGNORE
                pFile = IntPtr.Zero
            };

            fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            data.pFile = fileInfoPtr;
            dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_DATA>());
            Marshal.StructureToPtr(data, dataPtr, false);

            Guid actionId = WinTrustActionGenericVerifyV2;
            uint status = WinVerifyTrust(IntPtr.Zero, ref actionId, dataPtr);

            if (status == 0) // ERROR_SUCCESS - content is genuine and signed.
            {
                return true;
            }

            if (status == 0x800B0100) // TRUST_E_NOSIGNATURE (2148204809)
            {
                DebugHelper.WriteLine("Installer has no Authenticode signature (TRUST_E_NOSIGNATURE).");
            }
            else
            {
                DebugHelper.WriteLine($"Installer Authenticode content verification failed. WinVerifyTrust status: 0x{status:X8}.");
            }

            return false;
        }
        finally
        {
            // WTD_STATEACTION_CLOSE releases the internal resources allocated by WinVerifyTrust.
            if (dataPtr != IntPtr.Zero)
            {
                try
                {
                    WINTRUST_DATA closeData = new WINTRUST_DATA
                    {
                        cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                        dwUIChoice = 2,
                        fdwRevocationChecks = 0,
                        dwUnionChoice = 1,
                        dwStateAction = 2, // WTD_STATEACTION_CLOSE
                        pFile = fileInfoPtr
                    };

                    Marshal.StructureToPtr(closeData, dataPtr, false);
                    Guid closeActionId = WinTrustActionGenericVerifyV2;
                    WinVerifyTrust(IntPtr.Zero, ref closeActionId, dataPtr);
                }
                catch
                {
                    // Best-effort cleanup; ignore failures.
                }
            }

            if (dataPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dataPtr);
            }

            if (fileInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(fileInfoPtr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [RelayCommand]
    private void Cancel()
    {
        if (Status == DownloaderFormStatus.DownloadStarted)
        {
            _downloader.StopDownload();
        }

        DialogResult = false;
        RequestClose?.Invoke(false);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _downloader.FileSizeReceived -= OnFileSizeReceived;
            _downloader.ProgressChanged -= OnProgressChanged;

            if (_downloader.IsDownloading)
            {
                _downloader.StopDownload();
            }

            _disposed = true;
        }
    }
}
