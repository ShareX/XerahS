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
using System.IO;
using System.Reflection;
using NUnit.Framework;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public sealed class FFmpegRecordingServiceTests
{
    private string? _originalPersonalFolder;

    [SetUp]
    public void SetUp()
    {
        _originalPersonalFolder = PathsManager.PersonalFolder;
        PathsManager.PersonalFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ffmpeg-recorder-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_originalPersonalFolder))
        {
            PathsManager.PersonalFolder = _originalPersonalFolder;
        }
    }

    [Test]
    public void BuildFFmpegArguments_UsesAbsoluteCustomOutputPath_AndCreatesMissingDirectory()
    {
        using var service = new FFmpegRecordingService();
        string relativeOutputPath = Path.Combine("ffmpeg-recordings", Guid.NewGuid().ToString("N"), "capture.mp4");
        string expectedOutputPath = Path.GetFullPath(relativeOutputPath);

        string args = InvokeBuildFFmpegArguments(service, new RecordingOptions
        {
            OutputPath = relativeOutputPath
        });

        Assert.Multiple(() =>
        {
            Assert.That(args, Does.Contain($"\"{expectedOutputPath}\""));
            Assert.That(Directory.Exists(Path.GetDirectoryName(expectedOutputPath)!), Is.True);
        });
    }

    [Test]
    public void WindowsFallback_MapsWasapiMicrophoneIds_AndMixesBothRequestedSources()
    {
        var settings = new ScreenRecordingSettings { CaptureSystemAudio = true, CaptureMicrophone = true, MicrophoneDeviceId = "endpoint-mic" };
        string args = string.Join(" ", FFmpegRecordingService.BuildWindowsAudioArguments(settings, new Devices()));
        Assert.Multiple(() =>
        {
            Assert.That(args, Does.Contain("Stereo Mix"));
            Assert.That(args, Does.Contain("USB Microphone"));
            Assert.That(args, Does.Not.Contain("endpoint-mic"));
            Assert.That(args, Does.Contain("amix=inputs=2"));
            Assert.That(args, Does.Contain("-map \"[mixed]\""));
        });
    }

    [Test]
    public void WindowsFallback_DefaultMicrophoneUsesTheActualDefault_AndMissingSelectionsFail()
    {
        var settings = new ScreenRecordingSettings { CaptureMicrophone = true };
        Assert.That(string.Join(" ", FFmpegRecordingService.BuildWindowsAudioArguments(settings, new Devices())), Does.Contain("USB Microphone"));
        settings.MicrophoneDeviceId = "missing";
        Assert.Throws<ArgumentException>(() => FFmpegRecordingService.BuildWindowsAudioArguments(settings, new Devices()));
        settings.CaptureMicrophone = false; settings.CaptureSystemAudio = true; settings.SystemAudioDeviceId = "other-output";
        Assert.Throws<NotSupportedException>(() => FFmpegRecordingService.BuildWindowsAudioArguments(settings, new Devices()));
    }

    private sealed class Devices : IRecordingDeviceService
    {
        public IReadOnlyList<RecordingDevice> GetMicrophones() => [new("endpoint-mic", "USB Microphone")];
        public IReadOnlyList<RecordingDevice> GetSystemAudioDevices() => [new("default-output", "Speakers")];
        public RecordingDevice? GetDefaultMicrophone() => GetMicrophones()[0];
        public RecordingDevice? GetDefaultSystemAudioDevice() => GetSystemAudioDevices()[0];
    }

    private static string InvokeBuildFFmpegArguments(FFmpegRecordingService service, RecordingOptions options)
    {
        var method = typeof(FFmpegRecordingService).GetMethod("BuildFFmpegArguments", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        object? result = method!.Invoke(service, new object[] { options });
        Assert.That(result, Is.TypeOf<string>());
        return (string)result;
    }
}
