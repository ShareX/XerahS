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

using System.Globalization;
using System.Reflection;
using System.Text;

namespace XerahS.Common
{
    public enum NameParserType
    {
        Default,
        Text, // Allows new line
        FileName,
        FilePath,
        URL // URL path encodes
    }

    public class NameParser
    {
        private static readonly Lazy<string[]> Adjectives = new(() => LoadWordList("adjectives.txt"));
        private static readonly Lazy<string[]> Animals = new(() => LoadWordList("animals.txt"));

        public NameParserType Type { get; private set; }
        public int MaxNameLength { get; set; }
        public int MaxTitleLength { get; set; }
        public int AutoIncrementNumber { get; set; } // %i, %ia, %ib, %iAa, %ix
        public int ImageWidth { get; set; } // %width
        public int ImageHeight { get; set; } // %height
        public string? WindowText { get; set; } // %t
        public string? ProcessName { get; set; } // %pn
        public TimeZoneInfo? CustomTimeZone { get; set; }
        public bool IsPreviewMode { get; set; }

        protected NameParser()
        {
        }

        public NameParser(NameParserType nameParserType)
        {
            Type = nameParserType;
        }

        public static string Parse(NameParserType nameParserType, string pattern)
        {
            return new NameParser(nameParserType).Parse(pattern);
        }

        public string Parse(string? pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(pattern);

            if (WindowText != null)
            {
                string windowText = SanitizeInput(WindowText);

                if (MaxTitleLength > 0)
                {
                    windowText = windowText.Truncate(MaxTitleLength);
                }

                sb.Replace(CodeMenuEntryFilename.t.ToPrefixString(), windowText);
            }

            if (ProcessName != null)
            {
                string processName = SanitizeInput(ProcessName);

                sb.Replace(CodeMenuEntryFilename.pn.ToPrefixString(), processName);
            }

            string width = ImageWidth > 0 ? ImageWidth.ToString(CultureInfo.InvariantCulture) : string.Empty;
            string height = ImageHeight > 0 ? ImageHeight.ToString(CultureInfo.InvariantCulture) : string.Empty;

            sb.Replace(CodeMenuEntryFilename.width.ToPrefixString(), width);
            sb.Replace(CodeMenuEntryFilename.height.ToPrefixString(), height);

            DateTime dt = DateTime.Now;

            if (CustomTimeZone != null)
            {
                dt = TimeZoneInfo.ConvertTime(dt, CustomTimeZone);
            }

            sb.Replace(CodeMenuEntryFilename.mon2.ToPrefixString(), CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(dt.Month))
                .Replace(CodeMenuEntryFilename.mon.ToPrefixString(), CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dt.Month))
                .Replace(CodeMenuEntryFilename.yy.ToPrefixString(), dt.ToString("yy", CultureInfo.InvariantCulture))
                .Replace(CodeMenuEntryFilename.y.ToPrefixString(), dt.Year.ToString(CultureInfo.InvariantCulture))
                .Replace(CodeMenuEntryFilename.mo.ToPrefixString(), GeneralHelpers.AddZeroes(dt.Month))
                .Replace(CodeMenuEntryFilename.d.ToPrefixString(), GeneralHelpers.AddZeroes(dt.Day));

            string hour;

            if (sb.ToString().Contains(CodeMenuEntryFilename.pm.ToPrefixString(), StringComparison.Ordinal))
            {
                hour = GeneralHelpers.HourTo12(dt.Hour);
            }
            else
            {
                hour = GeneralHelpers.AddZeroes(dt.Hour);
            }

            sb.Replace(CodeMenuEntryFilename.h.ToPrefixString(), hour)
                .Replace(CodeMenuEntryFilename.mi.ToPrefixString(), GeneralHelpers.AddZeroes(dt.Minute))
                .Replace(CodeMenuEntryFilename.s.ToPrefixString(), GeneralHelpers.AddZeroes(dt.Second))
                .Replace(CodeMenuEntryFilename.ms.ToPrefixString(), GeneralHelpers.AddZeroes(dt.Millisecond, 3))
                .Replace(CodeMenuEntryFilename.wy.ToPrefixString(), dt.WeekOfYear().ToString(CultureInfo.InvariantCulture))
                .Replace(CodeMenuEntryFilename.w2.ToPrefixString(), CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(dt.DayOfWeek))
                .Replace(CodeMenuEntryFilename.w.ToPrefixString(), CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(dt.DayOfWeek))
                .Replace(CodeMenuEntryFilename.pm.ToPrefixString(), dt.Hour >= 12 ? "PM" : "AM");

