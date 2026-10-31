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
using XerahS.Platform.Windows.Recording.Native;

namespace XerahS.Tests.Platform.Windows;

[TestFixture]
public class WindowsNativeRecordingAudioTests
{
    private const long Origin = 1_000_000_000;
    private const int Frames = 480; // 10 ms at 48 kHz

    private static float[] Constant(int frames, float left, float right)
    {
        float[] packet = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            packet[i * 2] = left;
            packet[i * 2 + 1] = right;
        }
        return packet;
    }

    private static long TicksForFrames(long frames) => frames * TimeSpan.TicksPerSecond / AudioMixer.SampleRate;

    [Test]
    public void Mixer_PassesSamplesThroughSampleExact_WhenTimestampsAreContinuous()
    {
        var clock = new RecordingClock();
        clock.Start(Origin);
        var mixer = new AudioMixer(clock);

        for (int packet = 0; packet < 4; packet++)
        {
            mixer.Write(0, Origin + TicksForFrames(packet * Frames), Frames, Constant(Frames, 0.25f, -0.5f), 1f, false);
        }

        short[] output = new short[Frames * 2];
        mixer.Read(Frames, output);

        Assert.That(output.Where((_, i) => i % 2 == 0), Is.All.EqualTo((short)(0.25f * 32767)));
        Assert.That(output.Where((_, i) => i % 2 == 1), Is.All.EqualTo((short)(-0.5f * 32767)));
        Assert.That(mixer.Discontinuities, Is.Zero);
    }

    [Test]
    public void Mixer_SumsSourcesWithPerSourceGain_AndClipsDeterministically()
    {
        var clock = new RecordingClock();
        clock.Start(Origin);
        var mixer = new AudioMixer(clock);

        for (int packet = 0; packet < 3; packet++)
        {
            long timestamp = Origin + TicksForFrames(packet * Frames);
            mixer.Write(0, timestamp, Frames, Constant(Frames, 0.5f, 0.75f), 1f, false);   // system audio
            mixer.Write(1, timestamp, Frames, Constant(Frames, 0.25f, 0.75f), 2f, false);  // microphone, gain 2
        }

        short[] output = new short[100 * 2];
        mixer.Read(10, output);

        // Left: 0.5 + 0.25 * 2 = 1.0 -> 32767. Right: 0.75 + 1.5 = 2.25 -> clipped to 32767.
        Assert.That(output, Is.All.EqualTo((short)32767));
    }

    [Test]
    public void Mixer_FillsMissingAudioWithSilence()
    {
        var clock = new RecordingClock();
        clock.Start(Origin);
        var mixer = new AudioMixer(clock);

        short[] output = new short[Frames * 2];
        mixer.Read(0, output);

        Assert.That(output, Is.All.EqualTo((short)0));
    }

    [Test]
    public void Mixer_DropsNonFiniteSamples()
    {
        var clock = new RecordingClock();
        clock.Start(Origin);
        var mixer = new AudioMixer(clock);

        mixer.Write(0, Origin, Frames, Constant(Frames, float.NaN, float.PositiveInfinity), 1f, false);
        short[] output = new short[100 * 2];
        mixer.Read(0, output);

        Assert.That(output, Is.All.EqualTo((short)0));
    }

    [Test]
    public void Mixer_IgnoresAudioBeforeStartAndWhilePaused()
    {
        var clock = new RecordingClock();
        var mixer = new AudioMixer(clock);

        // Not started: nothing maps to the media timeline.
        mixer.Write(0, Origin, Frames, Constant(Frames, 0.5f, 0.5f), 1f, false);
        Assert.That(clock.TryMap(Origin, out _, out _), Is.False);

        clock.Start(RecordingClock.Now);
        clock.Pause();
        Assert.That(clock.IsPaused, Is.True);
        Assert.That(clock.TryMap(RecordingClock.Now, out _, out _), Is.False);
    }

    [Test]
    public void Mixer_TracksOpposingEndpointDriftForSixtySimulatedSeconds_WithoutCaptureGaps()
    {
        var clock = new RecordingClock();
        clock.Start(Origin);
        var mixer = new AudioMixer(clock);
        float[] loopback = Constant(Frames, 0.125f, 0.125f);
        float[] microphone = Constant(Frames, 0.25f, 0.25f);
        short[] output = new short[Frames * 2];
        long gaps = 0;
        for (int packet = 0; packet < 6000; packet++)
        {
            long elapsed = TicksForFrames((long)packet * Frames);
            mixer.Write(0, Origin + (long)(elapsed * 1.0002), Frames, loopback, 1, false);
            mixer.Write(1, Origin + (long)(elapsed * 0.9998), Frames, microphone, 1, false);
            if (packet < 3) continue; // Model the encoder's audio lookahead.
            mixer.Read((long)(packet - 3) * Frames, output);
            if (packet > 3) gaps += output.Count(sample => sample < 11000 || sample > 13500);
        }
        Assert.That(gaps, Is.Zero, "Constant signals should remain mixed while each endpoint clock drifts by 200 ppm.");
        Assert.That(mixer.Discontinuities, Is.Zero);
    }

    [Test]
    public void Mixer_SilentPacketsAdvanceTimeline_AndExplicitDiscontinuityRepositionsSource()
    {
        var clock = new RecordingClock();
        clock.Start(Origin);
        var mixer = new AudioMixer(clock);
        mixer.Write(0, Origin, Frames, ReadOnlySpan<float>.Empty, 1, false);
        mixer.Write(0, Origin + TicksForFrames(Frames * 2), Frames, Constant(Frames, 0.5f, 0.5f), 1, true);
        short[] output = new short[Frames * 2];
        mixer.Read(0, output);
        Assert.That(output, Is.All.Zero);
        mixer.Read(Frames * 2, output.AsSpan(0, 100));
        Assert.That(output.Take(100), Is.All.EqualTo((short)(0.5f * 32767)));
    }

    [Test]
    public void Clock_ExcludesPausedTimeFromMediaTimeline()
    {
        var clock = new RecordingClock();
        long start = RecordingClock.Now;
        clock.Start(start);

        Thread.Sleep(20);
        clock.Pause();
        Thread.Sleep(150);
        clock.Resume();

        long now = RecordingClock.Now;
        Assert.That(clock.TryMap(now, out long relative, out long segment), Is.True);
        Assert.That(segment, Is.GreaterThan(start));

        long wall = now - start;
        // At least the 150 ms pause is removed from media time.
        Assert.That(relative, Is.LessThanOrEqualTo(wall - TimeSpan.FromMilliseconds(140).Ticks));
        Assert.That(relative, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15).Ticks));

        // Packets captured before the resume belong to the previous segment and are rejected.
        Assert.That(clock.TryMap(segment - 1, out _, out _), Is.False);

        clock.Stop();
        long stopped = clock.Elapsed;
        Thread.Sleep(20);
        Assert.That(clock.Elapsed, Is.EqualTo(stopped));
    }
}
