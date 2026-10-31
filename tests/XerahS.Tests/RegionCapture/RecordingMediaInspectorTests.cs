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

using NUnit.Framework;
using XerahS.CLI.Commands;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public sealed class RecordingMediaInspectorTests
{
    private const string NativeMedia = """
        {"streams":[
          {"codec_type":"video","codec_name":"h264","width":1280,"height":720,"avg_frame_rate":"30000/1001","duration":"4.000"},
          {"codec_type":"audio","codec_name":"aac","sample_rate":"48000","channels":2,"duration":"4.021"}],
         "format":{"duration":"4.021"}}
        """;

    [Test]
    public void NativeMedia_ParsesRationalsAndStringNumbers_AndValidatesExpectedStreams()
    {
        var report = RecordingMediaInspector.Parse(NativeMedia);
        Assert.That(report.Streams[0].FramesPerSecond, Is.EqualTo(29.97002997).Within(0.00001));
        Assert.That(report.Validate(true, 1, 4), Is.Empty);
        Assert.That(report.Validate(true, 0, 4), Has.Count.EqualTo(1));
    }

    [Test]
    public void Inspection_RejectsWrongCodecFormatDriftAndPausedWallTime()
    {
        var report = RecordingMediaInspector.Parse(NativeMedia.Replace("h264", "hevc").Replace("48000", "44100"));
        Assert.That(report.Validate(true, 1), Has.Count.EqualTo(2));
        report = RecordingMediaInspector.Parse(NativeMedia.Replace("4.000", "2.000"));
        Assert.That(report.Validate(true, 1), Has.Count.EqualTo(1));
        report = RecordingMediaInspector.Parse(NativeMedia);
        Assert.That(report.Validate(true, 1, 10), Has.Count.EqualTo(1));
    }

    [Test]
    public void Inspection_RejectsMissingStreamsAndUnknownDuration()
    {
        var report = RecordingMediaInspector.Parse("""{"streams":[],"format":{"duration":"N/A"}}""");
        Assert.That(report.Validate(true, 1), Has.Count.EqualTo(3));
    }
}
