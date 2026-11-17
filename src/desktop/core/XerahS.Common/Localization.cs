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

namespace XerahS.Common
{
    /// <summary>
    /// Central culture/language management for the desktop UI.
    /// </summary>
    public static class Localization
    {
        /// <summary>
        /// Raised after the UI culture changed, so live bindings (XAML `{local:Localize ...}`)
        /// can re-read their values without a restart.
        /// </summary>
        public static event Action? CultureChanged;
        /// <summary>
        /// Applies the requested culture to the current and default UI/format cultures.
        /// A null or empty culture code leaves the OS default culture untouched.
        /// </summary>
        public static void ApplyCulture(string? cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
            {
                return;
            }

            CultureInfo culture = CultureInfo.GetCultureInfo(cultureCode);

            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            CultureChanged?.Invoke();
        }

        /// <summary>
        /// Cultures that actually ship translations, discovered from the satellite assemblies at
        /// runtime. Dropping <c>Resources.&lt;culture&gt;.resx</c> into the project and rebuilding is
        /// enough for the language to appear in the selector — no code change and no list to edit.
        /// English (the neutral resource set) is always first.
        /// </summary>
        public static IReadOnlyList<CultureInfo> GetAvailableCultures()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cultures = new List<CultureInfo>();

            CultureInfo english = CultureInfo.GetCultureInfo("en");
            cultures.Add(english);
            seen.Add(english.Name);

            foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                if (culture.Equals(CultureInfo.InvariantCulture))
                {
                    continue;
                }

                try
                {
                    // A regional variant (ru-RU) falls back to its neutral parent's satellite (ru),
                    // so resolving the culture that actually ships the assembly keeps the list free
                    // of duplicate entries such as "Russian" and "Russian (Russia)".
                    CultureInfo? resourceCulture = ResolveResourceCulture(culture);
                    if (resourceCulture != null && seen.Add(resourceCulture.Name))
                    {
                        cultures.Add(resourceCulture);
                    }
                }
                catch (ArgumentException)
                {
                    // Unknown/invalid culture name — skip.
                }
            }

            return cultures
                .OrderBy(c => c.Name == "en" ? string.Empty : c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// True when the build produced satellite resources for <paramref name="culture"/>.
        /// Checked on disk because <see cref="System.Resources.ResourceManager.GetResourceSet"/> reports
        /// null until a set is loaded, which made every culture look untranslated.
        /// </summary>
        private static bool HasSatelliteResources(CultureInfo culture)
        {
            string satellite = string.Concat(typeof(Resources).Assembly.GetName().Name, ".resources.dll");
            string path = Path.Combine(AppContext.BaseDirectory, culture.Name, satellite);
            return File.Exists(path);
        }

        /// <summary>
        /// The culture whose satellite assembly should be listed for <paramref name="culture"/>:
        /// the culture itself when it ships its own resources, otherwise its neutral parent.
        /// </summary>
        private static CultureInfo? ResolveResourceCulture(CultureInfo culture)
        {
            if (string.IsNullOrEmpty(culture.Name))
            {
                return null;
            }

            if (HasSatelliteResources(culture))
            {
                return culture;
            }

            if (!culture.IsNeutralCulture && culture.Parent.Name.Length > 0 && HasSatelliteResources(culture.Parent))
            {
                return culture.Parent;
            }

            // Single-file/mixed layouts may still expose the set through the ResourceManager; only
            // neutral cultures are asked, because a regional culture resolves to its parent's set.
            if (culture.IsNeutralCulture &&
                Resources.ResourceManager.GetResourceSet(culture, createIfNotExists: false, tryParents: false) != null)
            {
                return culture;
            }

            return null;
        }

        /// <summary>Human-readable name for a language entry, shown in its own language.</summary>
        public static string GetDisplayName(CultureInfo culture)
        {
            string name = string.IsNullOrWhiteSpace(culture.NativeName) ? culture.DisplayName : culture.NativeName;
            if (string.IsNullOrEmpty(name))
            {
                return culture.Name;
            }

            // Native names are lowercase in several cultures ("русский"); capitalize for menus.
            return char.ToUpper(name[0], culture) + name[1..];
        }
    }
}
