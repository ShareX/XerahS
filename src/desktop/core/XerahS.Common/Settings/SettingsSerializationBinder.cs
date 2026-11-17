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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace XerahS.Common
{
    /// <summary>
    /// Serialization binder that constrains Newtonsoft.Json polymorphic type resolution ("$type")
    /// for settings payloads used with <see cref="TypeNameHandling.Auto"/>.
    /// <para>
    /// Trust model: settings files (ApplicationConfig.json, UploadersConfig.json,
    /// WorkflowsConfig.json, backup archives, imported ShareX configs) and any JSON deep-copied
    /// in memory are untrusted input. Without a binder, "$type" tokens let an attacker
    /// instantiate arbitrary CLR types during deserialization (CWE-502). This binder restricts
    /// resolution to types defined in already-loaded product assemblies only (simple assembly
    /// name "ShareX" or "XerahS", or starting with "ShareX." / "XerahS."). Test harness assemblies
    /// (simple name ending with ".Tests") are excluded even though they share the product
    /// prefix, so settings payloads can never reach test-only types.
    /// </para>
    /// <para>
    /// The assembly name declared in the JSON payload is deliberately ignored during resolution
    /// (it is only reported in error messages): legacy ShareX configs carry historical assembly
    /// names such as "ShareX.UploadersLib", and attacker-controlled assembly names must not
    /// steer type loading. Type names are matched by <see cref="Type.FullName"/> or, as a
    /// fallback for short-name formats, by <see cref="Type.Name"/> (ordinal comparison).
    /// </para>
    /// <para>
    /// The binder fails closed: unknown or ambiguous type names throw
    /// <see cref="JsonSerializationException"/> instead of falling back to reflection-based
    /// resolution.
    /// </para>
    /// </summary>
    public sealed class SettingsSerializationBinder : ISerializationBinder
    {
        private const string ShareXAssemblyName = "ShareX";
        private const string ShareXAssemblyPrefix = "ShareX.";
        private const string XerahSAssemblyName = "XerahS";
        private const string XerahSAssemblyPrefix = "XerahS.";
        private const string TestAssemblySuffix = ".Tests";

        /// <summary>
        /// Shared stateless instance; safe to assign to any number of serializers.
        /// </summary>
        public static SettingsSerializationBinder Shared { get; } = new();

        private SettingsSerializationBinder()
        {
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = serializedType.Assembly.GetName().Name;
            typeName = serializedType.FullName;
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new JsonSerializationException(
                    $"Blocked or unknown settings type: empty type name (declared assembly: '{assemblyName ?? "(none)"}').");
            }

            // The assembly name declared in the JSON payload is intentionally NOT used for
            // resolution; it is only included in error text. Resolution is driven solely by
            // the type name matched against already-loaded product assemblies.
            List<Type>? fullNameMatches = null;
            List<Type>? shortNameMatches = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? simpleName = assembly.GetName().Name;
                if (!IsAllowedAssemblyName(simpleName))
                {
                    continue;
                }

                foreach (Type type in GetTypesSafe(assembly))
                {
                    if (string.Equals(type.FullName, typeName, StringComparison.Ordinal))
                    {
                        (fullNameMatches ??= new List<Type>()).Add(type);
                    }
                    else if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    {
                        (shortNameMatches ??= new List<Type>()).Add(type);
                    }
                }
            }

            if (fullNameMatches != null)
            {
                if (fullNameMatches.Count > 1)
                {
                    throw new JsonSerializationException(
                        $"Blocked or unknown settings type: '{typeName}' is ambiguous across allowed assemblies " +
                        $"(declared assembly: '{assemblyName ?? "(none)"}').");
                }

                return fullNameMatches[0];
            }

            if (shortNameMatches != null)
            {
                if (shortNameMatches.Count > 1)
                {
                    throw new JsonSerializationException(
                        $"Blocked or unknown settings type: '{typeName}' is ambiguous across allowed assemblies " +
                        $"(declared assembly: '{assemblyName ?? "(none)"}').");
                }

                return shortNameMatches[0];
            }

            throw new JsonSerializationException(
                $"Blocked or unknown settings type: '{typeName}' (declared assembly: '{assemblyName ?? "(none)"}') " +
                "was not found in the allowed ShareX/XerahS assemblies.");
        }

        private static bool IsAllowedAssemblyName(string? simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
            {
                return false;
            }

            // Test assemblies share the product prefix but must never be instantiable from
            // settings payloads.
            if (simpleName.EndsWith(TestAssemblySuffix, StringComparison.Ordinal))
            {
                return false;
            }

            return simpleName == ShareXAssemblyName
                || simpleName == XerahSAssemblyName
                || simpleName.StartsWith(ShareXAssemblyPrefix, StringComparison.Ordinal)
                || simpleName.StartsWith(XerahSAssemblyPrefix, StringComparison.Ordinal);
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types?.OfType<Type>() ?? [];
            }
        }
    }
}
