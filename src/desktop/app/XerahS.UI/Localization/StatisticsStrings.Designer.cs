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

using System.Globalization;
using System.Resources;

namespace XerahS.UI.Localization;

public static class StatisticsStrings
{
    private static readonly ResourceManager ResourceManager = new("XerahS.UI.Localization.StatisticsStrings", typeof(StatisticsStrings).Assembly);
    public static string HistoryWindow_CalculatingStatistics => ResourceManager.GetString(nameof(HistoryWindow_CalculatingStatistics), CultureInfo.CurrentUICulture) ?? nameof(HistoryWindow_CalculatingStatistics);
    public static string StatsWindow_AboutADay => ResourceManager.GetString(nameof(StatsWindow_AboutADay), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_AboutADay);
    public static string StatsWindow_AboutAWeek => ResourceManager.GetString(nameof(StatsWindow_AboutAWeek), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_AboutAWeek);
    public static string StatsWindow_ActiveDays => ResourceManager.GetString(nameof(StatsWindow_ActiveDays), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_ActiveDays);
    public static string StatsWindow_Afternoon => ResourceManager.GetString(nameof(StatsWindow_Afternoon), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Afternoon);
    public static string StatsWindow_All => ResourceManager.GetString(nameof(StatsWindow_All), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_All);
    public static string StatsWindow_AllYears => ResourceManager.GetString(nameof(StatsWindow_AllYears), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_AllYears);
    public static string StatsWindow_BeforeHistory => ResourceManager.GetString(nameof(StatsWindow_BeforeHistory), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_BeforeHistory);
    public static string StatsWindow_BusiestDay => ResourceManager.GetString(nameof(StatsWindow_BusiestDay), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_BusiestDay);
    public static string StatsWindow_BusiestOn => ResourceManager.GetString(nameof(StatsWindow_BusiestOn), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_BusiestOn);
    public static string StatsWindow_BusiestTime => ResourceManager.GetString(nameof(StatsWindow_BusiestTime), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_BusiestTime);
    public static string StatsWindow_CountingSince => ResourceManager.GetString(nameof(StatsWindow_CountingSince), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_CountingSince);
    public static string StatsWindow_DayOfWeek => ResourceManager.GetString(nameof(StatsWindow_DayOfWeek), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_DayOfWeek);
    public static string StatsWindow_Days => ResourceManager.GetString(nameof(StatsWindow_Days), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Days);
    public static string StatsWindow_Destinations => ResourceManager.GetString(nameof(StatsWindow_Destinations), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Destinations);
    public static string StatsWindow_Evening => ResourceManager.GetString(nameof(StatsWindow_Evening), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Evening);
    public static string StatsWindow_EveryDay => ResourceManager.GetString(nameof(StatsWindow_EveryDay), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_EveryDay);
    public static string StatsWindow_FileTypes => ResourceManager.GetString(nameof(StatsWindow_FileTypes), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_FileTypes);
    public static string StatsWindow_Images => ResourceManager.GetString(nameof(StatsWindow_Images), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Images);
    public static string StatsWindow_Less => ResourceManager.GetString(nameof(StatsWindow_Less), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Less);
    public static string StatsWindow_LongestStreak => ResourceManager.GetString(nameof(StatsWindow_LongestStreak), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_LongestStreak);
    public static string StatsWindow_LongestYet => ResourceManager.GetString(nameof(StatsWindow_LongestYet), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_LongestYet);
    public static string StatsWindow_More => ResourceManager.GetString(nameof(StatsWindow_More), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_More);
    public static string StatsWindow_Morning => ResourceManager.GetString(nameof(StatsWindow_Morning), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Morning);
    public static string StatsWindow_Night => ResourceManager.GetString(nameof(StatsWindow_Night), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Night);
    public static string StatsWindow_NoData => ResourceManager.GetString(nameof(StatsWindow_NoData), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_NoData);
    public static string StatsWindow_NoHistory => ResourceManager.GetString(nameof(StatsWindow_NoHistory), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_NoHistory);
    public static string StatsWindow_NoneInPrevious7Days => ResourceManager.GetString(nameof(StatsWindow_NoneInPrevious7Days), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_NoneInPrevious7Days);
    public static string StatsWindow_NonePrevious7DaysEither => ResourceManager.GetString(nameof(StatsWindow_NonePrevious7DaysEither), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_NonePrevious7DaysEither);
    public static string StatsWindow_NothingYet => ResourceManager.GetString(nameof(StatsWindow_NothingYet), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_NothingYet);
    public static string StatsWindow_OneDay => ResourceManager.GetString(nameof(StatsWindow_OneDay), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_OneDay);
    public static string StatsWindow_Other => ResourceManager.GetString(nameof(StatsWindow_Other), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Other);
    public static string StatsWindow_OtherFiles => ResourceManager.GetString(nameof(StatsWindow_OtherFiles), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_OtherFiles);
    public static string StatsWindow_Past7Days => ResourceManager.GetString(nameof(StatsWindow_Past7Days), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Past7Days);
    public static string StatsWindow_PastYear => ResourceManager.GetString(nameof(StatsWindow_PastYear), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_PastYear);
    public static string StatsWindow_Recordings => ResourceManager.GetString(nameof(StatsWindow_Recordings), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Recordings);
    public static string StatsWindow_Refresh => ResourceManager.GetString(nameof(StatsWindow_Refresh), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Refresh);
    public static string StatsWindow_Share => ResourceManager.GetString(nameof(StatsWindow_Share), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Share);
    public static string StatsWindow_ShareToolTip => ResourceManager.GetString(nameof(StatsWindow_ShareToolTip), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_ShareToolTip);
    public static string StatsWindow_Stats => ResourceManager.GetString(nameof(StatsWindow_Stats), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Stats);
    public static string StatsWindow_Streak => ResourceManager.GetString(nameof(StatsWindow_Streak), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Streak);
    public static string StatsWindow_TimeOfDay => ResourceManager.GetString(nameof(StatsWindow_TimeOfDay), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_TimeOfDay);
    public static string StatsWindow_Title => ResourceManager.GetString(nameof(StatsWindow_Title), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Title);
    public static string StatsWindow_Today => ResourceManager.GetString(nameof(StatsWindow_Today), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Today);
    public static string StatsWindow_TopApps => ResourceManager.GetString(nameof(StatsWindow_TopApps), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_TopApps);
    public static string StatsWindow_Total => ResourceManager.GetString(nameof(StatsWindow_Total), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_Total);
    public static string StatsWindow_TotalCount => ResourceManager.GetString(nameof(StatsWindow_TotalCount), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_TotalCount);
    public static string StatsWindow_VsPrevious7Days => ResourceManager.GetString(nameof(StatsWindow_VsPrevious7Days), CultureInfo.CurrentUICulture) ?? nameof(StatsWindow_VsPrevious7Days);
}
