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
using System.Collections.Generic;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.Orchestration;

internal enum CaptureDecisionOutcome
{
    Skipped,
    Failed,
    Cancelled,
    Succeeded
}

internal sealed class CaptureDecisionStep
{
    public CaptureDecisionStep(
        LinuxCaptureStage stage,
        string providerId,
        CaptureDecisionOutcome outcome,
        string? reason = null)
    {
        Stage = stage;
        ProviderId = providerId;
        Outcome = outcome;
        Reason = reason;
    }

    public LinuxCaptureStage Stage { get; }

    public string ProviderId { get; }

    public CaptureDecisionOutcome Outcome { get; }

    public string? Reason { get; }
}

internal sealed class CaptureDecisionTrace
{
    private readonly List<CaptureDecisionStep> _steps = new();

    public CaptureDecisionTrace(LinuxCaptureKind requestKind)
    {
        RequestKind = requestKind;
        StartedUtc = DateTimeOffset.UtcNow;
    }

    public LinuxCaptureKind RequestKind { get; }

    public DateTimeOffset StartedUtc { get; }

    public DateTimeOffset? CompletedUtc { get; private set; }

    public string? FinalProviderId { get; private set; }

    public CaptureDecisionOutcome? FinalOutcome { get; private set; }

    public IReadOnlyList<CaptureDecisionStep> Steps => _steps;

    public void AddStep(LinuxCaptureStage stage, string providerId, CaptureDecisionOutcome outcome, string? reason = null)
    {
        _steps.Add(new CaptureDecisionStep(stage, providerId, outcome, reason));
    }

    public void Complete(string providerId, CaptureDecisionOutcome outcome)
    {
        FinalProviderId = providerId;
        FinalOutcome = outcome;
        CompletedUtc = DateTimeOffset.UtcNow;
    }
}
