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

using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace ShareX.Auto.Plugin;

public sealed class AutoUploader : GenericUploader
{
    private readonly UploaderCategory _category;

    public AutoUploader(UploaderCategory category)
    {
        _category = category;
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        var instanceManager = InstanceManager.Instance;
        var targetInstance = instanceManager.ResolveAutoInstance(_category);

        if (targetInstance == null)
        {
            return CreateErrorResult($"Auto destination could not resolve a default uploader for category {_category}.");
        }

        var provider = ProviderCatalog.GetProvider(targetInstance.ProviderId);
        if (provider == null)
        {
            return CreateErrorResult($"Provider not found for auto-resolved instance: {targetInstance.ProviderId}.");
        }

        Uploader uploader;
        try
        {
            uploader = provider.CreateInstance(targetInstance.SettingsJson);
        }
        catch (Exception ex)
        {
            return CreateErrorResult(ex.Message);
        }

        Uploader.ProgressEventHandler progressHandler = progress => OnProgressChanged(progress);
        uploader.ProgressChanged += progressHandler;

        try
        {
            if (uploader is GenericUploader genericUploader)
            {
                return genericUploader.Upload(stream, fileName);
            }

            return CreateErrorResult("Resolved uploader does not support generic uploads.");
        }
        finally
        {
            uploader.ProgressChanged -= progressHandler;
        }
    }

    private static UploadResult CreateErrorResult(string message)
    {
        var result = new UploadResult
        {
            IsSuccess = false,
            Response = message
        };

        result.Errors.Add(message);
        return result;
    }
}
