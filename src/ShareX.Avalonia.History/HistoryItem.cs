#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using Newtonsoft.Json;
using System.Collections.Generic;

namespace XerahS.History
{
    public class HistoryItem
    {
        [JsonIgnore]
        public long Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
        public string ThumbnailURL { get; set; } = string.Empty;
        public string DeletionURL { get; set; } = string.Empty;
        public string ShortenedURL { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public string? TagsWindowTitle
        {
            get
            {
                if (Tags != null && Tags.TryGetValue("WindowTitle", out string value))
                {
                    return value;
                }

                return null;
            }
        }

        [JsonIgnore]
        public string? TagsProcessName
        {
            get
            {
                if (Tags != null && Tags.TryGetValue("ProcessName", out string value))
                {
                    return value;
                }

                return null;
            }
        }

        [JsonIgnore]
        public bool Favorite
        {
            get
            {
                return Tags != null && Tags.ContainsKey("Favorite");
            }
            set
            {
                if (Tags == null)
                {
                    Tags = new Dictionary<string, string>();
                }

                if (value)
                {
                    Tags["Favorite"] = null;
                }
                else
                {
                    Tags.Remove("Favorite");
                }
            }
        }

        [JsonIgnore]
        public string? Tag
        {
            get
            {
                if (Tags != null && Tags.TryGetValue("Tag", out string value))
                {
                    return value;
                }

                return null;
            }
            set
            {
                if (Tags == null)
                {
                    Tags = new Dictionary<string, string>();
                }

                if (!string.IsNullOrEmpty(value))
                {
                    Tags["Tag"] = value;
                }
                else
                {
                    Tags.Remove("Tag");
                }
            }
        }
    }
}

