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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace XerahS.Audits.Tool
{
    class Program
    {
        // Interactive controls to track
        static readonly HashSet<string> InteractiveTypes = new HashSet<string>
        {
            "Button", "MenuItem", "ToggleSwitch", "CheckBox", "RadioButton", "SplitButton", "RepeatButton"
        };

        static int Main(string[] args)
        {
            try
            {
                var rootDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
                Console.WriteLine($"Starting Scan in: {rootDir}");

                var docsDir = Path.Combine(rootDir, "docs", "audits");
                Directory.CreateDirectory(docsDir);
                var inventoryPath = Path.Combine(docsDir, "ui-control-inventory.json");
                var mdPath = Path.Combine(docsDir, "ui-control-inventory.md");
                var diffPath = Path.Combine(docsDir, "ui-control-inventory.diff.md");

                // Load Baseline
                List<ControlEntry> baseline = new List<ControlEntry>();
                if (File.Exists(inventoryPath))
                {
                    try
                    {
                        var json = File.ReadAllText(inventoryPath);
                        baseline = JsonSerializer.Deserialize<List<ControlEntry>>(json) ?? new List<ControlEntry>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to load baseline: {ex.Message}");
                    }
                }

                // Scan
                var currentInventory = Scan(rootDir);

                // Analyze Regressions
                var regressions = AnalyzeRegressions(baseline, currentInventory);
                
                // Write Artifacts
                WriteInventory(inventoryPath, currentInventory);
                WriteMarkdownReport(mdPath, currentInventory);
                WriteDiffReport(diffPath, regressions, baseline, currentInventory);

                if (regressions.Count > 0)
                {
                    Console.WriteLine("FAILURE: Regressions detected!");
                    foreach (var reg in regressions)
                    {
                        Console.WriteLine($"  - {reg}");
                    }
                    return 1;
                }

                // Also fail on New UNWIRED? The prompt says "Fail CI when new unwired controls appear".
                // My logic considers "New Control that is Unwired" as a regression? 
                // Wait, "Regression" usually means Wired -> Unwired.
                // "New Unwired" means New Control -> Unwired.
                // I'll count both.

                Console.WriteLine("Audit Success: No regressions found.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR: {ex}");
                return -1;
            }
        }

        static List<ControlEntry> Scan(string rootDir)
        {
            var inventory = new List<ControlEntry>();
            var files = Directory.GetFiles(rootDir, "*.axaml", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || 
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (Path.GetFileName(file) == "AuditStyles.axaml") continue;

                try
                {
                    var relativePath = Path.GetRelativePath(rootDir, file).Replace("\\", "/");
                    var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
                    
                    foreach (var el in doc.Descendants())
                    {
                        if (InteractiveTypes.Contains(el.Name.LocalName))
                        {
                            inventory.Add(AnalyzeControl(el, relativePath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing {file}: {ex.Message}");
                }
            }
            
            return inventory.OrderBy(x => x.File).ThenBy(x => x.Line).ToList();
        }

        static ControlEntry AnalyzeControl(XElement el, string file)
        {
            var entry = new ControlEntry
            {
                File = file,
                Line = ((IXmlLineInfo)el).LineNumber,
                Type = el.Name.LocalName,
                Name = el.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name" || a.Name.LocalName == "x:Name")?.Value ?? "[Anonymous]",
            };

            // Check Wiring
            bool hasCommand = el.Attributes().Any(a => a.Name.LocalName == "Command");
            bool hasClick = el.Attributes().Any(a => a.Name.LocalName == "Click" || a.Name.LocalName == "Tapped");
            bool hasIsChecked = el.Attributes().Any(a => a.Name.LocalName == "IsChecked"); // Binding or Value
            bool hasFlyout = el.Elements().Any(e => e.Name.LocalName.EndsWith("Flyout"));

            // Check suppression (UiAudit.IsWiredManual)
            // Fix: Check for EndsWith to handle namespace prefixes (e.g. audit:UiAudit.IsWiredManual -> LocalName might be IsWiredManual or UiAudit.IsWiredManual)
            // XDocument LocalName usually handles namespace well, but to be safe we allow partial match.
            bool isSuppressed = el.Attributes().Any(a => a.Name.LocalName.EndsWith("IsWiredManual") && a.Value == "True");

            if (isSuppressed)
            {
                entry.Wiring = "Manual/Suppressed";
                entry.Status = "WIRED";
            }
            else if (hasCommand)
            {
                entry.Wiring = "Command";
                entry.Status = "WIRED";
            }
            else if (hasClick)
            {
                entry.Wiring = "Event";
                entry.Status = "WIRED"; 
            }
            else if (hasIsChecked)
            {
                 entry.Wiring = "DataBinding";
                 entry.Status = "WIRED";
            }
            else if (hasFlyout)
            {
                 entry.Wiring = "Flyout";
                 entry.Status = "WIRED";
            }
            else
            {
                entry.Wiring = "None";
                entry.Status = "UNWIRED";
            }
            
            // Identify Unknown? "If intent is unclear mark it as UNKNOWN".
            // Automated scanner can't judge intent easily.
            // We assume if no wiring, it's UNWIRED. 
            // UNKNOWN might be a manual override status in JSON?
            
            return entry;
        }

        static List<string> AnalyzeRegressions(List<ControlEntry> baseline, List<ControlEntry> current)
        {
            var regressions = new List<string>();
            // Create dictionary for baseline lookup. Key: File:Line:Type (simple exact match)
            // Note: If line numbers shift, this breaks. 
            // Ideally we'd map by fuzzy match or order, but for now exact match is the constraint.
            var baselineMap = baseline.ToDictionary(
                x => $"{x.File}:{x.Line}:{x.Type}", 
                x => x); 

            foreach (var c in current)
            {
                if (c.Status == "UNWIRED")
                {
                    // Check if this specific instance was already UNWIRED in baseline
                    var key = $"{c.File}:{c.Line}:{c.Type}";
                    if (baselineMap.TryGetValue(key, out var oldEntry))
                    {
                        if (oldEntry.Status == "UNWIRED")
                        {
                            // Grandfathered.
                            continue;
                        }
                    }
                    
                    // New regression
                    regressions.Add($"UNWIRED: {c.Type} at {c.File}:{c.Line}");
                }
            }
            
            return regressions;
        }

        static void WriteInventory(string path, List<ControlEntry> inventory)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(inventory, options));
        }

        static void WriteMarkdownReport(string path, List<ControlEntry> inventory)
        {
            var lines = new List<string>();
            lines.Add("# UI Control Inventory");
            lines.Add($"Total Controls: {inventory.Count}");
            lines.Add($"Unwired: {inventory.Count(x => x.Status == "UNWIRED")}");
            lines.Add("");
            lines.Add("| File | Line | Type | Name | Status | Wiring |");
            lines.Add("|---|---|---|---|---|---|");
            foreach (var c in inventory)
            {
                lines.Add($"| {c.File} | {c.Line} | {c.Type} | {c.Name} | {c.Status} | {c.Wiring} |");
            }
            File.WriteAllText(path, string.Join(Environment.NewLine, lines));
        }

        static void WriteDiffReport(string path, List<string> regressions, List<ControlEntry> baseline, List<ControlEntry> current)
        {
            var lines = new List<string>();
            lines.Add("# Inventory Diff");
            if (regressions.Any())
            {
                lines.Add("## Regressions");
                foreach (var r in regressions) lines.Add($"- {r}");
            }
            else
            {
                lines.Add("No regressions found.");
            }
            File.WriteAllText(path, string.Join(Environment.NewLine, lines));
        }
    }

    public class ControlEntry
    {
        public string File { get; set; } = "";
        public int Line { get; set; }
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = ""; // WIRED, UNWIRED, UNKNOWN
        public string Wiring { get; set; } = ""; // Method of wiring
    }
}
