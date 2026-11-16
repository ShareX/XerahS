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

using System.Text.RegularExpressions;
using XerahS.Assistant.Models;

namespace XerahS.Assistant.Services;

public sealed record AssistantPrivacyCheck(
    string ToolName,
    AssistantPrivacyScope Scope,
    int ItemCount = 1,
    string? Text = null,
    string? FilePath = null,
    bool IsKnownHistoryItem = true,
    bool IsLocalOcr = true,
    bool IsModelInferred = false,
    DateTimeOffset? FileModifiedAt = null,
    bool UserExplicitlyRequested = false);

public sealed record AssistantPrivacyDecision(
    bool Allowed,
    bool RequiresConfirmation,
    string? ConfirmationCopy,
    string? Reason)
{
    public static AssistantPrivacyDecision Allow() => new(true, false, null, null);

    public static AssistantPrivacyDecision Confirm(string copy) => new(true, true, copy, null);

    public static AssistantPrivacyDecision Block(string reason) => new(false, false, null, reason);
}

public sealed partial class AssistantPrivacyGuard
{
    [GeneratedRegex(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
    [GeneratedRegex(@"[A-Za-z]:\\|/[^/\s]+/|\\\\", RegexOptions.CultureInvariant)]
    private static partial Regex PathRegex();

    public AssistantPrivacyDecision Evaluate(AssistantPrivacyCheck check)
    {
        if (!AssistantToolSchema.AllowlistedTools.Contains(check.ToolName))
        {
            return AssistantPrivacyDecision.Block("Unknown assistant tool.");
        }

        if (check.Scope == AssistantPrivacyScope.Destructive)
        {
            return AssistantPrivacyDecision.Block("Destructive assistant actions are blocked.");
        }

        if ((check.ToolName is AssistantToolNames.FileReveal or AssistantToolNames.EditorOpenImage or AssistantToolNames.OcrRun or AssistantToolNames.UploadFile) &&
            !check.IsKnownHistoryItem)
        {
            return AssistantPrivacyDecision.Block("Assistant file actions are limited to known XerahS history items.");
        }

        if (check.ToolName == AssistantToolNames.FileReveal)
        {
            string fileName = SafeFileName(check.FilePath);
            return AssistantPrivacyDecision.Confirm($"Reveal file in folder? `{fileName}`");
        }

        if (check.ToolName == AssistantToolNames.EditorOpenImage)
        {
            string fileName = SafeFileName(check.FilePath);
            return AssistantPrivacyDecision.Confirm($"Open image in editor? `{fileName}`");
        }

        if (check.ToolName == AssistantToolNames.WorkflowRun)
        {
            return AssistantPrivacyDecision.Confirm("Run this configured XerahS workflow?");
        }

        if (check.ItemCount > 5)
        {
            return AssistantPrivacyDecision.Confirm($"Run {check.ToolName} on {check.ItemCount} items?");
        }

        if (check.FileModifiedAt.HasValue && check.FileModifiedAt.Value < DateTimeOffset.Now.AddDays(-30))
        {
            string fileName = SafeFileName(check.FilePath);
            return AssistantPrivacyDecision.Confirm($"Use older file? `{fileName}`");
        }

        if (check.ToolName == AssistantToolNames.UploadFile || check.Scope == AssistantPrivacyScope.ExternalShare)
        {
            string fileName = SafeFileName(check.FilePath);
            return AssistantPrivacyDecision.Confirm($"Upload `{fileName}` to the configured destination?");
        }

        if (check.Scope == AssistantPrivacyScope.CloudImage)
        {
            string fileName = SafeFileName(check.FilePath);
            return AssistantPrivacyDecision.Confirm($"Send image to the active provider for analysis? Image: `{fileName}`");
        }

        if (check.Scope == AssistantPrivacyScope.CloudText &&
            (check.Text?.Length > 500 || ContainsUrlOrPath(check.Text)))
        {
            return AssistantPrivacyDecision.Confirm("Send text that may contain URLs or paths to the active provider?");
        }

        if (check.ToolName == AssistantToolNames.ClipboardCopyText)
        {
            if (check.Text?.Length > 1000 || ContainsUrlOrPath(check.Text))
            {
                return AssistantPrivacyDecision.Confirm($"Copy '{Preview(check.Text)}' to clipboard?");
            }

            if (check.IsModelInferred && !check.UserExplicitlyRequested)
            {
                return AssistantPrivacyDecision.Confirm($"Copy '{Preview(check.Text)}' to clipboard?");
            }
        }

        if (check.ToolName == AssistantToolNames.OcrRun && !check.IsLocalOcr)
        {
            string fileName = SafeFileName(check.FilePath);
            return AssistantPrivacyDecision.Confirm($"Send image to the active provider for analysis? Image: `{fileName}`");
        }

        return AssistantPrivacyDecision.Allow();
    }

    private static bool ContainsUrlOrPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return UrlRegex().IsMatch(value) || PathRegex().IsMatch(value);
    }

    private static string Preview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string compact = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return compact.Length <= 80 ? compact : compact[..77] + "...";
    }

    private static string SafeFileName(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "file";
        }

        try
        {
            string normalizedPath = filePath.Trim().TrimEnd('/', '\\');
            string fileName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrWhiteSpace(fileName) || string.Equals(fileName, normalizedPath, StringComparison.Ordinal))
            {
                string[] segments = normalizedPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
                fileName = segments.Length > 0 ? segments[^1] : fileName;
            }

            return string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        }
        catch
        {
            return "file";
        }
    }
}
