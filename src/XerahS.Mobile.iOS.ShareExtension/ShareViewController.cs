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

using Foundation;
using Social;
using UIKit;
using UniformTypeIdentifiers;

namespace XerahS.Mobile.iOS.ShareExtension;

[Register("ShareViewController")]
public class ShareViewController : SLComposeServiceViewController
{
    private const string AppGroupId = "group.com.sharexteam.xerahs";
    private const string SharedFolderName = "SharedFiles";
    private const string AppUrlScheme = "xerahs://share";

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "XerahS";
    }

    public override bool IsContentValid()
    {
        return true;
    }

    public override void DidSelectPost()
    {
        ProcessAttachmentsAsync();
    }

    private async void ProcessAttachmentsAsync()
    {
        var sharedFolder = GetSharedFolder();
        if (sharedFolder == null)
        {
            CompleteRequest();
            return;
        }

        var inputItems = ExtensionContext?.InputItems;
        if (inputItems == null || inputItems.Length == 0)
        {
            CompleteRequest();
            return;
        }

        var filesCopied = false;

        foreach (var inputItem in inputItems)
        {
            var attachments = inputItem.Attachments;
            if (attachments == null) continue;

            foreach (var attachment in attachments)
            {
                var copied = await ProcessAttachmentAsync(attachment, sharedFolder);
                if (copied) filesCopied = true;
            }
        }

        if (filesCopied)
        {
            OpenMainApp();
        }

        CompleteRequest();
    }

    private async Task<bool> ProcessAttachmentAsync(NSItemProvider attachment, string sharedFolder)
    {
        // Try image types first, then files, then URLs
        string[] typeIdentifiers =
        [
            UTTypes.Image.Identifier,
            UTTypes.Movie.Identifier,
            UTTypes.Data.Identifier,
            UTTypes.Url.Identifier,
            UTTypes.Text.Identifier
        ];

        foreach (var typeId in typeIdentifiers)
        {
            if (!attachment.HasItemConformingTo(typeId)) continue;

            try
            {
                var result = await attachment.LoadItemAsync(typeId, null);
                if (result == null) continue;

                if (result is NSUrl url && url.IsFileUrl)
                {
                    var fileName = url.LastPathComponent ?? $"share_{Guid.NewGuid():N}";
                    var destPath = Path.Combine(sharedFolder, fileName);
                    EnsureUniqueFileName(ref destPath);

                    NSFileManager.DefaultManager.Copy(url, NSUrl.FromFilename(destPath), out _);
                    return true;
                }

                if (result is NSData data)
                {
                    var ext = GetExtensionForType(typeId);
                    var fileName = $"share_{Guid.NewGuid():N}{ext}";
                    var destPath = Path.Combine(sharedFolder, fileName);

                    data.Save(NSUrl.FromFilename(destPath), atomically: true);
                    return true;
                }

                if (result is NSString text)
                {
                    var fileName = $"share_{Guid.NewGuid():N}.txt";
                    var destPath = Path.Combine(sharedFolder, fileName);

                    File.WriteAllText(destPath, text.ToString());
                    return true;
                }
            }
            catch
            {
                // Continue to next type identifier
            }
        }

        return false;
    }

    private string? GetSharedFolder()
    {
        var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
        if (containerUrl?.Path == null) return null;

        var sharedFolder = Path.Combine(containerUrl.Path, SharedFolderName);

        if (!Directory.Exists(sharedFolder))
        {
            Directory.CreateDirectory(sharedFolder);
        }

        return sharedFolder;
    }

    private void OpenMainApp()
    {
        var url = new NSUrl(AppUrlScheme);

        // Use responder chain to open URL (extensions can't call UIApplication.SharedApplication.OpenUrl directly)
        var responder = this as UIResponder;
        while (responder != null)
        {
            if (responder is UIApplication app)
            {
                app.OpenUrl(url, new NSDictionary(), null);
                return;
            }

            responder = responder.NextResponder;
        }
    }

    private void CompleteRequest()
    {
        ExtensionContext?.CompleteRequest([], null);
    }

    private static void EnsureUniqueFileName(ref string filePath)
    {
        if (!File.Exists(filePath)) return;

        var dir = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        var counter = 1;

        while (File.Exists(filePath))
        {
            filePath = Path.Combine(dir, $"{name}_{counter}{ext}");
            counter++;
        }
    }

    private static string GetExtensionForType(string typeIdentifier)
    {
        if (typeIdentifier == UTTypes.Image.Identifier) return ".png";
        if (typeIdentifier == UTTypes.Movie.Identifier) return ".mp4";
        if (typeIdentifier == UTTypes.Text.Identifier) return ".txt";
        return ".bin";
    }

    public override SLComposeSheetConfigurationItem[] GetConfigurationItems()
    {
        return [];
    }
}
