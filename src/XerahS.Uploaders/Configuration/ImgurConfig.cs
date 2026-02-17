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

using XerahS.Uploaders.Abstractions;
using ShareX.UploadersLib.ImageUploaders;

namespace XerahS.Uploaders.Configuration;

public sealed class ImgurConfig : IUploaderConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Imgur";
    public UploaderType Type => UploaderType.Imgur;

    public AccountType AccountType { get; set; } = AccountType.Anonymous;
    public bool DirectLink { get; set; } = true;
    public ImgurThumbnailType ThumbnailType { get; set; } = ImgurThumbnailType.Medium_Thumbnail;
    public bool UseGIFV { get; set; } = true;
    public OAuth2Info? OAuth2Info { get; set; }
    public bool UploadSelectedAlbum { get; set; }
    public ImgurAlbumData? SelectedAlbum { get; set; }
    public List<ImgurAlbumData>? AlbumList { get; set; }
}
