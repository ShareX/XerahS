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
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XerahS.Platform.Linux.Capture;



internal static class PortalRequestExtensions
{
    public static async Task<(uint response, IDictionary<string, object> results)> WaitForResponseAsync(this IPortalRequest request)
    {
        var tcs = new TaskCompletionSource<(uint, IDictionary<string, object>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watch = await request.WatchResponseAsync(data => tcs.TrySetResult((data.response, data.results))).ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    public static bool TryGetResult<T>(this IDictionary<string, object> results, string key, out T? value)
    {
        value = default;
        if (!results.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        raw = UnwrapVariant(raw);

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        if (typeof(T) == typeof(string) && raw is ObjectPath path)
        {
            value = (T)(object)path.ToString();
            return true;
        }

        return false;
    }

    private static object UnwrapVariant(object value)
    {
        var current = value;
        while (current != null)
        {
            var type = current.GetType();
            var typeName = type.FullName;
            if (typeName != "Tmds.DBus.Protocol.Variant" &&
                typeName != "Tmds.DBus.Protocol.VariantValue" &&
                typeName != "Tmds.DBus.Variant")
            {
                break;
            }

            var valueProp = type.GetProperty("Value");
            var unwrapped = valueProp?.GetValue(current);
            if (unwrapped == null)
            {
                break;
            }

            current = unwrapped;
        }

        return current ?? value;
    }
}
