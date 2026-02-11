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
using System.Drawing;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class AutoCaptureViewModel : ViewModelBase, IDisposable
{
    private readonly System.Timers.Timer _captureTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly Stopwatch _stopwatch = new();
    private int _delayMs;
    private Rectangle _customRegion;
    private bool _isFullScreenCapture;
    private bool _disposed;

    public event EventHandler? MinimizeRequested;

    [ObservableProperty]
    private bool _isFullScreen = true;

    [ObservableProperty]
    private string _regionText = "";

    [ObservableProperty]
    private bool _hasRegion;

    [ObservableProperty]
    private decimal _repeatTime = 60;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _waitUploads = true;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _startStopText = "Start";

    [ObservableProperty]
    private int _captureCount;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isSelectRegionEnabled;

    public AutoCaptureViewModel()
    {
        _captureTimer = new System.Timers.Timer();
        _captureTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(OnCaptureTimerElapsed);

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _statusTimer.Tick += (_, _) => UpdateStatus();

        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = SettingsManager.Settings;
        if (settings == null) return;

        _customRegion = settings.AutoCaptureRegion;
        RepeatTime = settings.AutoCaptureRepeatTime;
        MinimizeToTray = settings.AutoCaptureMinimizeToTray;
        WaitUploads = settings.AutoCaptureWaitUpload;

        if (_customRegion.IsEmpty)
        {
            IsFullScreen = true;
        }

        UpdateRegionDisplay();
    }

    partial void OnIsFullScreenChanged(bool value)
    {
        IsSelectRegionEnabled = !value;

        if (value)
        {
            _isFullScreenCapture = true;
        }
        else
        {
            _isFullScreenCapture = false;
        }

        UpdateRegionDisplay();
    }

    partial void OnRepeatTimeChanged(decimal value)
    {
        var settings = SettingsManager.Settings;
        if (settings != null)
        {
            settings.AutoCaptureRepeatTime = value;
        }
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        var settings = SettingsManager.Settings;
        if (settings != null)
        {
            settings.AutoCaptureMinimizeToTray = value;
        }
    }

    partial void OnWaitUploadsChanged(bool value)
    {
        var settings = SettingsManager.Settings;
        if (settings != null)
        {
            settings.AutoCaptureWaitUpload = value;
        }
    }

    [RelayCommand]
    private void StartStop()
    {
        if (IsRunning)
        {
            StopCapture();
        }
        else
        {
            StartCapture();
        }
    }

    private void StartCapture()
    {
        if (!HasRegion && !_isFullScreenCapture)
        {
            DebugHelper.WriteLine("AutoCapture: No region selected, cannot start.");
            return;
        }

        IsRunning = true;
        StartStopText = "Stop";
        CaptureCount = 0;
        ProgressPercent = 0;
        StatusText = "";

        _delayMs = (int)(RepeatTime * 1000);
        _captureTimer.Interval = 1000; // First capture after 1 second
        _captureTimer.Enabled = true;
        _statusTimer.IsEnabled = true;
        _stopwatch.Restart();

        if (MinimizeToTray)
        {
            MinimizeRequested?.Invoke(this, EventArgs.Empty);
        }

        DebugHelper.WriteLine($"AutoCapture: Started with interval {RepeatTime}s, fullscreen={_isFullScreenCapture}");
    }

    public void StopCapture()
    {
        if (!IsRunning) return;

        IsRunning = false;
        StartStopText = "Start";
        _captureTimer.Enabled = false;
        _statusTimer.IsEnabled = false;
        _stopwatch.Reset();
        ProgressPercent = 0;
        StatusText = $"Stopped. Total captures: {CaptureCount}";

        DebugHelper.WriteLine($"AutoCapture: Stopped after {CaptureCount} captures.");
    }

    [RelayCommand]
    private async Task SelectRegionAsync()
    {
        if (!PlatformServices.IsInitialized) return;

        var rect = await PlatformServices.ScreenCapture.SelectRegionAsync();
        if (rect == SKRectI.Empty) return;

        _customRegion = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);

        var settings = SettingsManager.Settings;
        if (settings != null)
        {
            settings.AutoCaptureRegion = _customRegion;
        }

        UpdateRegionDisplay();
    }

    private void UpdateRegionDisplay()
    {
        if (_isFullScreenCapture)
        {
            RegionText = "Full screen";
            HasRegion = true;
        }
        else if (!_customRegion.IsEmpty)
        {
            RegionText = $"X: {_customRegion.X}, Y: {_customRegion.Y}, Width: {_customRegion.Width}, Height: {_customRegion.Height}";
            HasRegion = true;
        }
        else
        {
            RegionText = "No region selected";
            HasRegion = false;
        }
    }

    private async void OnCaptureTimerElapsed()
    {
        if (!IsRunning) return;

        if (WaitUploads && TaskManager.Instance.Tasks.Any(t => t.IsBusy))
        {
            _captureTimer.Interval = 1000;
            return;
        }

        _stopwatch.Restart();
        _captureTimer.Interval = _delayMs;
        CaptureCount++;

        await TakeScreenshotAsync();
    }

    private async Task TakeScreenshotAsync()
    {
        try
        {
            if (!PlatformServices.IsInitialized) return;

            var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings
                ?? new TaskSettingsCapture();

            var captureOptions = new CaptureOptions
            {
                UseModernCapture = captureSettings.UseModernCapture,
                ShowCursor = captureSettings.ShowCursor,
            };

            SKBitmap? bitmap;

            if (_isFullScreenCapture)
            {
                bitmap = await PlatformServices.ScreenCapture.CaptureFullScreenAsync(captureOptions);
            }
            else
            {
                var region = _customRegion;
                if (region.IsEmpty) return;

                bitmap = await PlatformServices.ScreenCapture.CaptureRectAsync(
                    new SKRect(region.Left, region.Top, region.Right, region.Bottom),
                    captureOptions);
            }

            if (bitmap == null)
            {
                DebugHelper.WriteLine("AutoCapture: Capture returned null.");
                return;
            }

            // Create task settings with annotation and notifications disabled
            var taskSettings = new TaskSettings
            {
                Job = WorkflowType.PrintScreen,
                AfterCaptureJob = (SettingsManager.DefaultTaskSettings?.AfterCaptureJob
                    ?? (AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.SaveImageToFile))
                    & ~AfterCaptureTasks.AnnotateImage,
                AfterUploadJob = SettingsManager.DefaultTaskSettings?.AfterUploadJob
                    ?? AfterUploadTasks.CopyURLToClipboard,
                GeneralSettings = new TaskSettingsGeneral
                {
                    PlaySoundAfterCapture = false,
                    PlaySoundAfterUpload = false,
                    PlaySoundAfterAction = false,
                    ShowToastNotificationAfterTaskCompleted = false,
                },
                ImageSettings = SettingsManager.DefaultTaskSettings?.ImageSettings
                    ?? new TaskSettingsImage(),
                CaptureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings
                    ?? new TaskSettingsCapture(),
                UploadSettings = SettingsManager.DefaultTaskSettings?.UploadSettings
                    ?? new TaskSettingsUpload(),
            };

            await TaskManager.Instance.StartTask(taskSettings, bitmap);

            DebugHelper.WriteLine($"AutoCapture: Capture #{CaptureCount} submitted to pipeline ({bitmap.Width}x{bitmap.Height}).");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AutoCapture: TakeScreenshot failed");
        }
    }

    private void UpdateStatus()
    {
        if (!IsRunning) return;

        var elapsed = (int)_stopwatch.ElapsedMilliseconds;
        var timeLeft = Math.Max(0, _delayMs - elapsed);
        var percent = _delayMs > 0 ? (int)(100 - (double)timeLeft / _delayMs * 100) : 0;

        ProgressPercent = Math.Clamp(percent, 0, 100);
        var secondsLeft = (timeLeft / 1000f).ToString("0.0");
        StatusText = $"Time left: {secondsLeft}s  {ProgressPercent}%  Total: {CaptureCount}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopCapture();
        _captureTimer.Dispose();
        _statusTimer.IsEnabled = false;

        try
        {
            SettingsManager.SaveAllSettings();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AutoCapture: Failed to save settings on dispose");
        }
    }
}
