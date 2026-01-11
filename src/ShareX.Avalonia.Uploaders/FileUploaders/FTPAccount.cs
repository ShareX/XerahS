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
using System.ComponentModel;

namespace XerahS.Uploaders.FileUploaders
{
    public class FTPAccount : ICloneable
    {
        [Category("FTP"), Description("Shown in the list as: Name - Server:Port")]
        public string Name { get; set; }

        [Category("Account"), Description("Connection protocol"), DefaultValue(FTPProtocol.FTP)]
        public FTPProtocol Protocol { get; set; }

        [Category("FTP"), Description("Host, e.g. example.com")]
        public string Host { get; set; }

        [Category("FTP"), Description("Port number"), DefaultValue(21)]
        public int Port { get; set; }

        [Category("FTP")]
        public string Username { get; set; }

        [Category("FTP"), JsonEncrypt]
        public string Password { get; set; }

        [Category("FTP"), Description("Set true for active or false for passive"), DefaultValue(false)]
        public bool IsActive { get; set; }

        [Category("FTP"), Description("FTP sub folder path, example: Screenshots. You can use name parsing: %y = year, %mo = month.")]
        public string SubFolderPath { get; set; }

        [Category("FTP"), Description("Choose an appropriate protocol to be accessed by the browser"), DefaultValue(BrowserProtocol.http)]
        public BrowserProtocol BrowserProtocol { get; set; }

        [Category("FTP"), Description("URL = HttpHomePath + SubFolderPath + FileName. If HttpHomePath is empty then URL = Host + SubFolderPath + FileName. %host = Host")]
        public string HttpHomePath { get; set; }

        [Category("FTP"), Description("Automatically add sub folder path to end of http home path"), DefaultValue(false)]
        public bool HttpHomePathAutoAddSubFolderPath { get; set; }

        [Category("FTP"), Description("Don't add file extension to URL"), DefaultValue(false)]
        public bool HttpHomePathNoExtension { get; set; }

        [Category("FTPS"), Description("Type of SSL to use. Explicit is TLS, Implicit is SSL."), DefaultValue(FTPSEncryption.Explicit)]
        public FTPSEncryption FTPSEncryption { get; set; }

        [Category("SFTP"), Description("Key location")]
        public string Keypath { get; set; }

        [Category("SFTP"), Description("OpenSSH key passphrase"), JsonEncrypt]
        public string Passphrase { get; set; }

        [Category("FTP"), Description("Protocol://Host:Port"), Browsable(false)]
        public string FTPAddress
        {
            get
            {
                if (string.IsNullOrEmpty(Host))
                {
                    return string.Empty;
                }

                string serverProtocol;

                switch (Protocol)
                {
                    default:
                    case FTPProtocol.FTP:
                        serverProtocol = "ftp://";
                        break;
                    case FTPProtocol.FTPS:
                        serverProtocol = "ftps://";
                        break;
                    case FTPProtocol.SFTP:
                        serverProtocol = "sftp://";
                        break;
                }

                return $"{Name} - {EnumExtensions.GetDescription(Protocol)}";
            }
        }

        public FTPAccount()
        {
            Name = "New account";
            Protocol = FTPProtocol.FTP;
            Host = string.Empty;
            Port = 21;
            Username = string.Empty;
            Password = string.Empty;
            IsActive = false;
            SubFolderPath = string.Empty;
            BrowserProtocol = BrowserProtocol.http;
            HttpHomePath = string.Empty;
            HttpHomePathAutoAddSubFolderPath = true;
            HttpHomePathNoExtension = false;
            FTPSEncryption = FTPSEncryption.Explicit;
            Keypath = string.Empty;
            Passphrase = string.Empty;
        }

        public string GetSubFolderPath(string? fileName = null, NameParserType nameParserType = NameParserType.URL)
        {
            string path = NameParser.Parse(nameParserType, SubFolderPath.Replace("%host", Host, StringComparison.OrdinalIgnoreCase));
            return URLHelpers.CombineURL(path, fileName ?? string.Empty);
        }

