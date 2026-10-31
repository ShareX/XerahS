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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.UI.Views.Dialogs;

namespace XerahS.UI;

public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// Explicit VM→View maps for names that do not follow ViewModel→View,
    /// including ModalContent dialogs hosted via <c>ModalDialogHost</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, Func<Control>> KnownMappings =
        new Dictionary<Type, Func<Control>>
        {
            [typeof(DebugViewModel)] = static () => new DebugView(),
            [typeof(DestinationSettingsViewModel)] = static () => new DestinationSettingsView(),
            [typeof(HistoryViewModel)] = static () => new HistoryView(),
            [typeof(IndexFolderViewModel)] = static () => new IndexFolderPanel(),
            [typeof(ProviderCatalogViewModel)] = static () => new ProviderCatalogView(),
            [typeof(MediaBrowserViewModel)] = static () => new MediaBrowserView(),
            [typeof(SettingsViewModel)] = static () => new ApplicationSettingsView(),
            [typeof(TaskSettingsViewModel)] = static () => new TaskSettingsPanel(),
            [typeof(WorkflowEditorViewModel)] = static () => new WorkflowEditorView(),
            [typeof(WorkflowsViewModel)] = static () => new WorkflowsView(),

            // ModalDialogHost / ModalContent (non-conventional view names)
            [typeof(CustomUploaderEditorViewModel)] = static () => new CustomUploaderEditorDialog(),
            [typeof(PluginInstallerViewModel)] = static () => new PluginInstallerDialog(),
            [typeof(ImageEffectsViewModel)] = static () => new ImageEffectsBrowserDialog(),
            [typeof(FFmpegOptionsViewModel)] = static () => new FFmpegOptionsWindow(),
            [typeof(QrCodeGeneratorViewModel)] = static () => new QrCodeGeneratorDialog(),
            [typeof(ShareLogsViewModel)] = static () => new ShareLogsDialog(),
            [typeof(WatchFolderEditViewModel)] = static () => new WatchFolderDialog(),
            [typeof(OpenImageChoiceViewModel)] = static () => new OpenImageChoiceDialog(),
            [typeof(WindowSelectorViewModel)] = static () => new WindowSelectorDialog(),
            [typeof(UpdateMessageBoxViewModel)] = static () => new UpdateMessageBox(),
            [typeof(AfterCaptureViewModel)] = static () => new AfterCaptureWindow(),
            [typeof(SendToPromptViewModel)] = static () => new SendToPromptWindow(),
            [typeof(PrivacyReviewViewModel)] = static () => new PrivacyReviewView(),
            [typeof(SimplePromptViewModel)] = static () => new SimplePromptView(),
        };

    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        Type vmType = data.GetType();
        if (TryCreateControl(vmType, out Control? mapped) && mapped != null)
        {
            mapped.DataContext = data;
            return mapped;
        }

        NotifyDialogOpenFailure(vmType);
        return CreateDialogOpenFailureContent();
    }

    /// <summary>Operator-facing title when a dialog view cannot be resolved.</summary>
    internal const string DialogOpenFailureTitle = "Couldn't open this dialog.";

    /// <summary>Optional muted hint shown under <see cref="DialogOpenFailureTitle"/>.</summary>
    internal const string DialogOpenFailureHint = "Try again, or restart XerahS if it keeps happening.";

    private static void NotifyDialogOpenFailure(Type vmType)
    {
        var fullName = vmType.FullName ?? vmType.Name;
        DebugHelper.WriteLine($"[ViewLocator] Could not open dialog; view not found for {fullName}");

        try
        {
            if (!PlatformServices.IsToastServiceInitialized)
            {
                return;
            }

            PlatformServices.Toast.ShowToast(new ToastConfig
            {
                Title = DialogOpenFailureTitle,
                Text = DialogOpenFailureHint,
                Duration = 4f,
                Size = new SizeI(420, 120),
                AutoHide = true,
                LeftClickAction = ToastClickAction.CloseNotification,
                RightClickAction = ToastClickAction.CloseNotification,
                MiddleClickAction = ToastClickAction.CloseNotification
            });
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ViewLocator dialog-open-failure toast failed");
        }
    }

    private static Control CreateDialogOpenFailureContent()
    {
        // Plain-English fallback for ContentControl / ModalContent; never leak type names.
        var title = new TextBlock
        {
            Text = DialogOpenFailureTitle,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        var hint = new TextBlock
        {
            Text = DialogOpenFailureHint,
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            MinHeight = 44,
            MinWidth = 44,
            Margin = new Thickness(16),
            Children = { title, hint }
        };
    }

    public bool Match(object? data)
    {
        // Only claim ObservableObjects we can actually resolve. Application.DataTemplates
        // lists ViewLocator first; a blanket Match stole typed DataTemplates in App.axaml
        // and produced "Not Found: …View" for ModalContent dialogs with non-conventional names.
        if (data is not ObservableObject)
        {
            return false;
        }

        return CanResolve(data.GetType());
    }

    private static bool CanResolve(Type vmType)
    {
        if (KnownMappings.ContainsKey(vmType))
        {
            return true;
        }

        return ResolveViewType(GetConventionViewTypeName(vmType)) != null;
    }

    private static bool TryCreateControl(Type vmType, out Control? control)
    {
        if (KnownMappings.TryGetValue(vmType, out var createKnownControl))
        {
            control = createKnownControl();
            return true;
        }

        var type = ResolveViewType(GetConventionViewTypeName(vmType));
        if (type != null)
        {
            control = (Control)Activator.CreateInstance(type)!;
            return true;
        }

        control = null;
        return false;
    }

    private static string GetConventionViewTypeName(Type vmType)
    {
        return vmType.FullName!.Replace("ViewModel", "View").Replace("ViewModels", "Views");
    }

    private static Type? ResolveViewType(string fullName)
    {
        Type? type = Type.GetType(fullName, throwOnError: false);
        if (type != null)
        {
            return type;
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(candidate => candidate != null);
    }
}
