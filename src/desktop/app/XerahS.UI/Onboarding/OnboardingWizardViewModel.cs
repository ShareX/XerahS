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
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Services;
using XerahS.UI.Theming;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Onboarding;

/// <summary>
/// First-run wizard: a theme choice followed by short pointers to where workflows,
/// hotkeys and destinations are configured. It does not configure any of those itself.
/// </summary>
public partial class OnboardingWizardViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<OnboardingResult> _completionSource = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThemeStep))]
    [NotifyPropertyChangedFor(nameof(CurrentAdvice))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(StepCounterText))]
    private int _currentStepIndex;

    public IReadOnlyList<OnboardingAdvice> Advice { get; } =
    [
        new(
            HostIcons.NavigationWorkflows,
            "Workflows",
            "Everything XerahS captures starts from a workflow.",
            "Sidebar → Workflows   (or menu: Workflows → Manage Workflows)",
            [
                "A workflow is one job, such as region capture, window capture or screen recording, plus what happens after it.",
                "Use Add New to create one, or select a workflow and click Edit to change it.",
                "Run any workflow from Workflows → Run Workflow, or pin it to the tray menu."
            ]),
        new(
            HostIcons.OnboardingHotkeys,
            "Hotkeys",
            "Hotkeys are set inside each workflow.",
            "Workflows → Edit a workflow → Task tab → Hotkey",
            [
                "Each workflow carries its own hotkey, so pick the workflow first, then its shortcut.",
                "Click the hotkey box and press the keys you want. Escape cancels.",
                "A workflow without a hotkey still works from the menu and the tray."
            ]),
        new(
            HostIcons.NavigationUpload,
            "Destinations",
            "Choose where your captures get uploaded.",
            "Sidebar → Settings → Destination Settings",
            [
                "Add the upload services you use and sign in or enter their keys there.",
                "Then pick where each workflow sends files: Edit a workflow → Task Settings → Upload → Destinations.",
                "Auto tries your configured uploaders in order until one succeeds."
            ])
    ];

    public IReadOnlyList<OnboardingThemeOption> ThemeOptions { get; } =
    [
        new(AppThemeMode.Dark, "Dark"),
        new(AppThemeMode.Light, "Light")
    ];

    public int StepCount => Advice.Count + 1;

    public bool IsThemeStep => CurrentStepIndex == 0;

    public OnboardingAdvice? CurrentAdvice => CurrentStepIndex > 0 ? Advice[CurrentStepIndex - 1] : null;

    public bool CanGoBack => CurrentStepIndex > 0;

    public string StepCounterText => $"{CurrentStepIndex + 1} of {StepCount}";

    public Task<OnboardingResult> CompletionTask => _completionSource.Task;

    public bool UseSystemTheme
    {
        get => SettingsManager.Settings.ThemeMode == AppThemeMode.System;
        set
        {
            if (value == UseSystemTheme)
            {
                return;
            }

            // Leaving system mode keeps whatever variant is on screen so the switch does not flash.
            SetThemeMode(value
                ? AppThemeMode.System
                : ThemeService.ShouldUseDarkMode(AppThemeMode.System) ? AppThemeMode.Dark : AppThemeMode.Light);
        }
    }

    public bool CanChooseTheme => !UseSystemTheme;

    public OnboardingThemeOption? SelectedTheme
    {
        get
        {
            AppThemeMode effective = ThemeService.ShouldUseDarkMode(SettingsManager.Settings.ThemeMode)
                ? AppThemeMode.Dark
                : AppThemeMode.Light;
            return ThemeOptions.FirstOrDefault(option => option.Mode == effective);
        }
        set
        {
            if (value != null && !UseSystemTheme && value.Mode != SettingsManager.Settings.ThemeMode)
            {
                SetThemeMode(value.Mode);
            }
        }
    }

    private void SetThemeMode(AppThemeMode mode)
    {
        SettingsManager.Settings.ThemeMode = mode;
        ThemeService.ApplyTheme(mode);
        OnPropertyChanged(nameof(UseSystemTheme));
        OnPropertyChanged(nameof(CanChooseTheme));
        OnPropertyChanged(nameof(SelectedTheme));
    }

    [RelayCommand]
    private void Ok()
    {
        if (CurrentStepIndex < StepCount - 1)
        {
            CurrentStepIndex++;
        }
        else
        {
            Complete();
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CanGoBack)
        {
            CurrentStepIndex--;
        }
    }

    private void Complete()
    {
        if (_completionSource.Task.IsCompleted)
        {
            return;
        }

        try
        {
            SettingsManager.Settings.MarkFirstTimeRunCompleted(persist: false);
            SettingsManager.SaveAllSettings();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[OnboardingWizard] Failed to save settings");
        }

        _completionSource.TrySetResult(new OnboardingResult { Completed = true });
    }

    /// <summary>
    /// Called when the wizard is closed without completing.
    /// </summary>
    public void Cancel()
    {
        _completionSource.TrySetResult(new OnboardingResult { Completed = false });
    }
}

public sealed record OnboardingAdvice(
    string Icon,
    string Title,
    string Subtitle,
    string Location,
    IReadOnlyList<string> Tips);

public sealed record OnboardingThemeOption(AppThemeMode Mode, string DisplayName);