        public string GetHttpHomePath()
        {
            string homePath = HttpHomePath.Replace("%host", Host, StringComparison.OrdinalIgnoreCase);

            ShareXCustomUploaderSyntaxParser parser = new ShareXCustomUploaderSyntaxParser
            {
                UseNameParser = true,
                NameParserType = NameParserType.URL
            };

            return parser.Parse(homePath);
        }

        public string GetUriPath(string fileName)
        {
            return GetUriPath(fileName, null);
        }

        public string GetUriPath(string fileName, string? subFolderPath)
        {
            if (string.IsNullOrEmpty(Host))
            {
                return string.Empty;
            }

            if (HttpHomePathNoExtension)
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            fileName = URLHelpers.URLEncode(fileName);

            if (subFolderPath == null)
            {
                subFolderPath = GetSubFolderPath();
            }

            UriBuilder httpHomeUri;

            string httpHomePath = GetHttpHomePath();

            if (string.IsNullOrEmpty(httpHomePath))
            {
                string url = Host;

                if (url.StartsWith("ftp.", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(4);
                }

                if (HttpHomePathAutoAddSubFolderPath)
                {
                    url = URLHelpers.CombineURL(url, subFolderPath);
                }

                url = URLHelpers.CombineURL(url, fileName);

                httpHomeUri = new UriBuilder(url)
                {
                    Port = -1
                };
            }
            else
            {
                int firstSlash = httpHomePath.IndexOf('/');
                string httpHome = firstSlash >= 0 ? httpHomePath.Substring(0, firstSlash) : httpHomePath;
                int portSpecifiedAt = httpHome.LastIndexOf(':');

                string httpHomeHost = portSpecifiedAt >= 0 ? httpHome.Substring(0, portSpecifiedAt) : httpHome;
                int httpHomePort = -1;
                string httpHomePathAndQuery = firstSlash >= 0 ? httpHomePath.Substring(firstSlash + 1) : string.Empty;
                int querySpecifiedAt = httpHomePathAndQuery.LastIndexOf('?');
                string httpHomeDir = querySpecifiedAt >= 0 ? httpHomePathAndQuery.Substring(0, querySpecifiedAt) : httpHomePathAndQuery;
                string httpHomeQuery = querySpecifiedAt >= 0 ? httpHomePathAndQuery.Substring(querySpecifiedAt + 1) : string.Empty;

                if (portSpecifiedAt >= 0)
                {
                    int.TryParse(httpHome.Substring(portSpecifiedAt + 1), out httpHomePort);
                }

                httpHomeUri = new UriBuilder
                {
                    Host = httpHomeHost,
                    Path = httpHomeDir,
                    Query = httpHomeQuery
                };

                if (portSpecifiedAt >= 0)
                {
                    httpHomeUri.Port = httpHomePort;
                }

                if (httpHomeUri.Query.EndsWith("=", StringComparison.Ordinal))
                {
                    string query = httpHomeUri.Query.TrimStart('?');
                    httpHomeUri.Query = HttpHomePathAutoAddSubFolderPath
                        ? URLHelpers.CombineURL(query, subFolderPath, fileName)
                        : query + fileName;
                }
                else
                {
                    if (HttpHomePathAutoAddSubFolderPath)
                    {
                        httpHomeUri.Path = URLHelpers.CombineURL(httpHomeUri.Path, subFolderPath);
                    }

                    httpHomeUri.Path = URLHelpers.CombineURL(httpHomeUri.Path, fileName);
                }
            }

            httpHomeUri.Scheme = EnumExtensions.GetDescription(BrowserProtocol);
            return httpHomeUri.Uri.OriginalString;
        }

        public string GetFtpPath(string fileName)
        {
            if (string.IsNullOrEmpty(FTPAddress))
            {
                return string.Empty;
            }

            return URLHelpers.CombineURL(FTPAddress, GetSubFolderPath(fileName, NameParserType.FilePath));
        }

        public override string ToString()
        {
            return $"{Name} ({Host}:{Port})";
        }

        public FTPAccount Clone()
        {
            return MemberwiseClone() as FTPAccount;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}
