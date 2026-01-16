#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

namespace XerahS.Core;

[Flags]
public enum AfterCaptureTasks // Localized
{
    None = 0,
    ShowQuickTaskMenu = 1,
    ShowAfterCaptureWindow = 1 << 1,
    BeautifyImage = 1 << 2,
    AddImageEffects = 1 << 3,
    AnnotateImage = 1 << 4,
    CopyImageToClipboard = 1 << 5,
    PinToScreen = 1 << 6,
    SendImageToPrinter = 1 << 7,
    SaveImageToFile = 1 << 8,
    SaveImageToFileWithDialog = 1 << 9,
    SaveThumbnailImageToFile = 1 << 10,
    PerformActions = 1 << 11,
    CopyFileToClipboard = 1 << 12,
    CopyFilePathToClipboard = 1 << 13,
    ShowInExplorer = 1 << 14,
    AnalyzeImage = 1 << 15,
    ScanQRCode = 1 << 16,
    DoOCR = 1 << 17,
    ShowBeforeUploadWindow = 1 << 18,
    UploadImageToHost = 1 << 19,
    DeleteFile = 1 << 20
}

[Flags]
public enum AfterUploadTasks // Localized
{
    None = 0,
    ShowAfterUploadWindow = 1,
    UseURLShortener = 1 << 1,
    ShareURL = 1 << 2,
    CopyURLToClipboard = 1 << 3,
    OpenURL = 1 << 4,
    ShowQRCode = 1 << 5
}
