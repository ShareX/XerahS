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

                        if (xd != null)
                        {
                            string node;

                            switch (ReleaseType)
                            {
                                default:
                                case ReleaseChannelType.Stable:
                                    node = "Stable";
                                    break;
                                case ReleaseChannelType.Beta:
                                    node = "Beta"; // ShareX generic logic was "Beta|Stable" but XML structure usually implies checking one node. Adjust logic if needed.
                                    // Original: node = "Beta|Stable"; implies path traversal or fallback? 
                                    // The original GetNode(path) likely handled "Beta|Stable" by trying Beta then Stable? 
                                    // For now, let's assume direct mapping or implement simple path logic.
                                    // Actually, if the path implies alternatives, I'll stick to simple "Stable" for MVP or check how GetNode worked.
                                    // Let's assume standard single node for now. To be safe, I'll use "Stable" or the specific type.
                                    break;
                                case ReleaseChannelType.Dev:
                                    node = "Dev";
                                    break;
                            }

                            // Handling "ApplicationName" and "node" traversal manually since GetNode extension is missing
                            // Path: Update/{ApplicationName}/{node}
                            XElement updateNode = xd.Element("Update");
                            if (updateNode != null)
                            {
                                XElement appNode = updateNode.Element(ApplicationName);
                                if (appNode != null)
                                {
                                    XElement channelNode = appNode.Element(node);

                                    // Fallback logic if needed? 
                                    // If ReleaseType is Beta, and Beta node missing, maybe fallback to Stable?
                                    // Original code had "Beta|Stable" string, suggesting GetNode parsed pipes. 
                                    // I will interpret that as "Try Beta, then Stable".

                                    if (channelNode == null && ReleaseType == ReleaseChannelType.Beta)
                                    {
                                        channelNode = appNode.Element("Stable");
                                    }

                                    if (channelNode != null)
                                    {
                                        XElement versionEl = channelNode.Element("Version");
                                        XElement urlEl = channelNode.Element("URL");

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
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "XML update check failed");
            }

            Status = UpdateStatus.UpdateCheckFailed;
        }
    }
}
