# Graphical history statistics

Open **Statistics** from the sidebar, **View > Statistics**, or the History
 toolbar. This opens the separate **XerahS - Stats** window.

The dashboard ports ShareX's graphical statistics implementation from the
`develop` branch at commit `fdb0d415f7124b0f95e69e4c375cd6a2a25bf041`:

- [StatsWindow layout](https://github.com/ShareX/ShareX/blob/fdb0d415f7124b0f95e69e4c375cd6a2a25bf041/ShareX.HistoryLib/Views/StatsWindow.axaml).
- [Chart controls](https://github.com/ShareX/ShareX/blob/fdb0d415f7124b0f95e69e4c375cd6a2a25bf041/ShareX.HistoryLib/Views/StatsCharts.cs).
- [Statistics calculations](https://github.com/ShareX/ShareX/blob/fdb0d415f7124b0f95e69e4c375cd6a2a25bf041/ShareX.HistoryLib/Models/HistoryStats.cs).

It includes the total and image/recording/file split, today's activity, rolling
seven-day activity and comparison, current and longest streaks, and busiest day.
The calendar shows daily activity with date tooltips and past-year, all-years,
and individual-year ranges. The hourly chart filters all items, images, or
recordings and shows each part of the day's share. Bar lists show weekdays,
applications, destinations, and file types.

All calculations use the complete history database, independent of the History
page's search and pagination. GIF files count as recordings, matching ShareX.
Missing process or destination metadata is omitted from those breakdowns. The
window uses XerahS's theme resources and the system accent. Labels retain the
upstream localization resources and formatting.

**Refresh** reloads the database on a background thread. Empty and failed loads
have explicit states. **Share** renders the entire dashboard, including content
below the scroll viewport, without the action buttons, and submits the image to
XerahS's existing image-task pipeline using the default task settings.

`StatisticsTests` covers the aggregation rules and full-database loading.
`StatisticsViewTests` covers rendering, the hour filters, calendar ranges,
full-page image rendering, themes, empty history, and failed-load recovery.
Set `XERAHS_UI_CAPTURE_DIR` when running the UI tests to save rendered previews.

## Source applications

New standard screenshot and recording workflows snapshot the source window before
selection overlays take focus. Windows resolves its process ID to the executable
name, and explicit/custom-window captures use the selected target. History saves
that name in the existing `ProcessName` tag, which supplies the Top apps list.
Region and full-screen captures use the foreground app at capture start, rather
than attributing one image to every visible application.

An exited or inaccessible process leaves the app unknown without blocking the
capture. Existing privacy-review metadata stripping remains in effect. Older
records without an app tag are not backfilled. Refresh or reopen Stats after a
new capture to see the updated counts.
