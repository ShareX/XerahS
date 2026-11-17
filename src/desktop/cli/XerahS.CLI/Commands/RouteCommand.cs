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

using System.CommandLine;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks.Processors;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.CLI.Commands;

/// <summary>
/// File type overrides from the command line: send every file with an extension to one uploader instance,
/// whatever category the upload is in, and explain where a file would go.
/// </summary>
public static class RouteCommand
{
    public static Command Create()
    {
        var route = new Command("route", "File type overrides: send every file with an extension to one uploader instance");

        var list = new Command("list", "List file type overrides");
        list.SetAction(_ => Environment.ExitCode = List());
        route.Add(list);

        var setExt = new Argument<string>("extension") { Description = "Extension without the dot, e.g. md or tar.gz" };
        var setInstance = new Argument<string>("instance") { Description = "Instance display name, ID, or the first 6+ characters of its ID" };
        var set = new Command("set", "Send every file with this extension to this instance");
        set.Add(setExt);
        set.Add(setInstance);
        set.SetAction(p => Environment.ExitCode = Set(p.GetValue(setExt)!, p.GetValue(setInstance)!));
        route.Add(set);

        var removeExt = new Argument<string>("extension") { Description = "Extension without the dot" };
        var remove = new Command("remove", "Remove the override for this extension");
        remove.Add(removeExt);
        remove.SetAction(p => Environment.ExitCode = Remove(p.GetValue(removeExt)!));
        route.Add(remove);

        var explainFile = new Argument<string>("file") { Description = "A file name or path" };
        var asFile = new Option<bool>("--as-file") { Description = "Explain as 'upload --as-file' would send it" };
        var explain = new Command("explain", "Show which uploader 'xerahscli upload' would send a file to, and why, without uploading");
        explain.Add(explainFile);
        explain.Add(asFile);
        explain.SetAction(p => Environment.ExitCode = Explain(p.GetValue(explainFile)!, p.GetValue(asFile)));
        route.Add(explain);

        return route;
    }

    private static int List()
    {
        var manager = InstanceManager.Instance;
        var overrides = manager.GetFileTypeOverrides();
        if (overrides.Count == 0)
        {
            Console.WriteLine("No file type overrides. Add one with: xerahscli route set <extension> <instance>");
            return 0;
        }

        foreach (var o in overrides)
        {
            var instance = manager.GetInstance(o.InstanceId);
            string target = instance == null ? $"(removed instance {o.InstanceId})" : $"{instance.DisplayName} [{instance.Category}] ({instance.InstanceId})";
            Console.WriteLine($".{o.Extension,-10} -> {target}{(instance is { IsAvailable: false } ? " (unavailable)" : "")}");
        }

        return 0;
    }

    private static int Set(string extension, string instanceQuery)
    {
        var manager = InstanceManager.Instance;
        var matches = FindInstances(manager, instanceQuery);
        if (matches.Count != 1)
        {
            Console.Error.WriteLine(matches.Count == 0
                ? $"No uploader instance matches '{instanceQuery}'. Instances: {string.Join(", ", manager.GetInstances().Select(i => i.DisplayName))}"
                : $"'{instanceQuery}' matches several instances: {string.Join(", ", matches.Select(i => $"{i.DisplayName} ({i.InstanceId})"))}. Use the ID.");
            return 1;
        }

        var instance = matches[0];
        if (InstanceManager.IsAutoProvider(instance.ProviderId))
        {
            Console.Error.WriteLine("An Auto instance cannot be an override target: it picks an uploader itself.");
            return 1;
        }

        if (InstanceManager.NormalizeOverrideExtension(extension) is not { } ext)
        {
            Console.Error.WriteLine($"'{extension}' is not a file extension.");
            return 1;
        }

        manager.SetFileTypeOverride(ext, instance.InstanceId);
        Console.WriteLine($".{ext} -> {instance.DisplayName} [{instance.Category}] ({instance.InstanceId})");
        return 0;
    }

    private static int Remove(string extension)
    {
        bool removed = InstanceManager.Instance.RemoveFileTypeOverride(extension);
        Console.WriteLine(removed ? $"Removed the override for .{extension.Trim().TrimStart('.')}." : $"No override for .{extension.Trim().TrimStart('.')}.");
        return removed ? 0 : 1;
    }

    private static int Explain(string file, bool asFile)
    {
        var manager = InstanceManager.Instance;
        string name = Path.GetFileName(file);
        var match = manager.FindFileTypeOverride(name);
        if (match is { } m && !InstanceManager.IsAutoProvider(m.Instance.ProviderId))
        {
            Console.WriteLine($"{name} -> {m.Instance.DisplayName} [{m.Instance.Category}] ({m.Instance.InstanceId})");
            Console.WriteLine($"Rule: file type override for .{m.Override.Extension} (applies to text and file uploads alike).");
            return 0;
        }

        if (manager.GetListedFileTypeOverride(name) is { } ignored)
        {
            var dead = manager.GetInstance(ignored.InstanceId);
            Console.WriteLine($"Note: the override for .{ignored.Extension} is ignored: {(dead == null ? "its uploader was removed" : $"{dead.DisplayName} is {(dead.IsAvailable ? "an Auto uploader" : "unavailable")}")}.");
        }

        // The same choices 'upload' and the upload processor make (UploadCommand, UploadJobProcessor).
        bool asText = !asFile && FileHelpers.IsTextFile(file);
        UploaderInstance? target;
        string rule;
        if (asText)
        {
            target = manager.GetDefaultInstance(UploaderCategory.Text);
            rule = "Text upload; the default Text uploader (text uploads do not use each uploader's file type settings).";
        }
        else
        {
            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            string? id = workflow.TaskSettings.GetDestinationInstanceIdForDataType(EDataType.File);
            var chosen = string.IsNullOrWhiteSpace(id) ? null : manager.GetInstance(id);
            var start = chosen ?? manager.GetDefaultInstance(UploaderCategory.File);
            target = UploadJobProcessor.ApplyFileTypeRoute(manager, UploaderCategory.File, start, name);
            rule = target != null && start != null && target.InstanceId != start.InstanceId
                ? $"File upload; {target.DisplayName} claims {Path.GetExtension(name)} in its file type settings."
                : chosen != null ? "File upload; the File Upload workflow's destination." : "File upload; the default File uploader.";
        }

        if (target == null)
        {
            Console.WriteLine($"{name} -> no {(asText ? "Text" : "File")} uploader is configured.");
            return 1;
        }

        Console.WriteLine($"{name} -> {target.DisplayName} [{target.Category}] ({target.InstanceId})");
        Console.WriteLine($"Rule: {rule} No override for {(string.IsNullOrEmpty(Path.GetExtension(name)) ? "files without an extension" : Path.GetExtension(name))}.");
        if (!target.IsAvailable || InstanceManager.IsAutoProvider(target.ProviderId))
        {
            Console.WriteLine($"Note: {target.DisplayName} is {(target.IsAvailable ? "an Auto uploader, which picks one itself" : "unavailable")}; XerahS tries other uploaders.");
        }

        return 0;
    }

    private static List<UploaderInstance> FindInstances(InstanceManager manager, string query)
    {
        var all = manager.GetInstances();
        var exact = all.Where(i => string.Equals(i.InstanceId, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.DisplayName, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return exact;
        return query.Length >= 6
            ? all.Where(i => i.InstanceId.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
    }
}
