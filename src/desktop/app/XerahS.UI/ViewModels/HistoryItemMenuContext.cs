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

using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.History;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Context for the shared history-item context menu (ContextFlyout).
/// Exposes commands and the current item so the same MenuFlyout can be used in History and Toast.
/// </summary>
public interface IHistoryItemMenuContext
{
    ICommand EditImageCommand { get; }
    ICommand EditAnnotationsCommand { get; }
    ICommand OpenFileCommand { get; }
    ICommand UploadItemCommand { get; }
    ICommand OpenFolderCommand { get; }
    ICommand CopyFilePathCommand { get; }
    ICommand CopyURLCommand { get; }
    ICommand CopyMarkdownImageCommand { get; }
    ICommand CopyImageToClipboardCommand { get; }
    ICommand CopyErrorsCommand { get; }
    ICommand OpenURLCommand { get; }
    ICommand PublishCommand { get; }
    ICommand UnpublishCommand { get; }
    ICommand DeleteItemCommand { get; }
    ICommand DeleteRemoteCommand { get; }
    ICommand TrimVideoCommand { get; }
    ICommand ResizeImageCommand { get; }

    /// <summary>"Copy Path", or "Copy Paths" when the menu acts on several selected entries.</summary>
    string CopyFilePathHeader { get; }

    /// <summary>"Copy URL", or "Copy URLs" when the menu acts on several selected entries.</summary>
    string CopyUrlHeader { get; }

    /// <summary>Current item used for visibility (URL, HasErrors).</summary>
    IHistoryItemMenuTarget? Item { get; }

    /// <summary>Underlying item for display (e.g. HistoryItem in History view). Null for Toast.</summary>
    object? DisplayItem { get; }
}

/// <summary>
/// Minimal item shape for context menu visibility (URL, HasErrors).
/// </summary>
public interface IHistoryItemMenuTarget
{
    string? URL { get; }
    bool HasErrors { get; }
    bool HasEditableAnnotations { get; }
    bool HasImageFile { get; }
    bool HasFilePath { get; }
    bool HasExistingFile { get; }
    bool CanPublish { get; }
    bool CanUnpublish { get; }
    bool CanDeleteRemotely { get; }
    bool HasVideoFile { get; }
}

/// <summary>
/// Adapter so <see cref="HistoryItem"/> can be used as <see cref="IHistoryItemMenuTarget"/> without coupling History to UI.
/// </summary>
public sealed class HistoryItemMenuTargetAdapter : IHistoryItemMenuTarget
{
    private readonly HistoryItem _item;

    private readonly string? _currentOwnerSubject;

    public HistoryItemMenuTargetAdapter(HistoryItem item, string? currentOwnerSubject = null)
    {
        _item = item;
        _currentOwnerSubject = currentOwnerSubject;
    }

    public string? URL => _item.URL;
    public bool HasErrors => _item.HasErrors;
    public bool HasEditableAnnotations => _item.HasEditableAnnotations;
    public bool HasImageFile => !string.IsNullOrWhiteSpace(_item.FilePath) && FileHelpers.IsImageFile(_item.FilePath);
    public bool HasFilePath => !string.IsNullOrWhiteSpace(_item.FilePath);
    public bool HasExistingFile => !string.IsNullOrWhiteSpace(_item.FilePath) && File.Exists(_item.FilePath);
    public bool CanPublish => HistoryPublishMetadata.CanPublish(_item, _currentOwnerSubject);
    public bool CanUnpublish => HistoryPublishMetadata.CanUnpublish(_item, _currentOwnerSubject);
    public bool CanDeleteRemotely => XerahS.Core.Services.UploadRemoteDeletionService.CanDelete(_item);
    public bool HasVideoFile => HasExistingFile && FileHelpers.IsVideoFile(_item.FilePath);
}

/// <summary>
/// Context for the shared context menu when used from History view (per-item).
/// </summary>
public sealed class HistoryItemMenuContext : IHistoryItemMenuContext
{
    private readonly HistoryViewModel _vm;
    private readonly HistoryItem _item;

