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
using XerahS.Platform.Abstractions;
using XerahS.Uploaders; // For GenericUploader and UploadResult
using XerahS.Uploaders.PluginSystem;
using System.Drawing;
using System.Drawing.Imaging;

namespace XerahS.Core.Tasks;

/// <summary>
/// Represents a minimal workflow task for quick automation (Path A)
/// Handles: Capture → Upload → URL to Clipboard
/// </summary>
public class WorkflowTask : IDisposable
{
    public event EventHandler<UploadProgressEventArgs>? UploadProgressChanged;
    public event EventHandler? TaskCompleted;

    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public Image? Image { get; set; }
    public Stream? Data { get; set; }
    public UploadResult? Result { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsError => Result?.IsError ?? false;

    private bool _disposed;

    /// <summary>
    /// Creates a workflow task for image upload
    /// </summary>
    public static WorkflowTask CreateImageUploadTask(Image image, string fileName)
    {
        return new WorkflowTask
        {
            Image = image,
            FileName = fileName
        };
    }

    /// <summary>
    /// Executes the workflow: Image → Stream → Upload → URL → Clipboard
    /// </summary>
    public async Task ExecuteAsync()
    {
        try
        {
            // Step 1: Convert image to stream
            if (Image == null)
            {
                Result = new UploadResult { IsSuccess = false };
                Result.Errors.Add("No image provided");
                return;
            }

            Data = ConvertImageToStream(Image);

            // Step 2: Upload to configured provider
            Result = await UploadAsync();

            // Step 3: Copy URL to clipboard if successful
            if (Result != null && !Result.IsError && !string.IsNullOrEmpty(Result.URL))
            {
                CopyURLToClipboard(Result.ToString());
            }
        }
        catch (Exception ex)
        {
            Result = new UploadResult { IsSuccess = false };
            Result.Errors.Add($"Workflow failed: {ex.Message}");
        }
        finally
        {
            IsCompleted = true;
            TaskCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private Stream ConvertImageToStream(Image image)
    {
        var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return stream;
    }

    private async Task<UploadResult> UploadAsync()
    {
        try
        {
            // Get the default image uploader instance
            var instanceManager = InstanceManager.Instance;
            var defaultInstance = instanceManager.GetDefaultInstance(UploaderCategory.Image);

            if (defaultInstance == null)
            {
                var result = new UploadResult { IsSuccess = false };
                result.Errors.Add("No image uploader configured. Please set a default uploader in settings.");
                return result;
            }

            // Get the provider
            var provider = ProviderCatalog.GetProvider(defaultInstance.ProviderId);
            if (provider == null)
            {
                var result = new UploadResult { IsSuccess = false };
                result.Errors.Add($"Provider '{defaultInstance.ProviderId}' not found.");
                return result;
            }

            // Create uploader instance from settings JSON
            var uploader = provider.CreateInstance(defaultInstance.SettingsJson) as GenericUploader;
            if (uploader == null)
            {
                var result = new UploadResult { IsSuccess = false };
                result.Errors.Add("Provider did not return a valid uploader instance.");
                return result;
            }

            // Ensure data is at start
            if (Data != null && Data.CanSeek)
            {
                Data.Position = 0;
            }

            // Perform upload using async wrapper
            var url = await uploader.UploadAsync(Data!, FileName);

            if (string.IsNullOrEmpty(url))
            {
                var result = new UploadResult { IsSuccess = false };
                result.Errors.Add("Upload succeeded but returned empty URL");
                return result;
            }

            return new UploadResult
            {
                URL = url,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            var result = new UploadResult { IsSuccess = false };
            result.Errors.Add($"Upload failed: {ex.Message}");
            return result;
        }
    }

    private void CopyURLToClipboard(string url)
    {
        try
        {
            PlatformServices.Clipboard.SetText(url);
        }
        catch (Exception ex)
        {
            // Non-critical failure, just log
            System.Diagnostics.Debug.WriteLine($"Failed to copy URL to clipboard: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Data?.Dispose();
        Image?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for upload progress
/// </summary>
public class UploadProgressEventArgs : EventArgs
{
    public long BytesSent { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage => TotalBytes > 0 ? (BytesSent / (double)TotalBytes) * 100 : 0;
}
