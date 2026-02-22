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

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
        #region Upload / File Naming

        public string NameFormatPattern
        {
            get => _settings.UploadSettings.NameFormatPattern;
            set
            {
                if (_settings.UploadSettings.NameFormatPattern != value)
                {
                    _settings.UploadSettings.NameFormatPattern = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NameFormatPatternActiveWindow
        {
            get => _settings.UploadSettings.NameFormatPatternActiveWindow;
            set
            {
                if (_settings.UploadSettings.NameFormatPatternActiveWindow != value)
                {
                    _settings.UploadSettings.NameFormatPatternActiveWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FileUploadUseNamePattern
        {
            get => _settings.UploadSettings.FileUploadUseNamePattern;
            set
            {
                if (_settings.UploadSettings.FileUploadUseNamePattern != value)
                {
                    _settings.UploadSettings.FileUploadUseNamePattern = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FileUploadReplaceProblematicCharacters
        {
            get => _settings.UploadSettings.FileUploadReplaceProblematicCharacters;
            set
            {
                if (_settings.UploadSettings.FileUploadReplaceProblematicCharacters != value)
                {
                    _settings.UploadSettings.FileUploadReplaceProblematicCharacters = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool URLRegexReplace
        {
            get => _settings.UploadSettings.URLRegexReplace;
            set
            {
                if (_settings.UploadSettings.URLRegexReplace != value)
                {
                    _settings.UploadSettings.URLRegexReplace = value;
                    OnPropertyChanged();
                }
            }
        }

        public string URLRegexReplacePattern
        {
            get => _settings.UploadSettings.URLRegexReplacePattern;
            set
            {
                if (_settings.UploadSettings.URLRegexReplacePattern != value)
                {
                    _settings.UploadSettings.URLRegexReplacePattern = value;
                    OnPropertyChanged();
                }
            }
        }

        public string URLRegexReplaceReplacement
        {
            get => _settings.UploadSettings.URLRegexReplaceReplacement;
            set
            {
                if (_settings.UploadSettings.URLRegexReplaceReplacement != value)
                {
                    _settings.UploadSettings.URLRegexReplaceReplacement = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ClipboardUploadURLContents
        {
            get => _settings.UploadSettings.ClipboardUploadURLContents;
            set
            {
                if (_settings.UploadSettings.ClipboardUploadURLContents != value)
                {
                    _settings.UploadSettings.ClipboardUploadURLContents = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ClipboardUploadShortenURL
        {
            get => _settings.UploadSettings.ClipboardUploadShortenURL;
            set
            {
                if (_settings.UploadSettings.ClipboardUploadShortenURL != value)
                {
                    _settings.UploadSettings.ClipboardUploadShortenURL = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion
    }
}
