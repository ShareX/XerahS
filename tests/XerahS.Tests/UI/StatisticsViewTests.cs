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

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using NUnit.Framework;
using XerahS.History;
using XerahS.UI.Controls;
using XerahS.UI.Views;

namespace XerahS.Tests.UI;

[TestFixture]
public class StatisticsViewTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0);

    [AvaloniaTest]
    public async Task DashboardRendersChartsFiltersYearRangesAndFullSnapshot()
    {
        List<HistoryItem> items = Enumerable.Range(0, 730).SelectMany(day =>
            Enumerable.Range(0, day % 17 + 1).Select(i => new HistoryItem
            {
                DateTime = Now.Date.AddDays(-day).AddHours(i % 24),
                FileName = i % 5 == 0 ? "clip.mp4" : "capture.png",
                Host = "Example destination", Tags = new() { ["ProcessName"] = "browser" }
            })).ToList();
        bool shared = false;
        var view = new StatisticsView(() => Task.FromResult(items), image => { shared = true; image.Dispose(); }, () => Now);
        foreach (string theme in new[] { "Typography", "ThemeResources" })
            view.Styles.Insert(0, new StyleInclude(new Uri("avares://XerahS.UI/"))
            { Source = new Uri($"avares://XerahS.UI/Themes/{theme}.axaml") });
        try
        {
            await view.RefreshAsync();
            view.Show();
            await WaitForLoad(view);
            var calendar = view.FindControl<StatsCalendar>("Calendar")!;
            var chart = view.FindControl<StatsHourChart>("HourChart")!;
            Assert.Multiple(() =>
            {
                Assert.That(view.FindControl<TextBlock>("TotalText")!.Text, Is.EqualTo(items.Count.ToString("N0")));
                Assert.That(calendar.Stats!.Total.Total, Is.EqualTo(items.Count));
                Assert.That(calendar.Bounds.Height, Is.GreaterThan(80));
                Assert.That(chart.Bounds.Height, Is.EqualTo(230));
                Assert.That(view.FindControl<ItemsControl>("AppsList")!.ItemCount, Is.EqualTo(1));
            });
            view.FindControl<Button>("FilterRecordingsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(chart.Filter, Is.EqualTo(HistoryStatsFilter.Recordings));
            view.FindControl<ComboBox>("SpanComboBox")!.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.That(calendar.Span, Is.EqualTo(StatsCalendar.AllYears));
            Assert.That(calendar.Bounds.Height, Is.GreaterThan(200));
            view.FindControl<ComboBox>("SpanComboBox")!.SelectedIndex = 0;
            view.FindControl<Button>("FilterAllButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            view.FindControl<Button>("ShareButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(shared, Is.True, "Share must deliver a rendered image to the image-task callback.");

            foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                view.RequestedThemeVariant = theme;
                view.Width = 1060;
                view.Height = 900;
                Dispatcher.UIThread.RunJobs();
                Capture(view, $"statistics-dashboard-{theme.Key}");
                using var snapshot = view.RenderSnapshot();
                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.Width, Is.GreaterThan(900));
                    Assert.That(snapshot.Height, Is.GreaterThan(900), "Share must include content below the viewport.");
                });
            }
            view.Width = 720;
            view.Height = 480;
            Dispatcher.UIThread.RunJobs();
            Assert.That(chart.Bounds.Width, Is.GreaterThan(500));
            Capture(view, "statistics-dashboard-small");
        }
        finally { view.Close(); }
    }

    [AvaloniaTest]
    public async Task EmptyFailureAndRetryHaveExplicitStates()
    {
        bool fail = false;
        var view = new StatisticsView(() => fail
            ? Task.FromException<List<HistoryItem>>(new IOException("Database unavailable"))
            : Task.FromResult(new List<HistoryItem>()), _ => { }, () => Now);
        await view.RefreshAsync();
        Assert.Multiple(() =>
        {
            Assert.That(view.FindControl<StackPanel>("ContentPanel")!.IsVisible, Is.False);
            Assert.That(view.FindControl<TextBlock>("StatusText")!.Text, Does.Contain("history"));
            Assert.That(view.FindControl<Button>("ShareButton")!.IsEnabled, Is.False);
        });
        fail = true;
        await view.RefreshAsync();
        Assert.That(view.FindControl<TextBlock>("StatusText")!.Text, Is.EqualTo("Database unavailable"));
        fail = false;
        await view.RefreshAsync();
        Assert.That(view.FindControl<TextBlock>("StatusText")!.Text, Does.Contain("history"));
    }

    private static async Task WaitForLoad(StatisticsView view)
    {
        for (int i = 0; i < 100 && !view.FindControl<Button>("RefreshButton")!.IsEnabled; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        Dispatcher.UIThread.RunJobs();
        Assert.That(view.FindControl<Button>("RefreshButton")!.IsEnabled, Is.True);
    }

    private static void Capture(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("XERAHS_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        using var stream = File.Create(Path.Combine(directory, name + ".png"));
        frame?.Save(stream, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
