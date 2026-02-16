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

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XerahS.Common.Converters
{
    /// <summary>
    /// Serializes TimeZoneInfo as an ID string and supports legacy object payloads.
    /// </summary>
    public sealed class TimeZoneInfoJsonConverter : JsonConverter<TimeZoneInfo>
    {
        public override void WriteJson(JsonWriter writer, TimeZoneInfo? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Id);
        }

        public override TimeZoneInfo ReadJson(JsonReader reader, Type objectType, TimeZoneInfo? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return existingValue ?? TimeZoneInfo.Utc;
            }

            if (reader.TokenType == JsonToken.String && reader.Value is string timeZoneId)
            {
                return ResolveTimeZone(timeZoneId, existingValue);
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject timeZoneObject = JObject.Load(reader);
                string? idFromObject = GetPropertyValueCaseInsensitive(timeZoneObject, "Id");
                return ResolveTimeZone(idFromObject, existingValue);
            }

            return existingValue ?? TimeZoneInfo.Utc;
        }

        private static string? GetPropertyValueCaseInsensitive(JObject obj, string propertyName)
        {
            JProperty? property = obj.Properties().FirstOrDefault(x => x.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            return property?.Value.Type == JTokenType.String ? property.Value.Value<string>() : null;
        }

        private static TimeZoneInfo ResolveTimeZone(string? timeZoneId, TimeZoneInfo? fallback)
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (ArgumentException)
                {
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return fallback ?? TimeZoneInfo.Utc;
        }
    }
}
