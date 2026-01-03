#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using System;
using System.IO;
using ShareX.Ava.Uploaders.PluginSystem;

namespace ShareX.Ava.PluginExporter;

internal static class Program
{
    private const int Success = 0;
    private const int Failure = 1;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || HasHelpFlag(args))
        {
            PrintUsage();
            return args.Length == 0 ? Failure : Success;
        }

        if (!TryParseArgs(args, out var pluginDirectory, out var outputPath, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return Failure;
        }

        if (!Directory.Exists(pluginDirectory))
        {
            Console.Error.WriteLine($"Plugin directory not found: {pluginDirectory}");
            return Failure;
        }

        string resolvedOutput = ResolveOutputPath(pluginDirectory, outputPath);

        try
        {
            string packagePath = PluginPackager.Package(pluginDirectory, resolvedOutput);
            Console.WriteLine($"Created package: {packagePath}");
            return Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Packaging failed: {ex.Message}");
            return Failure;
        }
    }

    private static bool TryParseArgs(string[] args, out string pluginDirectory, out string? outputPath, out string? error)
    {
        pluginDirectory = string.Empty;
        outputPath = null;
        error = null;

        int index = 0;
        if (index < args.Length && !IsOption(args[index]))
        {
            pluginDirectory = args[index];
            index++;
        }
        else
        {
            error = "Missing plugin directory argument.";
            return false;
        }

        while (index < args.Length)
        {
            string arg = args[index];
            if (arg.Equals("-o", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--output", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    error = "Missing value for output path.";
                    return false;
                }

                outputPath = args[index];
                index++;
                continue;
            }

            if (IsOption(arg))
            {
                error = $"Unknown option: {arg}";
                return false;
            }

            if (outputPath == null)
            {
                outputPath = arg;
                index++;
                continue;
            }

            error = "Too many arguments.";
            return false;
        }

        return true;
    }

    private static bool IsOption(string value)
    {
        return value.StartsWith("-", StringComparison.Ordinal);
    }

    private static bool HasHelpFlag(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveOutputPath(string pluginDirectory, string? outputPath)
    {
        string pluginName = Path.GetFileName(
            pluginDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Path.Combine(Environment.CurrentDirectory, $"{pluginName}.sxadp");
        }

        if (Directory.Exists(outputPath))
        {
            return Path.Combine(outputPath, $"{pluginName}.sxadp");
        }

        if (!Path.HasExtension(outputPath))
        {
            return $"{outputPath}.sxadp";
        }

        return outputPath;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  PluginExporter <pluginDirectory> [outputPath]");
        Console.WriteLine("  PluginExporter <pluginDirectory> -o <outputPath>");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  PluginExporter \"C:\\Path\\To\\Plugin\"");
        Console.WriteLine("  PluginExporter \"C:\\Path\\To\\Plugin\" -o \"C:\\Output\\MyPlugin.sxadp\"");
    }
}
