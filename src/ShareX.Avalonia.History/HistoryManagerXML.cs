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

using ShareX.Ava.Common;
using ShareX.Ava.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ShareX.Ava.History
{
    public class HistoryManagerXML : HistoryManager
    {
        private static readonly object thisLock = new object();

        public HistoryManagerXML(string filePath) : base(filePath)
        {
        }

        internal override List<HistoryItem> Load(string filePath)
        {
            List<HistoryItem> historyItemList = new List<HistoryItem>();
            DebugHelper.WriteLine($"Trace: HistoryManagerXML - Loading from path: {filePath}");

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                lock (thisLock)
                {
                    XmlReaderSettings settings = new XmlReaderSettings
                    {
                        ConformanceLevel = ConformanceLevel.Auto,
                        IgnoreWhitespace = true
                    };

                    try 
                    {
                        using (StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8))
                        using (XmlReader reader = XmlReader.Create(streamReader, settings))
                        {
                            reader.MoveToContent();

                            while (!reader.EOF)
                            {
                                if (reader.NodeType == XmlNodeType.Element && reader.Name == "HistoryItem")
                                {
                                    if (XNode.ReadFrom(reader) is XElement element)
                                    {
                                        HistoryItem hi = ParseHistoryItem(element);
                                        historyItemList.Add(hi);
                                    }
                                }
                                else
                                {
                                    reader.Read();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                         DebugHelper.WriteLine($"Trace: HistoryManagerXML - Error loading history: {ex.Message}");
                         DebugHelper.WriteException(ex);
                    }
                }
            }
            else
            {
                 DebugHelper.WriteLine($"Trace: HistoryManagerXML - File not found or invalid path: {filePath}");
            }

            DebugHelper.WriteLine($"Trace: HistoryManagerXML - Loaded {historyItemList.Count} items.");
            return historyItemList;
        }

        private HistoryItem ParseHistoryItem(XElement element)
        {
            HistoryItem hi = new HistoryItem();

            foreach (XElement child in element.Elements())
            {
                string name = child.Name.LocalName;

                switch (name)
                {
                    case "Filename":
                        hi.FileName = child.Value;
                        break;
                    case "Filepath":
                        hi.FilePath = child.Value;
                        break;
                    case "DateTimeUtc":
                        DateTime dateTime;
                        if (DateTime.TryParse(child.Value, out dateTime))
                        {
                            hi.DateTime = dateTime;
                        }
                        break;
                    case "Type":
                        hi.Type = child.Value;
                        break;
                    case "Host":
                        hi.Host = child.Value;
                        break;
                    case "URL":
                        hi.URL = child.Value;
                        break;
                    case "ThumbnailURL":
                        hi.ThumbnailURL = child.Value;
                        break;
                    case "DeletionURL":
                        hi.DeletionURL = child.Value;
                        break;
                    case "ShortenedURL":
                        hi.ShortenedURL = child.Value;
                        break;
                }
            }

            return hi;
        }

        protected override bool Append(string filePath, IEnumerable<HistoryItem> historyItems)
        {
            DebugHelper.WriteLine($"Trace: HistoryManagerXML - Appending to path: {filePath}");
            if (!string.IsNullOrEmpty(filePath))
            {
                lock (thisLock)
                {
                    try
                    {
                        FileHelpers.CreateDirectoryFromFilePath(filePath);

                        using (FileStream fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                        using (XmlTextWriter writer = new XmlTextWriter(fileStream, Encoding.UTF8))
                        {
                            writer.Formatting = Formatting.Indented;
                            writer.Indentation = 4;

                            foreach (HistoryItem historyItem in historyItems)
                            {
                                writer.WriteStartElement("HistoryItem");
                                writer.WriteElementIfNotEmpty("Filename", historyItem.FileName);
                                writer.WriteElementIfNotEmpty("Filepath", historyItem.FilePath);
                                writer.WriteElementIfNotEmpty("DateTimeUtc", historyItem.DateTime.ToString("o"));
                                writer.WriteElementIfNotEmpty("Type", historyItem.Type);
                                writer.WriteElementIfNotEmpty("Host", historyItem.Host);
                                writer.WriteElementIfNotEmpty("URL", historyItem.URL);
                                writer.WriteElementIfNotEmpty("ThumbnailURL", historyItem.ThumbnailURL);
                                writer.WriteElementIfNotEmpty("DeletionURL", historyItem.DeletionURL);
                                writer.WriteElementIfNotEmpty("ShortenedURL", historyItem.ShortenedURL);
                                writer.WriteEndElement();
                            }

                            writer.WriteWhitespace(Environment.NewLine);
                        }

                        Backup(FilePath);
                        DebugHelper.WriteLine("Trace: HistoryManagerXML - Append successful.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteLine($"Trace: HistoryManagerXML - Error appending history: {ex.Message}");
                        DebugHelper.WriteException(ex);
                    }
                }
            }
            
            DebugHelper.WriteLine("Trace: HistoryManagerXML - Append failed (invalid path or error).");
            return false;
        }
    }
}

