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
using XerahS.Core;
using XerahS.UI.Services;
using XerahS.UI.Views;

namespace XerahS.UI.Helpers;

/// <summary>
/// Central routing helper for all Tools navigation tags.
/// Used by both menu-bar and navigation-bar flows.
/// </summary>
public static class ToolNavigationHelper
{
    public static bool TryHandleToolsTag(
        string tag,
        Window? owner,
        ContentControl contentFrame,
        Func<WorkflowType, Task> executeWorkflowFromNavigationAsync)
    {
        if (string.IsNullOrEmpty(tag) || !tag.StartsWith("Tools", StringComparison.Ordinal))
        {
            return false;
        }

        switch (tag)
        {
            case "Tools":
                contentFrame.Content = new ToolsView();
                return true;
            case "Tools_IndexFolder":
                contentFrame.Content = new IndexFolderView();
                return true;
            case "Tools_ColorPicker":
                _ = ColorPickerToolService.HandleWorkflowAsync(WorkflowType.ColorPicker, owner);
                return true;
            case "Tools_ScreenColorPicker":
                _ = ColorPickerToolService.HandleWorkflowAsync(WorkflowType.ScreenColorPicker, owner);
                return true;
            case "Tools_QrGenerator":
                _ = QrCodeToolService.HandleWorkflowAsync(WorkflowType.QRCode, owner);
                return true;
            case "Tools_QrScanScreen":
                _ = QrCodeToolService.HandleWorkflowAsync(WorkflowType.QRCodeDecodeFromScreen, owner);
                return true;
            case "Tools_QrScanRegion":
                _ = QrCodeToolService.HandleWorkflowAsync(WorkflowType.QRCodeScanRegion, owner);
                return true;
            case "Tools_ImageCombiner":
                _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.ImageCombiner, owner);
                return true;
            case "Tools_ImageSplitter":
                _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.ImageSplitter, owner);
                return true;
            case "Tools_ImageThumbnailer":
                _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.ImageThumbnailer, owner);
                return true;
            case "Tools_VideoConverter":
                _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.VideoConverter, owner);
                return true;
            case "Tools_VideoThumbnailer":
                _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.VideoThumbnailer, owner);
                return true;
            case "Tools_AnalyzeImage":
                _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.AnalyzeImage, owner);
                return true;
            case "Tools_Ruler":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.Ruler);
                return true;
            case "Tools_PinToScreenFromScreen":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.PinToScreenFromScreen);
                return true;
            case "Tools_PinToScreenFromClipboard":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.PinToScreenFromClipboard);
                return true;
            case "Tools_PinToScreenFromFile":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.PinToScreenFromFile);
                return true;
            case "Tools_PinToScreenCloseAll":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.PinToScreenCloseAll);
                return true;
            case "Tools_OCR":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.OCR);
                return true;
            case "Tools_HashCheck":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.HashCheck);
                return true;
            case "Tools_Metadata":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.Metadata);
                return true;
            case "Tools_StripMetadata":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.StripMetadata);
                return true;
            case "Tools_ClipboardViewer":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.ClipboardViewer);
                return true;
            case "Tools_MonitorTest":
                _ = executeWorkflowFromNavigationAsync(WorkflowType.MonitorTest);
                return true;
            default:
                return false;
        }
    }
}
