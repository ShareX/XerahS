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

using XerahS.Common;
using System.Net;
using System.Text;

namespace XerahS.Indexer
{
    public class IndexerHtml : Indexer
    {
        private const string DefaultCss = @":root {
    color-scheme: light;
}

* {
    box-sizing: border-box;
}

body {
    margin: 0;
    font-family: ""Segoe UI"", ""Helvetica Neue"", Arial, sans-serif;
    background-color: #f6f7f9;
    color: #1f2328;
}

a {
    color: #0969da;
    text-decoration: none;
}

a:hover {
    text-decoration: underline;
}

.container {
    max-width: 1000px;
    margin: 0 auto;
    padding: 24px;
}

ul {
    margin: 0 0 12px 0;
    list-style-type: none;
    padding-left: 12px;
}

li {
    margin-bottom: 4px;
}

h1, h2, h3, h4, h5, h6 {
    margin: 0;
    padding: 8px 12px;
    border-radius: 8px 8px 0 0;
    background-color: #0f4c81;
    color: #ffffff;
    font-size: 16px;
    font-weight: 600;
}

h1 {
    font-size: 22px;
    margin-bottom: 6px;
}

h2 {
    background-color: #1f6fb2;
}

h3 {
    background-color: #3380bd;
}

h4 {
    background-color: #4a95c9;
}

h5 {
    background-color: #5ea9d4;
}

h6 {
    background-color: #72bedf;
}

.MainFolderBorder, .FolderBorder {
    border: 1px solid #d0d7de;
    border-top: none;
    border-bottom-left-radius: 8px;
    border-bottom-right-radius: 8px;
    padding: 16px 12px 10px 12px;
    background-color: #ffffff;
}

.MainFolderBorder {
    margin: 0 0 14px 0;
}

.FolderBorder {
    margin: 0 0 12px 0;
}

.FolderInfo {
    color: #f0f6fc;
    float: right;
    margin-left: 12px;
}

.FileSize {
    color: #57606a;
}

footer {
    margin-top: 24px;
    color: #57606a;
    font-size: 12px;
}";
        protected StringBuilder sbContent = new StringBuilder();
        protected int prePathTrim = 0;
        private const int IndentSize = 2;
        private const int ContentBaseIndent = 3;

        public IndexerHtml(IndexerSettings indexerSettings) : base(indexerSettings)
        {
        }

        public override string Index(string folderPath)
        {
            sbContent.Clear();
            StringBuilder sbHtmlIndex = new StringBuilder();
            AppendHtmlLine(sbHtmlIndex, 0, "<!DOCTYPE html>");
            AppendHtmlLine(sbHtmlIndex, 0, HtmlHelper.StartTag("html", "", "lang=\"en\""));
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.StartTag("head"));
            AppendHtmlLine(sbHtmlIndex, 2, "<meta charset=\"UTF-8\">");
            AppendHtmlLine(sbHtmlIndex, 2, "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
            AppendHtmlLine(sbHtmlIndex, 2, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            AppendHtmlLine(sbHtmlIndex, 2, HtmlHelper.Tag("title", "Index for " + Path.GetFileName(folderPath)));
            AppendHtmlBlock(sbHtmlIndex, 2, GetCssStyle());
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.EndTag("head"));
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.StartTag("body"));
            AppendHtmlLine(sbHtmlIndex, 2, HtmlHelper.StartTag("div", "", "class=\"container\""));

            folderPath = Path.GetFullPath(folderPath).TrimEnd('\\');
            prePathTrim = folderPath.LastIndexOf(@"\") + 1;

            FolderInfo folderInfo = GetFolderInfo(folderPath);
            folderInfo.Update();

            IndexFolder(folderInfo);
            string index = sbContent.ToString().TrimEnd();
            AppendHtmlBlock(sbHtmlIndex, 0, index);
            if (settings.AddFooter)
            {
                AppendHtmlLine(sbHtmlIndex, ContentBaseIndent, HtmlHelper.StartTag("footer") + GetFooter() + HtmlHelper.EndTag("footer"));
            }

            AppendHtmlLine(sbHtmlIndex, 2, HtmlHelper.EndTag("div"));
            AppendHtmlLine(sbHtmlIndex, 1, HtmlHelper.EndTag("body"));
            AppendHtmlLine(sbHtmlIndex, 0, HtmlHelper.EndTag("html"));
            return sbHtmlIndex.ToString().Trim();
        }

        protected override void IndexFolder(FolderInfo dir, int level = 0)
        {
            int blockIndent = ContentBaseIndent + (level * 2);
            AppendHtmlLine(sbContent, blockIndent, GetFolderNameRow(dir, level));

            string divClass = level > 0 ? "FolderBorder" : "MainFolderBorder";
            AppendHtmlLine(sbContent, blockIndent, HtmlHelper.StartTag("div", "", $"class=\"{divClass}\""));

            if (dir.Files.Count > 0)
            {
                AppendHtmlLine(sbContent, blockIndent + 1, HtmlHelper.StartTag("ul"));

                foreach (FileInfo fi in dir.Files)
                {
                    AppendHtmlLine(sbContent, blockIndent + 2, GetFileNameRow(fi));
                }

                AppendHtmlLine(sbContent, blockIndent + 1, HtmlHelper.EndTag("ul"));
            }

            foreach (FolderInfo subdir in dir.Folders)
            {
                IndexFolder(subdir, level + 1);
            }

            AppendHtmlLine(sbContent, blockIndent, HtmlHelper.EndTag("div"));
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
            return $"Generated by <a href=\"{Links.Website}\">{AppResources.AppName} Directory Indexer</a> on {DateTime.UtcNow:yyyy-MM-dd 'at' HH:mm:ss 'UTC'}";
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

        private static void AppendHtmlLine(StringBuilder builder, int indentLevel, string line)
        {
            builder.Append(new string(' ', indentLevel * IndentSize));
            builder.AppendLine(line);
        }

        private static void AppendHtmlBlock(StringBuilder builder, int indentLevel, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                AppendHtmlLine(builder, indentLevel, string.Empty);
                return;
            }

            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                AppendHtmlLine(builder, indentLevel, line);
            }
        }
    }
}
