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

namespace XerahS.Core
{
    public static class SupportedLanguageExtensions
    {
        /// <summary>
        /// Maps a configured language to a .NET culture code. Returns null for
        /// <see cref="SupportedLanguage.Automatic"/> (use the OS default culture).
        /// </summary>
        public static string? ToCultureCode(this SupportedLanguage language) => language switch
        {
            SupportedLanguage.English => "en",
            SupportedLanguage.German => "de",
            SupportedLanguage.Russian => "ru",
            SupportedLanguage.Spanish => "es",
            SupportedLanguage.SimplifiedChinese => "zh-CN",
            SupportedLanguage.TraditionalChinese => "zh-TW",
            SupportedLanguage.French => "fr",
            SupportedLanguage.Italian => "it",
            SupportedLanguage.Portuguese => "pt",
            SupportedLanguage.PortugueseBrazil => "pt-BR",
            SupportedLanguage.Turkish => "tr",
            SupportedLanguage.Polish => "pl",
            SupportedLanguage.Ukrainian => "uk",
            SupportedLanguage.Korean => "ko",
            SupportedLanguage.Japanese => "ja",
            SupportedLanguage.Arabic => "ar",
            SupportedLanguage.Dutch => "nl",
            SupportedLanguage.Hebrew => "he",
            SupportedLanguage.Hungarian => "hu",
            SupportedLanguage.Indonesian => "id",
            SupportedLanguage.MexicanSpanish => "es-MX",
            SupportedLanguage.Persian => "fa",
            SupportedLanguage.Romanian => "ro",
            SupportedLanguage.Vietnamese => "vi",
            _ => null
        };
    }
}
