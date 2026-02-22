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
using System.Xml;
using System.Xml.Linq;

namespace XerahS.Common
{
    public class XMLUpdateChecker : UpdateChecker
    {
        public string URL { get; private set; }
        public string ApplicationName { get; private set; }

        public XMLUpdateChecker(string url, string applicationName)
        {
            URL = url;
            ApplicationName = applicationName;
        }

        public override async Task CheckUpdateAsync()
        {
            try
            {
                // Replaced WebHelpers.DownloadStringAsync with HttpClient
                using (HttpClient client = new HttpClient())
                {
                    string response = await client.GetStringAsync(URL);

                    using (StringReader sr = new StringReader(response))
                    using (XmlTextReader xml = new XmlTextReader(sr))
                    {
                        XDocument xd = XDocument.Load(xml);

                        string node;

                        switch (ReleaseType)
                        {
                            default:
                            case ReleaseChannelType.Stable:
                                node = "Stable";
                                break;
                            case ReleaseChannelType.Beta:
                                node = "Beta";
                                break;
                            case ReleaseChannelType.Dev:
                                node = "Dev";
                                break;
                        }

                        XElement? updateNode = xd.Element("Update");
                        if (updateNode != null)
                        {
                            XElement? appNode = updateNode.Element(ApplicationName);
                            if (appNode != null)
                            {
                                XElement? channelNode = appNode.Element(node);

                                if (channelNode == null && ReleaseType == ReleaseChannelType.Beta)
                                {
                                    channelNode = appNode.Element("Stable");
                                }

                                if (channelNode != null)
                                {
                                    XElement? versionEl = channelNode.Element("Version");
                                    XElement? urlEl = channelNode.Element("URL");

                                    if (versionEl != null && urlEl != null)
                                    {
                                        LatestVersion = new Version(versionEl.Value);
                                        DownloadURL = urlEl.Value;
                                        RefreshStatus();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "XML update check failed");
            }

            Status = UpdateStatus.UpdateCheckFailed;
        }
    }
}
