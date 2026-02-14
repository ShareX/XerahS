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
using System.Text;
using UIKit;
using UniformTypeIdentifiers;

namespace XerahS.Mobile.iOS.ShareExtension;

[Register("ShareViewController")]
public class ShareViewController : SLComposeServiceViewController
{
    private const string SharedPayloadKey = "xerahs_shared_payload";
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
        var inputItems = ExtensionContext?.InputItems;
        if (inputItems == null || inputItems.Length == 0)
        {
            CompleteRequest();
            return;
        }

        var payload = new List<SharedFilePayload>();

        foreach (var inputItem in inputItems)
        {
            var attachments = inputItem.Attachments;
            if (attachments == null) continue;

            foreach (var attachment in attachments)
            {
                var item = await ProcessAttachmentAsync(attachment);
                if (item != null)
                {
                    payload.Add(item);
                }
            }
        }

        if (payload.Count > 0)
        {
            SavePayloadToPasteboard(payload);
            OpenMainApp();
        }

        CompleteRequest();
    }

    private async Task<SharedFilePayload?> ProcessAttachmentAsync(NSItemProvider attachment)
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
                    var urlData = NSData.FromUrl(url);
                    if (urlData == null) continue;

                    return new SharedFilePayload
                    {
                        FileName = fileName,
                        Base64Content = urlData.GetBase64EncodedString(NSDataBase64EncodingOptions.None)
                    };
                }

                if (result is NSData data)
                {
                    var ext = GetExtensionForType(typeId);
                    var fileName = $"share_{Guid.NewGuid():N}{ext}";
                    return new SharedFilePayload
                    {
                        FileName = fileName,
                        Base64Content = data.GetBase64EncodedString(NSDataBase64EncodingOptions.None)
                    };
                }

                if (result is NSString text)
                {
                    var fileName = $"share_{Guid.NewGuid():N}.txt";
                    var bytes = Encoding.UTF8.GetBytes(text.ToString());
                    var textData = NSData.FromArray(bytes);
                    return new SharedFilePayload
                    {
                        FileName = fileName,
                        Base64Content = textData.GetBase64EncodedString(NSDataBase64EncodingOptions.None)
                    };
                }
            }
            catch
            {
                // Continue to next type identifier
            }
        }

        return null;
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

    private static void SavePayloadToPasteboard(List<SharedFilePayload> payload)
    {
        var lines = payload
            .Select(x => $"{Uri.EscapeDataString(x.FileName)}|{x.Base64Content}");
        var encoded = string.Join("\n", lines);
        var pasteboard = UIPasteboard.FromName(GetSharedPasteboardName(), create: true) ?? UIPasteboard.General;
        pasteboard.String = $"{SharedPayloadKey}:{encoded}";
    }

    private static string GetSharedPasteboardName()
    {
        const string shareExtensionSuffix = ".share-extension";
        const string shareExtensionAltSuffix = ".shareextension";

        var bundleId = NSBundle.MainBundle.BundleIdentifier ?? "com.sharexteam.xerahs.share-extension";
        var appBundleId = bundleId;

        if (bundleId.EndsWith(shareExtensionSuffix, StringComparison.Ordinal))
        {
            appBundleId = bundleId[..^shareExtensionSuffix.Length];
        }
        else if (bundleId.EndsWith(shareExtensionAltSuffix, StringComparison.Ordinal))
        {
            appBundleId = bundleId[..^shareExtensionAltSuffix.Length];
        }

        return $"{appBundleId}.shared";
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

internal class SharedFilePayload
{
    public string FileName { get; set; } = string.Empty;
    public string Base64Content { get; set; } = string.Empty;
}
