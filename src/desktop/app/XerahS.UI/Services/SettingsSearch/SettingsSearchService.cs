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

using Avalonia.Controls;
using System.Collections.Immutable;
using XerahS.Common;

namespace XerahS.UI.Services.SettingsSearch;

/// <summary>
/// Process-wide settings search index. Catalog is always available; visual-tree
/// entries are merged after prewarm. Search never walks the UI tree.
/// </summary>
public sealed class SettingsSearchService
{
    public const int DefaultMaxResults = 25;

    private static readonly SettingsSearchService InstanceField = new();

    private readonly Lock _gate = new();
    private readonly IReadOnlyList<SettingsSearchEntry> _catalogEntries = SettingsSearchCatalog.CreateEntries();
    private ImmutableArray<SettingsSearchEntry> _snapshot;
    private bool _applicationIndexed;
    private bool _destinationIndexed;

    private SettingsSearchService()
    {
        _snapshot = _catalogEntries.ToImmutableArray();
    }

    public static SettingsSearchService Instance => InstanceField;

    public bool IsApplicationIndexed
    {
        get { lock (_gate) { return _applicationIndexed; } }
    }

    public bool IsDestinationIndexed
    {
        get { lock (_gate) { return _destinationIndexed; } }
    }

    public bool IsFullyIndexed
    {
        get { lock (_gate) { return _applicationIndexed && _destinationIndexed; } }
    }

    public int EntryCount
    {
        get { lock (_gate) { return _snapshot.Length; } }
    }

    public void EnsureCatalogOnly()
    {
        // Snapshot already starts as catalog; method exists for explicit hub warm-up.
        lock (_gate)
        {
            if (_snapshot.IsDefaultOrEmpty)
            {
                _snapshot = _catalogEntries.ToImmutableArray();
            }
        }
    }

    public void MergeApplicationIndex(Control applicationSettingsRoot)
    {
        try
        {
            IReadOnlyList<SettingsSearchEntry> visual = SettingsVisualTreeIndexer.IndexApplicationSettings(applicationSettingsRoot);
            MergeVisual(visual, application: true, destination: false);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "SettingsSearch: application visual index failed");
        }
    }

    public void MergeDestinationIndex(
        Control destinationSettingsRoot,
        IEnumerable<(string CategoryName, IEnumerable<string> InstanceNames)> categories)
    {
        try
        {
            IReadOnlyList<SettingsSearchEntry> visual = SettingsVisualTreeIndexer.IndexDestinationSettings(
                destinationSettingsRoot,
                categories);
            MergeVisual(visual, application: false, destination: true);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "SettingsSearch: destination visual index failed");
        }
    }

    public IReadOnlyList<SettingsSearchEntry> GetAllEntries()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public IReadOnlyList<SettingsSearchEntry> Search(string? query, int maxResults = DefaultMaxResults)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Array.Empty<SettingsSearchEntry>();
        }

        ImmutableArray<SettingsSearchEntry> snapshot;
        lock (_gate)
        {
            snapshot = _snapshot;
        }

        string needle = trimmed.ToLowerInvariant();
        string[] tokens = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return Array.Empty<SettingsSearchEntry>();
        }

        List<(SettingsSearchEntry Entry, int Score)> scored = new(Math.Min(snapshot.Length, maxResults * 2));

        foreach (SettingsSearchEntry entry in snapshot)
        {
            int score = Score(entry, needle, tokens);
            if (score <= 0)
            {
                continue;
            }

            scored.Add((entry, score));
        }

        if (scored.Count == 0)
        {
            return Array.Empty<SettingsSearchEntry>();
        }

        scored.Sort(static (left, right) =>
        {
            int byScore = right.Score.CompareTo(left.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            int bySource = left.Entry.Source.CompareTo(right.Entry.Source);
            if (bySource != 0)
            {
                return bySource;
            }

            return string.Compare(left.Entry.Title, right.Entry.Title, StringComparison.OrdinalIgnoreCase);
        });

        if (scored.Count > maxResults)
        {
            scored.RemoveRange(maxResults, scored.Count - maxResults);
        }

        return scored.Select(static item => item.Entry).ToList();
    }

    private void MergeVisual(IReadOnlyList<SettingsSearchEntry> visual, bool application, bool destination)
    {
        lock (_gate)
        {
            Dictionary<string, SettingsSearchEntry> map = new(StringComparer.OrdinalIgnoreCase);

            foreach (SettingsSearchEntry entry in _catalogEntries)
            {
                map[entry.Id] = entry;
            }

            foreach (SettingsSearchEntry entry in _snapshot)
            {
                if (entry.Source == SettingsSearchSource.VisualTree)
                {
                    bool keepApp = _applicationIndexed && entry.Area == SettingsSearchArea.Application && !application;
                    bool keepDest = _destinationIndexed && entry.Area == SettingsSearchArea.Destination && !destination;
                    if (keepApp || keepDest)
                    {
                        map[entry.Id] = entry;
                    }
                }
            }

            foreach (SettingsSearchEntry entry in visual)
            {
                map[entry.Id] = entry;
            }

            _snapshot = map.Values.ToImmutableArray();
            if (application)
            {
                _applicationIndexed = true;
            }

            if (destination)
            {
                _destinationIndexed = true;
            }
        }
    }

    private static int Score(SettingsSearchEntry entry, string needle, string[] tokens)
    {
        string blob = entry.SearchBlob;
        if (blob.Length == 0)
        {
            return 0;
        }

        int score = 0;
        string title = entry.Title.ToLowerInvariant();

        if (title == needle)
        {
            score += 200;
        }
        else if (title.StartsWith(needle, StringComparison.Ordinal))
        {
            score += 120;
        }
        else if (title.Contains(needle, StringComparison.Ordinal))
        {
            score += 80;
        }

        if (blob.Contains(needle, StringComparison.Ordinal))
        {
            score += 40;
        }

        int tokenHits = 0;
        foreach (string token in tokens)
        {
            if (blob.Contains(token, StringComparison.Ordinal))
            {
                tokenHits++;
            }
        }

        if (tokenHits == 0 && score == 0)
        {
            return 0;
        }

        if (tokenHits == tokens.Length)
        {
            score += 30 + (tokenHits * 8);
        }
        else if (tokenHits > 0)
        {
            score += tokenHits * 5;
        }

        if (entry.Source == SettingsSearchSource.Catalog)
        {
            score += 10;
        }

        return score;
    }
}
