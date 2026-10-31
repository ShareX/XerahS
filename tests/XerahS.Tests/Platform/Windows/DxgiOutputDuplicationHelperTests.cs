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

using System.Runtime.InteropServices;
using NUnit.Framework;
using XerahS.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

public class DxgiOutputDuplicationPolicyTests
{
    [Test]
    public void ShouldUseDuplicateOutput1_FalseOnArm64()
    {
        Assert.That(DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(Architecture.Arm64, isHdrOutput: true, failedThisSession: false), Is.False);
        Assert.That(DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(Architecture.Arm, isHdrOutput: true, failedThisSession: false), Is.False);
    }

    [Test]
    public void ShouldUseDuplicateOutput1_TrueOnIntelHdrOutput()
    {
        Assert.That(DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(Architecture.X64, isHdrOutput: true, failedThisSession: false), Is.True);
        Assert.That(DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(Architecture.X86, isHdrOutput: true, failedThisSession: false), Is.True);
    }

    [Test]
    public void ShouldUseDuplicateOutput1_FalseOnSdrOutput()
    {
        // Regression: x64 SDR multi-monitor desktop crashed in DuplicateOutput1 (0.31.3, 2026-09-29 log).
        Assert.That(DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(Architecture.X64, isHdrOutput: false, failedThisSession: false), Is.False);
    }

    [Test]
    public void ShouldUseDuplicateOutput1_FalseAfterFailureThisSession()
    {
        Assert.That(DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(Architecture.X64, isHdrOutput: true, failedThisSession: true), Is.False);
    }
}
