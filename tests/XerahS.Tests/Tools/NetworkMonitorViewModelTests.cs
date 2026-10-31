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

using Avalonia.Headless.NUnit;
using NUnit.Framework;
using XerahS.Common.NetworkMonitor;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Tools;

[TestFixture]
[NonParallelizable]
public class NetworkMonitorViewModelTests
{
    [AvaloniaTest]
    public void InitialState_ShowsCheckingBeforeFirstProbeCompletes()
    {
        using NetworkMonitorHost host = new(new BlockingProbe(), persist: false);
        using NetworkMonitorViewModel viewModel = new(host);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusText, Is.EqualTo("Checking"));
            Assert.That(viewModel.StatusDetails, Does.StartWith("Waiting for a reply"));
            Assert.That(viewModel.CurrentLatencyText, Is.EqualTo("-"));
        });
    }

    [AvaloniaTest]
    public async Task ExistingSample_ShowsCurrentAndAverageLatency()
    {
        DateTime now = DateTime.Now;
        using NetworkMonitorHost host = new(new StableProbe(18), clock: () => now, persist: false);
        await host.Monitor.CheckOnceAsync();

        using NetworkMonitorViewModel viewModel = new(host);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusText, Is.EqualTo("Connected"));
            Assert.That(viewModel.CurrentLatencyText, Is.EqualTo("18 ms"));
            Assert.That(viewModel.AverageLatencyText, Is.EqualTo("18 ms"));
            Assert.That(viewModel.AvailabilityText, Is.EqualTo("100.0%"));
        });
    }

    [AvaloniaTest]
    public void TargetAndIntervalSelection_UpdateMonitorOptions()
    {
        using NetworkMonitorHost host = new(new BlockingProbe(), persist: false);
        using NetworkMonitorViewModel viewModel = new(host);

        viewModel.SelectedTarget = viewModel.TargetOptions.Single(option => option.DisplayName.StartsWith("Google DNS", StringComparison.Ordinal));
        viewModel.SelectedInterval = viewModel.IntervalOptions.Single(option => option.Interval == TimeSpan.FromSeconds(30));
        NetworkMonitorOptions options = host.Monitor.Options;

        Assert.Multiple(() =>
        {
            Assert.That(options.PingAddresses, Is.EqualTo(new[] { "8.8.8.8" }));
            Assert.That(options.PingIntervalMs, Is.EqualTo(30000));
            Assert.That(options.FailThreshold, Is.EqualTo(2));
        });
    }

    [AvaloniaTest]
    public void CustomHost_ParsesSeveralAddresses()
    {
        using NetworkMonitorHost host = new(new BlockingProbe(), persist: false);
        using NetworkMonitorViewModel viewModel = new(host);

        viewModel.SelectedTarget = viewModel.TargetOptions.Single(option => option.IsCustom);
        viewModel.CustomHosts = "192.0.2.1, dns.example";
        NetworkMonitorOptions options = host.Monitor.Options;

        Assert.That(options.PingAddresses, Is.EqualTo(new[] { "192.0.2.1", "dns.example" }));
        Assert.That(viewModel.StatusDetails, Is.EqualTo("Waiting for a reply from Custom host."));
    }

    [Test]
    public void BackgroundMonitoring_IsOptIn_ForNewAndExistingSettings()
    {
        Assert.That(new XerahS.Core.ApplicationConfig().NetworkMonitorEnabled, Is.False);
        var existing = Newtonsoft.Json.JsonConvert.DeserializeObject<XerahS.Core.ApplicationConfig>("{}");
        Assert.That(existing!.NetworkMonitorEnabled, Is.False);
    }

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public void ClosingWindow_StopsProbesUnlessBackgroundMonitoringWasEnabled(bool backgroundEnabled)
    {
        using NetworkMonitorHost host = new(new BlockingProbe(), persist: false);
        var viewModel = new NetworkMonitorViewModel(host, () => backgroundEnabled);
        Assert.That(host.Monitor.IsMonitoring, Is.True, "Opening the diagnostic explicitly starts a temporary session.");
        viewModel.Dispose();
        Assert.That(host.Monitor.IsMonitoring, Is.EqualTo(backgroundEnabled));
    }

    [AvaloniaTest]
    public void StopCommand_CanStopEvenAnOptedInBackgroundSession()
    {
        using NetworkMonitorHost host = new(new BlockingProbe(), persist: false);
        using var viewModel = new NetworkMonitorViewModel(host, () => true);
        viewModel.StopCommand.Execute(null);
        Assert.That(host.Monitor.IsMonitoring, Is.False);
    }

    private sealed class StableProbe(long latencyMs) : INetworkProbe
    {
        public Task<NetworkProbeResult> ProbeAsync(string host, int timeoutMs, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NetworkProbeResult(true, latencyMs));
        }
    }

    private sealed class BlockingProbe : INetworkProbe
    {
        public async Task<NetworkProbeResult> ProbeAsync(
            string host,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new NetworkProbeResult(false, null);
        }
    }
}
