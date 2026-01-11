#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using XerahS.Common;
using System.Net;
using System.Text;

namespace XerahS.Indexer
{
    public class IndexerHtml : Indexer
    {
        private const string DefaultCss = @"body {
    font-family: Arial, Helvetica, sans-serif;
    background-color: #F2F2F2;
}

img {
    border-style: none;
}

a:link, a:visited {
    color: #0066FF;
}

ul {
    margin: 0px 0px 8px 0px;
    list-style-type: none;
    padding-left: 10px;
}

li {
    margin-bottom: 2px;
}

h1, h2, h3, h4, h5, h6 {
    border: 1px solid #000000;
    border-top-left-radius: 5px;
    border-top-right-radius: 5px;
    padding: 4px 0px 4px 10px;
    margin: 0px 10px -10px 10px;
    color: #FFFFFF;
    font-family: Arial, Helvetica, sans-serif;
    font-size: large;
    font-weight: normal;
    text-shadow: 0px 1px 1px #000000;
}

h1 {
    background-color: #336699;
    margin: 0px 0px -10px 0px;
    font-size: x-large;
}

h2 {
    background-color: #3D7AAD;
}

h3 {
    background-color: #478FC2;
}

h4 {
    background-color: #52A3D6;
}

h5 {
    background-color: #5CB8EB;
}

h6 {
    background-color: #66CCFF;
}

.MainFolderBorder, .FolderBorder {
    border-style: solid;
    border-width: 0px 1px 1px 1px;
    border-bottom-left-radius: 5px;
    border-bottom-right-radius: 5px;
    padding: 20px 0px 0px 0px;
}

.MainFolderBorder {
    margin: 0px 0px 10px 0px;
}

.FolderBorder {
    margin: 0px 10px 10px 10px;
}

.FolderInfo {
    color: #FFFFFF;
    float: right;
    margin-right: 10px;
    display: block;
}

.FileSize {
    color: #3D7AAD;
}";
        protected StringBuilder sbContent = new StringBuilder();
        protected int prePathTrim = 0;

        public IndexerHtml(IndexerSettings indexerSettings) : base(indexerSettings)
        {
        }

        public override string Index(string folderPath)
        {
            StringBuilder sbHtmlIndex = new StringBuilder();
            sbHtmlIndex.AppendLine("<!DOCTYPE html>");
            sbHtmlIndex.AppendLine(HtmlHelper.StartTag("html"));
            sbHtmlIndex.AppendLine(HtmlHelper.StartTag("head"));
            sbHtmlIndex.AppendLine("<meta charset=\"UTF-8\">");
            sbHtmlIndex.AppendLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
            sbHtmlIndex.AppendLine(HtmlHelper.Tag("title", "Index for " + Path.GetFileName(folderPath)));
            sbHtmlIndex.AppendLine(GetCssStyle());
            sbHtmlIndex.AppendLine(HtmlHelper.EndTag("head"));
            sbHtmlIndex.AppendLine(HtmlHelper.StartTag("body"));

            folderPath = Path.GetFullPath(folderPath).TrimEnd('\\');
            prePathTrim = folderPath.LastIndexOf(@"\") + 1;

            FolderInfo folderInfo = GetFolderInfo(folderPath);
            folderInfo.Update();

            IndexFolder(folderInfo);
            string index = sbContent.ToString().Trim();

            sbHtmlIndex.AppendLine(index);
            if (settings.AddFooter) sbHtmlIndex.AppendLine(HtmlHelper.StartTag("div") + GetFooter() + HtmlHelper.EndTag("div"));
            sbHtmlIndex.AppendLine(HtmlHelper.EndTag("body"));
            sbHtmlIndex.AppendLine(HtmlHelper.EndTag("html"));
            return sbHtmlIndex.ToString().Trim();
        }

        protected override void IndexFolder(FolderInfo dir, int level = 0)
        {
            sbContent.AppendLine(GetFolderNameRow(dir, level));

            string divClass = level > 0 ? "FolderBorder" : "MainFolderBorder";
            sbContent.AppendLine(HtmlHelper.StartTag("div", "", $"class=\"{divClass}\""));

            if (dir.Files.Count > 0)
            {
                sbContent.AppendLine(HtmlHelper.StartTag("ul"));

                foreach (FileInfo fi in dir.Files)
                {
                    sbContent.AppendLine(GetFileNameRow(fi));
                }

                sbContent.AppendLine(HtmlHelper.EndTag("ul"));
            }

            foreach (FolderInfo subdir in dir.Folders)
            {
                IndexFolder(subdir, level + 1);
            }

            sbContent.AppendLine(HtmlHelper.EndTag("div"));
        }

        private string GetFolderNameRow(FolderInfo dir, int level)
        {
            string folderNameRow = "";

            if (!dir.IsEmpty)
            {
                if (settings.ShowSizeInfo)
                {
                    folderNameRow += dir.Size.ToSizeString(settings.BinaryUnits) + " ";
                }

                folderNameRow += "(";

                if (dir.TotalFileCount > 0)
                {
                    folderNameRow += dir.TotalFileCount.ToString("n0") + " file" + (dir.TotalFileCount > 1 ? "s" : "");
                }

                if (dir.TotalFolderCount > 0)
                {
                    if (dir.TotalFileCount > 0)
                    {
                        folderNameRow += ", ";
                    }

                    folderNameRow += dir.TotalFolderCount.ToString("n0") + " folder" + (dir.TotalFolderCount > 1 ? "s" : "");
                }

                folderNameRow += ")";
                folderNameRow = " " + HtmlHelper.Tag("span", folderNameRow, "", "class=\"FolderInfo\"");
            }

            string pathTitle;

            if (settings.DisplayPath)
            {
                pathTitle = settings.DisplayPathLimited ? dir.FolderPath.Substring(prePathTrim) : dir.FolderPath;
            }
            else
            {
                pathTitle = dir.FolderName;
            }

            int heading = (level + 1).Clamp(1, 6);

            return HtmlHelper.StartTag("h" + heading) + WebUtility.HtmlEncode(pathTitle) + folderNameRow + HtmlHelper.EndTag("h" + heading);
        }

        private string GetFileNameRow(FileInfo fi)
        {
            string fileNameRow = HtmlHelper.StartTag("li") + WebUtility.HtmlEncode(fi.Name);

            if (settings.ShowSizeInfo)
            {
                fileNameRow += " " + HtmlHelper.Tag("span", fi.Length.ToSizeString(settings.BinaryUnits), "", "class=\"FileSize\"");
            }

            fileNameRow += HtmlHelper.EndTag("li");

            return fileNameRow;
        }

        private string GetFooter()
        {
            return $"Generated by <a href=\"{Links.Website}\">ShareX Directory Indexer</a> on {DateTime.UtcNow:yyyy-MM-dd 'at' HH:mm:ss 'UTC'}";
        }

        private string GetCssStyle()
        {
            string css;

            if (settings.UseCustomCSSFile && !string.IsNullOrEmpty(settings.CustomCSSFilePath) && File.Exists(settings.CustomCSSFilePath))
            {
                css = File.ReadAllText(settings.CustomCSSFilePath, Encoding.UTF8);
            }
            else
            {
                css = DefaultCss;
            }

            return $"<style type=\"text/css\">\r\n{css}\r\n</style>";
        }
    }
}