            sb.Replace(CodeMenuEntryFilename.unix.ToPrefixString(), DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

            if (sb.ToString().Contains(CodeMenuEntryFilename.i.ToPrefixString(), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.ib.ToPrefixString(), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.ib.ToPrefixString().Replace('b', 'B'), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.iAa.ToPrefixString(), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.iAa.ToPrefixString().Replace("Aa", "aA"), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.ia.ToPrefixString(), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.ia.ToPrefixString().Replace('a', 'A'), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.ix.ToPrefixString(), StringComparison.Ordinal)
                || sb.ToString().Contains(CodeMenuEntryFilename.ix.ToPrefixString().Replace('x', 'X'), StringComparison.Ordinal))
            {
                AutoIncrementNumber++;

                try
                {
                    foreach (Tuple<string, int[]> entry in ListEntryWithValues(sb.ToString(), CodeMenuEntryFilename.ib.ToPrefixString(), 2))
                    {
                        sb.Replace(entry.Item1, GeneralHelpers.AddZeroes(AutoIncrementNumber.ToBase(entry.Item2[0], GeneralHelpers.AlphanumericInverse), entry.Item2[1]));
                    }
                    foreach (Tuple<string, int[]> entry in ListEntryWithValues(sb.ToString(), CodeMenuEntryFilename.ib.ToPrefixString().Replace('b', 'B'), 2))
                    {
                        sb.Replace(entry.Item1, GeneralHelpers.AddZeroes(AutoIncrementNumber.ToBase(entry.Item2[0], GeneralHelpers.Alphanumeric), entry.Item2[1]));
                    }
                }
                catch
                {
                }

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.iAa.ToPrefixString()))
                {
                    sb.Replace(entry.Item1, GeneralHelpers.AddZeroes(AutoIncrementNumber.ToBase(62, GeneralHelpers.Alphanumeric), entry.Item2));
                }
                sb.Replace(CodeMenuEntryFilename.iAa.ToPrefixString(), AutoIncrementNumber.ToBase(62, GeneralHelpers.Alphanumeric));

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.iAa.ToPrefixString().Replace("Aa", "aA")))
                {
                    sb.Replace(entry.Item1, GeneralHelpers.AddZeroes(AutoIncrementNumber.ToBase(62, GeneralHelpers.AlphanumericInverse), entry.Item2));
                }
                sb.Replace(CodeMenuEntryFilename.iAa.ToPrefixString().Replace("Aa", "aA"), AutoIncrementNumber.ToBase(62, GeneralHelpers.AlphanumericInverse));

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.ia.ToPrefixString()))
                {
                    sb.Replace(entry.Item1, GeneralHelpers.AddZeroes(AutoIncrementNumber.ToBase(36, GeneralHelpers.Alphanumeric), entry.Item2).ToLowerInvariant());
                }
                sb.Replace(CodeMenuEntryFilename.ia.ToPrefixString(), AutoIncrementNumber.ToBase(36, GeneralHelpers.Alphanumeric).ToLowerInvariant());

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.ia.ToPrefixString().Replace('a', 'A')))
                {
                    sb.Replace(entry.Item1, GeneralHelpers.AddZeroes(AutoIncrementNumber.ToBase(36, GeneralHelpers.Alphanumeric), entry.Item2).ToUpperInvariant());
                }
                sb.Replace(CodeMenuEntryFilename.ia.ToPrefixString().Replace('a', 'A'), AutoIncrementNumber.ToBase(36, GeneralHelpers.Alphanumeric).ToUpperInvariant());

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.ix.ToPrefixString()))
                {
                    sb.Replace(entry.Item1, AutoIncrementNumber.ToString("x" + entry.Item2.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                }
                sb.Replace(CodeMenuEntryFilename.ix.ToPrefixString(), AutoIncrementNumber.ToString("x", CultureInfo.InvariantCulture));

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.ix.ToPrefixString().Replace('x', 'X')))
                {
                    sb.Replace(entry.Item1, AutoIncrementNumber.ToString("X" + entry.Item2.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                }
                sb.Replace(CodeMenuEntryFilename.ix.ToPrefixString().Replace('x', 'X'), AutoIncrementNumber.ToString("X", CultureInfo.InvariantCulture));

                foreach (Tuple<string, int> entry in ListEntryWithValue(sb.ToString(), CodeMenuEntryFilename.i.ToPrefixString()))
                {
                    sb.Replace(entry.Item1, AutoIncrementNumber.ToString("d" + entry.Item2.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                }
                sb.Replace(CodeMenuEntryFilename.i.ToPrefixString(), AutoIncrementNumber.ToString("d", CultureInfo.InvariantCulture));
            }

            sb.Replace(CodeMenuEntryFilename.un.ToPrefixString(), Environment.UserName);
            sb.Replace(CodeMenuEntryFilename.uln.ToPrefixString(), Environment.UserDomainName);
            sb.Replace(CodeMenuEntryFilename.cn.ToPrefixString(), Environment.MachineName);

            if (Type == NameParserType.Text)
            {
                sb.Replace(CodeMenuEntryFilename.n.ToPrefixString(), Environment.NewLine);
            }

            string result = sb.ToString();

            result = result.ReplaceAll(CodeMenuEntryFilename.radjective.ToPrefixString(),
                () => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(GetRandomWord(Adjectives.Value)));
            result = result.ReplaceAll(CodeMenuEntryFilename.ranimal.ToPrefixString(),
                () => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(GetRandomWord(Animals.Value)));

            foreach (Tuple<string, string> entry in ListEntryWithArgument(result, CodeMenuEntryFilename.rf.ToPrefixString()))
            {
                result = result.ReplaceAll(entry.Item1, () =>
                {
                    try
                    {
                        string path = entry.Item2;

                        if (FileHelpers.IsTextFile(path))
                        {
                            return GetRandomLineFromFile(path);
                        }

                        throw new InvalidOperationException("Valid text file path is required.");
                    }
                    catch (Exception e) when (IsPreviewMode)
                    {
                        return e.Message;
                    }
                });
            }

            foreach (Tuple<string, int> entry in ListEntryWithValue(result, CodeMenuEntryFilename.rna.ToPrefixString()))
            {
                result = result.ReplaceAll(entry.Item1, () => GeneralHelpers.RepeatGenerator(entry.Item2, () => GeneralHelpers.GetRandomChar(GeneralHelpers.Base56).ToString()));
            }

            foreach (Tuple<string, int> entry in ListEntryWithValue(result, CodeMenuEntryFilename.rn.ToPrefixString()))
            {
                result = result.ReplaceAll(entry.Item1, () => GeneralHelpers.RepeatGenerator(entry.Item2, () => GeneralHelpers.GetRandomChar(GeneralHelpers.Numbers).ToString()));
            }

            foreach (Tuple<string, int> entry in ListEntryWithValue(result, CodeMenuEntryFilename.ra.ToPrefixString()))
            {
                result = result.ReplaceAll(entry.Item1, () => GeneralHelpers.RepeatGenerator(entry.Item2, () => GeneralHelpers.GetRandomChar(GeneralHelpers.Alphanumeric).ToString()));
            }

            foreach (Tuple<string, int> entry in ListEntryWithValue(result, CodeMenuEntryFilename.rx.ToPrefixString()))
            {
                result = result.ReplaceAll(entry.Item1, () => GeneralHelpers.RepeatGenerator(entry.Item2, () => GeneralHelpers.GetRandomChar(GeneralHelpers.Hexadecimal.ToLowerInvariant()).ToString()));
            }

            foreach (Tuple<string, int> entry in ListEntryWithValue(result, CodeMenuEntryFilename.rx.ToPrefixString().Replace('x', 'X')))
            {
                result = result.ReplaceAll(entry.Item1, () => GeneralHelpers.RepeatGenerator(entry.Item2, () => GeneralHelpers.GetRandomChar(GeneralHelpers.Hexadecimal.ToUpperInvariant()).ToString()));
            }

            foreach (Tuple<string, int> entry in ListEntryWithValue(result, CodeMenuEntryFilename.remoji.ToPrefixString()))
            {
                result = result.ReplaceAll(entry.Item1, () => GeneralHelpers.RepeatGenerator(entry.Item2, () => RandomCrypto.Pick(Emoji.Emojis)));
            }

            result = result.ReplaceAll(CodeMenuEntryFilename.rna.ToPrefixString(), () => GeneralHelpers.GetRandomChar(GeneralHelpers.Base56).ToString());
            result = result.ReplaceAll(CodeMenuEntryFilename.rn.ToPrefixString(), () => GeneralHelpers.GetRandomChar(GeneralHelpers.Numbers).ToString());
            result = result.ReplaceAll(CodeMenuEntryFilename.ra.ToPrefixString(), () => GeneralHelpers.GetRandomChar(GeneralHelpers.Alphanumeric).ToString());
            result = result.ReplaceAll(CodeMenuEntryFilename.rx.ToPrefixString(), () => GeneralHelpers.GetRandomChar(GeneralHelpers.Hexadecimal.ToLowerInvariant()).ToString());
            result = result.ReplaceAll(CodeMenuEntryFilename.rx.ToPrefixString().Replace('x', 'X'), () => GeneralHelpers.GetRandomChar(GeneralHelpers.Hexadecimal.ToUpperInvariant()).ToString());
            result = result.ReplaceAll(CodeMenuEntryFilename.guid.ToPrefixString().ToLowerInvariant(), () => Guid.NewGuid().ToString().ToLowerInvariant());
            result = result.ReplaceAll(CodeMenuEntryFilename.guid.ToPrefixString().ToUpperInvariant(), () => Guid.NewGuid().ToString().ToUpperInvariant());
            result = result.ReplaceAll(CodeMenuEntryFilename.remoji.ToPrefixString(), () => RandomCrypto.Pick(Emoji.Emojis));

            if (Type == NameParserType.FileName)
            {
                result = FileHelpers.SanitizeFileName(result);
            }
            else if (Type == NameParserType.FilePath)
            {
                result = FileHelpers.SanitizePath(result);
            }
            else if (Type == NameParserType.URL)
            {
                result = URLHelpers.GetValidURL(result);
            }

            if (MaxNameLength > 0)
            {
                result = result.Truncate(MaxNameLength);
            }

            return result;
        }

        private string SanitizeInput(string input)
        {
            input = input.Trim().Replace(' ', '_');

            if (Type == NameParserType.FileName || Type == NameParserType.FilePath)
            {
                input = FileHelpers.SanitizeFileName(input);
            }

            return input;
        }

        private static string GetRandomWord(IReadOnlyList<string> words)
        {
            if (words == null || words.Count == 0)
            {
                return string.Empty;
            }

            string? word = GeneralHelpers.Pick(words.ToList());
            return word?.Trim() ?? string.Empty;
        }

        private string GetRandomLineFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("File not found.", path);
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                throw new InvalidOperationException("File is empty.");
            }

            string? line = GeneralHelpers.Pick(lines.ToList());
            return line?.Trim() ?? string.Empty;
        }

        private static string[] LoadWordList(string fileName)
        {
            try
            {
                string resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);

                if (File.Exists(resourcesPath))
                {
                    return File.ReadAllLines(resourcesPath)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToArray();
                }

                Assembly assembly = typeof(NameParser).Assembly;
                string? resourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        List<string> lines = new List<string>();
                        using StreamReader reader = new StreamReader(stream);
                        while (!reader.EndOfStream)
                        {
                            string? line = reader.ReadLine();

                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                lines.Add(line.Trim());
                            }
                        }
                        return lines.ToArray();
                    }
                }
            }
            catch
            {
            }

            return Array.Empty<string>();
        }

        private IEnumerable<Tuple<string, string[]>> ListEntryWithArguments(string text, string entry, int elements)
        {
            foreach (Tuple<string, string> o in text.ForEachBetween(entry + "{", "}"))
            {
                string[] s = o.Item2.Split(',');
                if (elements > s.Length)
                {
                    Array.Resize(ref s, elements);
                }
                yield return new Tuple<string, string[]>(o.Item1, s);
            }
        }

        private IEnumerable<Tuple<string, string>> ListEntryWithArgument(string text, string entry)
        {
            foreach (Tuple<string, string[]> o in ListEntryWithArguments(text, entry, 1))
            {
                yield return new Tuple<string, string>(o.Item1, o.Item2[0]);
            }
        }

        private IEnumerable<Tuple<string, int[]>> ListEntryWithValues(string text, string entry, int elements)
        {
            foreach (Tuple<string, string[]> o in ListEntryWithArguments(text, entry, elements))
            {
                int[] a = new int[o.Item2.Length];
                for (int i = o.Item2.Length - 1; i >= 0; --i)
                {
                    if (int.TryParse(o.Item2[i], out int n))
                    {
                        a[i] = n;
                    }
                }
                yield return new Tuple<string, int[]>(o.Item1, a);
            }
        }

        private IEnumerable<Tuple<string, int>> ListEntryWithValue(string text, string entry)
        {
            foreach (Tuple<string, int[]> o in ListEntryWithValues(text, entry, 1))
            {
                yield return new Tuple<string, int>(o.Item1, o.Item2[0]);
            }
        }
    }
}
