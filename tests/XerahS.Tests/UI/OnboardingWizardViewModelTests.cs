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

using NUnit.Framework;
using XerahS.Core;
using XerahS.UI.Onboarding;

namespace XerahS.Tests.UI;

[TestFixture]
[NonParallelizable]
public class OnboardingWizardViewModelTests
{
    private string _rootPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "XerahS.Tests", "OnboardingWizard", Guid.NewGuid().ToString("N"));
        var personalFolder = Path.Combine(_rootPath, "Personal");
        Directory.CreateDirectory(personalFolder);
        SettingsManager.PersonalFolder = personalFolder;
        SettingsManager.LoadAllSettings();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        SettingsManager.PersonalFolder = null!;
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [SetUp]
    public void SetUp()
    {
        SettingsManager.Settings.ThemeMode = AppThemeMode.System;
    }

    [Test]
    public void Steps_AreThemeThenWorkflowsHotkeysDestinations()
    {
        var wizard = new OnboardingWizardViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(wizard.StepCount, Is.EqualTo(4));
            Assert.That(wizard.IsThemeStep, Is.True);
            Assert.That(wizard.CurrentAdvice, Is.Null);
            Assert.That(wizard.Advice.Select(a => a.Title), Is.EqualTo(new[] { "Workflows", "Hotkeys", "Destinations" }));
        });
    }

    [Test]
    public void Ok_AdvancesThroughEveryStepThenCompletes()
    {
        var wizard = new OnboardingWizardViewModel();

        for (int i = 1; i < wizard.StepCount; i++)
        {
            wizard.OkCommand.Execute(null);
            Assert.That(wizard.CurrentAdvice, Is.SameAs(wizard.Advice[i - 1]));
            Assert.That(wizard.CompletionTask.IsCompleted, Is.False);
        }

        wizard.OkCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(wizard.CompletionTask.IsCompletedSuccessfully, Is.True);
            Assert.That(wizard.CompletionTask.Result.Completed, Is.True);
        });
    }

    [Test]
    public void Back_ReturnsToPreviousStep()
    {
        var wizard = new OnboardingWizardViewModel();

        wizard.OkCommand.Execute(null);
        wizard.OkCommand.Execute(null);
        wizard.BackCommand.Execute(null);

        Assert.That(wizard.CurrentAdvice, Is.SameAs(wizard.Advice[0]));
    }

    [Test]
    public void Cancel_ReportsNotCompleted()
    {
        var wizard = new OnboardingWizardViewModel();

        wizard.Cancel();

        Assert.That(wizard.CompletionTask.Result.Completed, Is.False);
    }

    [Test]
    public void ThemeChoice_WritesThemeModeToSettings()
    {
        var wizard = new OnboardingWizardViewModel();

        wizard.UseSystemTheme = false;
        wizard.SelectedTheme = wizard.ThemeOptions.Single(option => option.Mode == AppThemeMode.Light);
        Assert.That(SettingsManager.Settings.ThemeMode, Is.EqualTo(AppThemeMode.Light));

        wizard.SelectedTheme = wizard.ThemeOptions.Single(option => option.Mode == AppThemeMode.Dark);
        Assert.That(SettingsManager.Settings.ThemeMode, Is.EqualTo(AppThemeMode.Dark));

        wizard.UseSystemTheme = true;
        Assert.Multiple(() =>
        {
            Assert.That(SettingsManager.Settings.ThemeMode, Is.EqualTo(AppThemeMode.System));
            Assert.That(wizard.CanChooseTheme, Is.False);
        });
    }
}
