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

using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace XerahS.Common
{
    public class DPAPIEncryptedStringValueProvider : IValueProvider
    {
        private const string EncryptedTag = "$DPAPIEncrypted$";

        private readonly PropertyInfo targetProperty;

        public DPAPIEncryptedStringValueProvider(PropertyInfo targetProperty)
        {
            this.targetProperty = targetProperty;
        }

        public object GetValue(object target)
        {
            string value = (string)targetProperty.GetValue(target);

            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    value = EncryptedTag + DPAPI.Encrypt(value);
                }
                catch
                {
                }
            }

            return value;
        }

        public void SetValue(object target, object value)
        {
            string text = (string)value;

            if (!string.IsNullOrEmpty(text) && text.StartsWith(EncryptedTag))
            {
                try
                {
                    string encryptedString = text.Substring(EncryptedTag.Length);
                    text = DPAPI.Decrypt(encryptedString);
                }
                catch
                {
                    text = null;
                }
            }

            targetProperty.SetValue(target, text);
        }
    }
}
