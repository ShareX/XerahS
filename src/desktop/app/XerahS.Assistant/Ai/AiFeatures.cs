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

using XerahS.Core;

namespace XerahS.Assistant.Ai;

/// <param name="Id">Saved in the settings; do not change it once released.</param>
/// <param name="Sends">What leaves this computer when the feature runs, in words for the user.</param>
/// <param name="NeedsImages">The feature needs a provider whose model reads images.</param>
public sealed record AiFeatureInfo(string Id, string Title, string Description, string Sends, bool NeedsImages);

/// <summary>
/// Every place XerahS uses the AI provider. The Assistant tab in Settings lists these with a checkbox each, and a
/// feature checks <see cref="IsUsable"/> before it offers itself. To add an AI feature: add an entry here, build the
/// feature on <see cref="IAiClient"/>, and gate it with <see cref="IsUsable"/>.
/// </summary>
public static class AiFeatures
{
    public const string Ocr = "ocr";
    public const string PrivacyScan = "privacy-scan";
    public const string RedactImage = "redact-image";
    public const string TextTools = "text-tools";
    public const string DescribeImage = "describe-image";
    public const string OverlayRequests = "overlay-requests";

    public static IReadOnlyList<AiFeatureInfo> All { get; } =
    [
        new(Ocr, "OCR: AI Assistant engine",
            "Adds AI Assistant to the engine list in the OCR window. It reads languages and handwriting the built-in engines cannot.",
            "The image, each time you run OCR with this engine.", NeedsImages: true),
        new(PrivacyScan, "Privacy mode: find sensitive information",
            "Before an upload in Privacy mode, finds names, emails, addresses, card numbers, passwords and keys to redact. You are asked before each scan.",
            "The unredacted screenshot, after you allow that scan.", NeedsImages: true),
        new(RedactImage, "Redact sensitive information: find what to hide",
            "When you choose Redact sensitive information on an image, finds what to blur or mask before you save a redacted copy. You are asked before each scan. Switched off, the tool still opens with local detection and manual review.",
            "The unredacted image, after you allow that scan.", NeedsImages: true),
        new(TextTools, "OCR window: translate, summarise and clean up text",
            "Adds an AI menu to the OCR window that works on the recognised text.",
            "The recognised text, when you choose an action.", NeedsImages: false),
        new(DescribeImage, "OCR window: describe the image",
            "Adds Describe image to the OCR window's AI menu. It writes a short description you can use as alt text.",
            "The image, when you choose the action.", NeedsImages: true),
        new(OverlayRequests, "Assistant overlay: understand free-form requests",
            "Lets the overlay work out what you mean when a request does not match a built-in command.",
            "The request you typed.", NeedsImages: false)
    ];

    public static AiFeatureInfo? Find(string id) =>
        All.FirstOrDefault(feature => feature.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether the user has left the feature switched on. Features are on until switched off, so a new one works as
    /// soon as a provider is set up; none of them sends anything without the user starting it.
    /// </summary>
    public static bool IsEnabled(string id)
    {
        try
        {
            return !SettingsManager.Settings.AssistantDisabledFeatures.Contains(id, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Settings are not loaded (a headless host or a test).
            return true;
        }
    }

    public static void SetEnabled(string id, bool enabled)
    {
        List<string> disabled = SettingsManager.Settings.AssistantDisabledFeatures;
        disabled.RemoveAll(item => item.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (!enabled)
        {
            disabled.Add(id);
        }
    }

    /// <summary>
    /// Whether the feature can run now: switched on, a provider set up, and a model that reads images if it needs one.
    /// </summary>
    public static bool IsUsable(string id, IAiClient? client = null)
    {
        if (!IsEnabled(id))
        {
            return false;
        }

        AiConnection? connection = (client ?? AiServices.Client).Connection;
        return connection != null && (connection.SupportsImages || Find(id)?.NeedsImages != true);
    }
}
