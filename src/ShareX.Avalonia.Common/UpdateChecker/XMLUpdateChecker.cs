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
