#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

#nullable enable

namespace XerahS.Common
{
    public class CodeMenuEntryFilename : CodeMenuEntry
    {
        private const string CategoryWindow = "Window";
        private const string CategoryDateTime = "Date and Time";
        private const string CategoryIncremental = "Incremental";
        private const string CategoryRandom = "Random";
        private const string CategoryImage = "Image";
        private const string CategoryComputer = "Computer";

        protected override string Prefix { get; } = "%";

        public static readonly CodeMenuEntryFilename t = new CodeMenuEntryFilename("t", "Title of active window", CategoryWindow);
        public static readonly CodeMenuEntryFilename pn = new CodeMenuEntryFilename("pn", "Process name of active window", CategoryWindow);
        public static readonly CodeMenuEntryFilename y = new CodeMenuEntryFilename("y", "Current year", CategoryDateTime);
        public static readonly CodeMenuEntryFilename yy = new CodeMenuEntryFilename("yy", "Current year (2 digits)", CategoryDateTime);
        public static readonly CodeMenuEntryFilename mo = new CodeMenuEntryFilename("mo", "Current month", CategoryDateTime);
        public static readonly CodeMenuEntryFilename mon = new CodeMenuEntryFilename("mon", "Current month name (Local language)", CategoryDateTime);
        public static readonly CodeMenuEntryFilename mon2 = new CodeMenuEntryFilename("mon2", "Current month name (English)", CategoryDateTime);
        public static readonly CodeMenuEntryFilename w = new CodeMenuEntryFilename("w", "Current week name (Local language)", CategoryDateTime);
        public static readonly CodeMenuEntryFilename w2 = new CodeMenuEntryFilename("w2", "Current week name (English)", CategoryDateTime);
        public static readonly CodeMenuEntryFilename wy = new CodeMenuEntryFilename("wy", "Week of year", CategoryDateTime);
        public static readonly CodeMenuEntryFilename d = new CodeMenuEntryFilename("d", "Current day", CategoryDateTime);
        public static readonly CodeMenuEntryFilename h = new CodeMenuEntryFilename("h", "Current hour", CategoryDateTime);
        public static readonly CodeMenuEntryFilename mi = new CodeMenuEntryFilename("mi", "Current minute", CategoryDateTime);
        public static readonly CodeMenuEntryFilename s = new CodeMenuEntryFilename("s", "Current second", CategoryDateTime);
        public static readonly CodeMenuEntryFilename ms = new CodeMenuEntryFilename("ms", "Current millisecond", CategoryDateTime);
        public static readonly CodeMenuEntryFilename pm = new CodeMenuEntryFilename("pm", "Gets AM/PM", CategoryDateTime);
        public static readonly CodeMenuEntryFilename unix = new CodeMenuEntryFilename("unix", "Unix timestamp", CategoryDateTime);
        public static readonly CodeMenuEntryFilename i = new CodeMenuEntryFilename("i", "Auto increment number", CategoryIncremental);
        public static readonly CodeMenuEntryFilename ia = new CodeMenuEntryFilename("ia", "Auto increment alphanumeric", CategoryIncremental);
        public static readonly CodeMenuEntryFilename iAa = new CodeMenuEntryFilename("iAa", "Auto increment alphanumeric (all)", CategoryIncremental);
        public static readonly CodeMenuEntryFilename ib = new CodeMenuEntryFilename("ib", "Auto increment base alphanumeric", CategoryIncremental);
        public static readonly CodeMenuEntryFilename ix = new CodeMenuEntryFilename("ix", "Auto increment hexadecimal", CategoryIncremental);
        public static readonly CodeMenuEntryFilename rn = new CodeMenuEntryFilename("rn", "Random number 0 to 9", CategoryRandom);
        public static readonly CodeMenuEntryFilename ra = new CodeMenuEntryFilename("ra", "Random alphanumeric char", CategoryRandom);
        public static readonly CodeMenuEntryFilename rna = new CodeMenuEntryFilename("rna", "Random non-ambiguous alphanumeric char, repeat using {n}", CategoryRandom);
        public static readonly CodeMenuEntryFilename rx = new CodeMenuEntryFilename("rx", "Random hexadecimal", CategoryRandom);
        public static readonly CodeMenuEntryFilename guid = new CodeMenuEntryFilename("guid", "Random guid", CategoryRandom);
        public static readonly CodeMenuEntryFilename radjective = new CodeMenuEntryFilename("radjective", "Random adjective", CategoryRandom);
        public static readonly CodeMenuEntryFilename ranimal = new CodeMenuEntryFilename("ranimal", "Random animal", CategoryRandom);
        public static readonly CodeMenuEntryFilename remoji = new CodeMenuEntryFilename("remoji", "Random emoji", CategoryRandom);
        public static readonly CodeMenuEntryFilename rf = new CodeMenuEntryFilename("rf", "Random line from file", CategoryRandom);
        public static readonly CodeMenuEntryFilename width = new CodeMenuEntryFilename("width", "Gets image width", CategoryImage);
        public static readonly CodeMenuEntryFilename height = new CodeMenuEntryFilename("height", "Gets image height", CategoryImage);
        public static readonly CodeMenuEntryFilename un = new CodeMenuEntryFilename("un", "User name", CategoryComputer);
        public static readonly CodeMenuEntryFilename uln = new CodeMenuEntryFilename("uln", "User login name", CategoryComputer);
        public static readonly CodeMenuEntryFilename cn = new CodeMenuEntryFilename("cn", "Computer name", CategoryComputer);
        public static readonly CodeMenuEntryFilename n = new CodeMenuEntryFilename("n", "New line");

        public CodeMenuEntryFilename(string value, string description, string? category = null) : base(value, description, category)
        {
        }
    }
}
