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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XerahS.UI.ViewModels;

public enum NavigationNodeKind
{
    Page,
    Action,
    Group
}

public partial class NavigationNode : ObservableObject
{
    private string _searchText;
    private readonly string? _searchKeywords;
    private string _text;

    public NavigationNode(string text, string? tag, string? glyph, NavigationNodeKind kind, string? searchText = null, string? localizationKey = null)
    {
        _text = text;
        Tag = tag;
        Glyph = glyph;
        Kind = kind;
        LocalizationKey = localizationKey;
        _searchKeywords = searchText;
        _searchText = string.Join(' ', text, searchText ?? string.Empty);
    }

    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    /// <summary>
    /// Resource key the caption was built from, so a language switch can re-read it in place
    /// instead of rebuilding the tree (which would move the selection).
    /// </summary>
    public string? LocalizationKey { get; }

    public string? Tag { get; }

    public string? Glyph { get; }

    public NavigationNodeKind Kind { get; }

    public ObservableCollection<NavigationNode> Children { get; } = new();

    public NavigationNode? Parent { get; private set; }

    public bool HasGlyph => !string.IsNullOrWhiteSpace(Glyph);

    public string SearchText
    {
        get => _searchText;
        private set => SetProperty(ref _searchText, value);
    }

    public void AddChild(NavigationNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public void ReplaceChildren(IEnumerable<NavigationNode> children)
    {
        Children.Clear();

        foreach (NavigationNode child in children)
        {
            AddChild(child);
        }
    }

    public void ExpandPath()
    {
        for (NavigationNode? current = this; current != null; current = current.Parent)
        {
            if (current.Children.Count > 0)
            {
                current.IsExpanded = true;
            }
        }
    }

    public void UpdateSearchText(string? searchText)
    {
        SearchText = string.Join(' ', Text, searchText ?? string.Empty);
    }

    public void AppendSearchText(string? extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
        {
            return;
        }

        SearchText = string.Join(' ', SearchText, extra);
    }

    /// <summary>Re-reads the localized caption for this node and its children.</summary>
    public void RefreshLocalization()
    {
        if (!string.IsNullOrEmpty(LocalizationKey))
        {
            Text = XerahS.Common.Resources.GetString(LocalizationKey);
            UpdateSearchText(_searchKeywords);
        }

        foreach (NavigationNode child in Children)
        {
            child.RefreshLocalization();
        }
    }

    /// <summary>
    /// Applies ShareX-style AND term filter. Returns whether this node remains visible.
    /// </summary>
    public bool ApplyFilter(string? query)
    {
        query ??= string.Empty;

        bool childMatches = false;

        foreach (NavigationNode child in Children)
        {
            childMatches |= child.ApplyFilter(query);
        }

        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool selfMatches = terms.Length == 0 ||
            terms.All(term => SearchText.Contains(term, StringComparison.CurrentCultureIgnoreCase));

        IsVisible = selfMatches || childMatches;

        if (!string.IsNullOrWhiteSpace(query) && childMatches)
        {
            IsExpanded = true;
        }

        return IsVisible;
    }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isVisible = true;
}
