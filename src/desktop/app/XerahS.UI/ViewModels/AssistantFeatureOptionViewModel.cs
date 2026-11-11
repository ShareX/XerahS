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

using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Assistant.Ai;
using XerahS.Core;

namespace XerahS.UI.ViewModels;

/// <summary>
/// One AI feature in the Assistant tab of Settings: a checkbox that switches it on or off, and a note when the
/// provider that is set up cannot run it.
/// </summary>
public sealed partial class AssistantFeatureOptionViewModel : ObservableObject
{
    private readonly AiFeatureInfo _feature;

    public AssistantFeatureOptionViewModel(AiFeatureInfo feature)
    {
        _feature = feature;
        RefreshAvailability();
    }

    public string Title => _feature.Title;
    public string Description => _feature.Description;
    public string SendsText => $"Sends: {_feature.Sends}";

    public bool IsEnabled
    {
        get => AiFeatures.IsEnabled(_feature.Id);
        set
        {
            if (AiFeatures.IsEnabled(_feature.Id) == value)
            {
                return;
            }

            AiFeatures.SetEnabled(_feature.Id, value);
            SettingsManager.SaveApplicationConfig();
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnavailable))]
    private string _unavailableText = string.Empty;

    public bool IsUnavailable => UnavailableText.Length > 0;

    /// <summary>Call after the provider or its key changes.</summary>
    public void RefreshAvailability()
    {
        AiConnection? connection = AiServices.Client.Connection;

        UnavailableText = connection == null
            ? "Needs an AI provider with an API key."
            : _feature.NeedsImages && !connection.SupportsImages
                ? $"{connection.ProviderName} cannot read images, so this feature is not available with it."
                : string.Empty;
    }
}
