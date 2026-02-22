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

*/

#endregion License Information (GPL v3)

using SkiaSharp;

namespace XerahS.Platform.Abstractions;

public sealed class AfterUploadWindowInfo
{
    public string Url { get; init; } = string.Empty;
    public string? ShortenedUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? DeletionUrl { get; init; }
    public string? FilePath { get; init; }
    public string? FileName { get; init; }
    public string? DataType { get; init; }
    public string? UploaderHost { get; init; }
    public string? ClipboardContentFormat { get; init; }
    public string? OpenUrlFormat { get; init; }
    public bool AutoCloseAfterUploadForm { get; init; }
    public SKBitmap? PreviewImage { get; init; }
    public string? ErrorDetails { get; init; }
}
