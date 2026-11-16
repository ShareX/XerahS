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

namespace XerahS.Assistant.Routing;

public enum AssistantDeterministicIntentKind
{
    Unknown,
    LatestScreenshotPaths,
    CopyLatestScreenshotPath,
    OpenLatestScreenshot,
    RevealLatestScreenshot,
    OcrLatestScreenshot,
    CopyOcrLatestScreenshot,
    UploadLatestScreenshot,
    SearchScreenshotText,
    RunWorkflow
}

public sealed record AssistantDeterministicIntent(
    AssistantDeterministicIntentKind Kind,
    int Limit,
    bool CopyRequested,
    string? Separator = null,
    string? Argument = null);

public sealed partial class AssistantCommandRouter
{
    [GeneratedRegex(@"\b(?:separated|joined)\s+by\s+(?:(?:a|an)\s+)?(?<separator>""[^""]*""|'[^']*'|[^\s,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExtractRequestedSeparatorRegex();

    public const int MaxLatestScreenshotLimit = 10;

    [GeneratedRegex(@"\b(?:give\s+me\s+)?(?:the\s+)?(?:local\s+)?(?:(?:file\s+)?paths?|filepath(?:s)?)\s+(?:of\s+)?(?:my\s+)?(?:last|latest)\s+(?<limit>\d{1,2})\s+screenshot(?:s)?\b|\b(?:last|latest)\s+(?<limit2>\d{1,2})\s+screenshot(?:s)?\s+(?:(?:local\s+)?(?:file\s+path(?:s)?|filepath(?:s)?)|paths?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LastScreenshotPathsRegex();

    [GeneratedRegex(@"\b(?:latest|last|most\s+recent)\s+screenshot\b.*\b(?:path|filepath|file\s+path)\b|\b(?:path|filepath|file\s+path)\b.*\b(?:latest|last|most\s+recent)\s+screenshot\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LatestScreenshotPathRegex();

    [GeneratedRegex(@"\bcopy\b.*(?:\b(?:latest|last|most\s+recent)\s+screenshot\b.*\b(?:path|filepath|file\s+path)\b|\b(?:path|filepath|file\s+path)\b.*\b(?:latest|last|most\s+recent)\s+screenshot\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CopyLatestScreenshotPathRegex();

    [GeneratedRegex(@"\bopen\b.*\b(?:latest|last|most\s+recent)\s+screenshot\b.*\b(?:editor|edit|image\s+editor)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OpenLatestScreenshotRegex();

    [GeneratedRegex(@"\b(?:reveal|show)\b.*\b(?:latest|last|most\s+recent)\s+(?:capture|screenshot)\b.*\b(?:folder|explorer|finder|files)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RevealLatestScreenshotRegex();

    [GeneratedRegex(@"\b(?:ocr|extract\s+text|read\s+text)\b.*\b(?:latest|last|most\s+recent)\s+(?:capture|screenshot)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OcrLatestScreenshotRegex();

    [GeneratedRegex(@"\bcopy\b.*\b(?:ocr|extracted\s+text|text)\b.*\b(?:latest|last|most\s+recent)\s+(?:capture|screenshot)\b|\b(?:ocr|extract\s+text|read\s+text)\b.*\b(?:latest|last|most\s+recent)\s+(?:capture|screenshot)\b.*\bcopy\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CopyOcrLatestScreenshotRegex();

    [GeneratedRegex(@"\b(?:upload|share)\b.*\b(?:latest|last|most\s+recent)\s+(?:capture|screenshot)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UploadLatestScreenshotRegex();

    [GeneratedRegex(@"\b(?:find|search|show)\b.*\b(?:screenshots?|captures?)\b.*\b(?:containing|with|for|text)\b\s+(?<query>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SearchScreenshotTextRegex();

    [GeneratedRegex(@"\b(?:run|start)\s+(?:the\s+)?(?:workflow\s+)?(?<name>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RunWorkflowRegex();

    public AssistantDeterministicIntent Parse(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.Unknown, 0, CopyRequested: false);
        }

        if (CopyLatestScreenshotPathRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.CopyLatestScreenshotPath, 1, CopyRequested: true);
        }

        if (CopyOcrLatestScreenshotRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.CopyOcrLatestScreenshot, 1, CopyRequested: true);
        }

        if (OcrLatestScreenshotRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.OcrLatestScreenshot, 1, CopyRequested: false);
        }

        if (UploadLatestScreenshotRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.UploadLatestScreenshot, 1, CopyRequested: false);
        }

        var searchMatch = SearchScreenshotTextRegex().Match(prompt);
        if (searchMatch.Success)
        {
            string query = searchMatch.Groups["query"].Value.Trim().Trim('"', '\'');
            if (!string.IsNullOrWhiteSpace(query))
            {
                return new AssistantDeterministicIntent(
                    AssistantDeterministicIntentKind.SearchScreenshotText,
                    MaxLatestScreenshotLimit,
                    CopyRequested: prompt.Contains("copy", StringComparison.OrdinalIgnoreCase),
                    Argument: query);
            }
        }

        if (OpenLatestScreenshotRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.OpenLatestScreenshot, 1, CopyRequested: false);
        }

        if (RevealLatestScreenshotRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.RevealLatestScreenshot, 1, CopyRequested: false);
        }

        var lastMatch = LastScreenshotPathsRegex().Match(prompt);
        if (lastMatch.Success)
        {
            string limitText = lastMatch.Groups["limit"].Success
                ? lastMatch.Groups["limit"].Value
                : lastMatch.Groups["limit2"].Value;

            int limit = int.TryParse(limitText, out int parsed)
                ? Math.Clamp(parsed, 1, MaxLatestScreenshotLimit)
                : 5;

            return new AssistantDeterministicIntent(
                AssistantDeterministicIntentKind.LatestScreenshotPaths,
                limit,
                CopyRequested: prompt.Contains("copy", StringComparison.OrdinalIgnoreCase),
                Separator: ExtractRequestedSeparator(prompt));
        }

        if (LatestScreenshotPathRegex().IsMatch(prompt))
        {
            return new AssistantDeterministicIntent(
                AssistantDeterministicIntentKind.LatestScreenshotPaths,
                1,
                CopyRequested: prompt.Contains("copy", StringComparison.OrdinalIgnoreCase),
                Separator: ExtractRequestedSeparator(prompt));
        }

        var workflowMatch = RunWorkflowRegex().Match(prompt);
        if (workflowMatch.Success)
        {
            return new AssistantDeterministicIntent(
                AssistantDeterministicIntentKind.RunWorkflow,
                1,
                CopyRequested: false,
                Argument: workflowMatch.Groups["name"].Value.Trim());
        }

        return new AssistantDeterministicIntent(AssistantDeterministicIntentKind.Unknown, 0, CopyRequested: false);
    }

    private static string? ExtractRequestedSeparator(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var match = ExtractRequestedSeparatorRegex().Match(prompt);

        if (match.Success)
        {
            string rawValue = match.Groups["separator"].Value.Trim().Trim('"', '\'');
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                return NormalizeSeparator(rawValue);
            }
        }

        if (prompt.Contains("semicolon-separated", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("semicolon separated", StringComparison.OrdinalIgnoreCase))
        {
            return ";";
        }

        if (prompt.Contains("comma-separated", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("comma separated", StringComparison.OrdinalIgnoreCase))
        {
            return ",";
        }

        return null;
    }

    private static string NormalizeSeparator(string value)
    {
        string trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        string normalizedKeyword = trimmed
            .TrimEnd('.', '!', '?', ',', ':')
            .ToLowerInvariant();

        return normalizedKeyword switch
        {
            "semicolon" or "semicolons" => ";",
            "comma" or "commas" => ",",
            "pipe" or "pipes" => "|",
            "tab" or "tabs" => "\t",
            "newline" or "newlines" => Environment.NewLine,
            "new line" or "new lines" => Environment.NewLine,
            "linebreak" or "linebreaks" => Environment.NewLine,
            "line-break" or "line-breaks" => Environment.NewLine,
            _ => trimmed
        };
    }

    public static IReadOnlyList<string> GetSuggestions() =>
    [
        "Give me the local file path of my last 5 screenshots",
        "Copy the path of the latest screenshot",
        "Open the latest screenshot in the editor",
        "Reveal the latest capture in Explorer",
        "Copy OCR text from the latest screenshot",
        "Upload the latest screenshot",
        "Run workflow region capture"
    ];
}