    public HistoryItemMenuContext(HistoryViewModel vm, HistoryItem item)
    {
        _vm = vm;
        _item = item;
        Item = new HistoryItemMenuTargetAdapter(item, vm.CurrentCloudOwnerSubject);
        EditImageCommand = new RelayCommand(() => _vm.EditImageCommand.Execute(_item));
        EditAnnotationsCommand = new RelayCommand(() => _vm.EditAnnotationsCommand.Execute(_item));
        OpenFileCommand = new RelayCommand(() => _vm.OpenFileCommand.Execute(_item));
        UploadItemCommand = new RelayCommand(() => _vm.UploadItemCommand.Execute(_item));
        OpenFolderCommand = new RelayCommand(() => _vm.OpenFolderCommand.Execute(_item));
        CopyFilePathCommand = new RelayCommand(() => _vm.CopyFilePathCommand.Execute(_item));
        CopyURLCommand = new RelayCommand(() => _vm.CopyURLCommand.Execute(_item));
        CopyMarkdownImageCommand = new RelayCommand(() => _vm.CopyMarkdownImageCommand.Execute(_item));
        CopyImageToClipboardCommand = new RelayCommand(() => _vm.CopyImageToClipboardCommand.Execute(_item));
        CopyErrorsCommand = new RelayCommand(() => _vm.CopyErrorsCommand.Execute(_item));
        OpenURLCommand = new RelayCommand(() => _vm.OpenURLCommand.Execute(_item));
        PublishCommand = new AsyncRelayCommand(() => _vm.PublishItemCommand.ExecuteAsync(_item));
        UnpublishCommand = new AsyncRelayCommand(() => _vm.UnpublishItemCommand.ExecuteAsync(_item));
        DeleteItemCommand = new RelayCommand(() => _vm.DeleteItemCommand.Execute(_item));
        DeleteRemoteCommand = new AsyncRelayCommand(() => _vm.DeleteRemoteItemCommand.ExecuteAsync(_item));
        TrimVideoCommand = new RelayCommand(() => _vm.TrimVideoCommand.Execute(_item));
        ResizeImageCommand = new RelayCommand(() => _vm.ResizeImageCommand.Execute(_item));
    }

    public IHistoryItemMenuTarget? Item { get; }
    public object? DisplayItem => _item;
    public string CopyFilePathHeader => _vm.IsInMultiSelection(_item) ? "Copy Paths" : "Copy Path";
    public string CopyUrlHeader => _vm.IsInMultiSelection(_item) ? "Copy URLs" : "Copy URL";

    public ICommand EditImageCommand { get; }
    public ICommand EditAnnotationsCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand UploadItemCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand CopyFilePathCommand { get; }
    public ICommand CopyURLCommand { get; }
    public ICommand CopyMarkdownImageCommand { get; }
    public ICommand CopyImageToClipboardCommand { get; }
    public ICommand CopyErrorsCommand { get; }
    public ICommand OpenURLCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand UnpublishCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand DeleteRemoteCommand { get; }
    public ICommand TrimVideoCommand { get; }
    public ICommand ResizeImageCommand { get; }
}

/// <summary>
/// Adapter so <see cref="ToastViewModel"/> can be used as <see cref="IHistoryItemMenuTarget"/> for visibility bindings.
/// </summary>
public sealed class ToastItemMenuTargetAdapter : IHistoryItemMenuTarget
{
    private readonly ToastViewModel _vm;

    public ToastItemMenuTargetAdapter(ToastViewModel vm)
    {
        _vm = vm;
    }

    public string? URL => _vm.Url;
    public bool HasErrors => _vm.HasErrors;
    public bool HasEditableAnnotations => false;
    public bool HasImageFile => _vm.CanCopyImage;
    public bool HasFilePath => !string.IsNullOrWhiteSpace(_vm.FilePath);
    public bool HasExistingFile => _vm.HasExistingFile;
    public bool CanPublish => _vm.CanPublishHistoryItem;
    public bool CanUnpublish => _vm.CanUnpublishHistoryItem;
    public bool CanDeleteRemotely => false;
    public bool HasVideoFile => false;
}


/// <summary>
/// Context for the shared context menu when used from Toast window.
/// </summary>
public sealed class ToastMenuContext : IHistoryItemMenuContext
{
    public ToastMenuContext(ToastViewModel vm)
    {
        ViewModel = vm;
        Item = new ToastItemMenuTargetAdapter(vm);
    }

    /// <summary>Toast ViewModel for view bindings (Title, Text, Image, etc.).</summary>
    public ToastViewModel ViewModel { get; }

    public IHistoryItemMenuTarget? Item { get; }
    public object? DisplayItem => null;
    public string CopyFilePathHeader => "Copy Path";
    public string CopyUrlHeader => "Copy URL";

    public ICommand EditImageCommand => ViewModel.EditImageCommand;
    public ICommand EditAnnotationsCommand => ViewModel.EditImageCommand;
    public ICommand OpenFileCommand => ViewModel.OpenFileCommand;
    public ICommand UploadItemCommand => ViewModel.UploadItemCommand;
    public ICommand OpenFolderCommand => ViewModel.OpenFolderCommand;
    public ICommand CopyFilePathCommand => ViewModel.CopyFilePathCommand;
    public ICommand CopyURLCommand => ViewModel.CopyUrlCommand;
    public ICommand CopyMarkdownImageCommand => ViewModel.CopyMarkdownImageCommand;
    public ICommand CopyImageToClipboardCommand => ViewModel.CopyImageToClipboardCommand;
    public ICommand CopyErrorsCommand => ViewModel.CopyErrorsCommand;
    public ICommand OpenURLCommand => ViewModel.OpenURLCommand;
    public ICommand PublishCommand => ViewModel.PublishCommand;
    public ICommand UnpublishCommand => ViewModel.UnpublishCommand;
    public ICommand DeleteItemCommand => ViewModel.DeleteItemCommand;
    public ICommand DeleteRemoteCommand { get; } = new RelayCommand(() => { });
    public ICommand TrimVideoCommand { get; } = new RelayCommand(() => { });
    public ICommand ResizeImageCommand => ViewModel.ResizeImageCommand;
}
