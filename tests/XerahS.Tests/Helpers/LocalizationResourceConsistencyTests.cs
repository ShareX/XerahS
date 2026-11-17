using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace XerahS.Tests.Helpers;

/// <summary>
/// Guards the "drop in a Resources.&lt;culture&gt;.resx and the language appears" workflow: a key used
/// by XAML but missing from the neutral set renders its raw key name, and a translation that drifts
/// from the neutral set silently falls back to English for the missing entries.
/// </summary>
public class LocalizationResourceConsistencyTests
{
    private static readonly Regex LocalizeUsage = new(@"\{local:Localize\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

    [Test]
    public void EveryXamlLocalizeKeyExistsInTheNeutralResources()
    {
        string root = FindRepositoryRoot();
        HashSet<string> keys = LoadKeys(Path.Combine(root, "src", "desktop", "core", "XerahS.Common", "Resources.resx"));
        SortedSet<string> missing = new(StringComparer.Ordinal);

        foreach (string file in EnumerateSources(root, "*.axaml"))
        {
            foreach (Match match in LocalizeUsage.Matches(File.ReadAllText(file)))
            {
                string key = match.Groups[1].Value;
                if (!keys.Contains(key))
                {
                    missing.Add(key + " in " + Path.GetRelativePath(root, file));
                }
            }
        }

        Assert.That(missing, Is.Empty, "XAML references localization keys that Resources.resx does not define.");
    }

    [Test]
    public void TranslationsDefineTheSameKeysAsTheNeutralResources()
    {
        string root = FindRepositoryRoot();
        string resourcesDirectory = Path.Combine(root, "src", "desktop", "core", "XerahS.Common");
        HashSet<string> neutral = LoadKeys(Path.Combine(resourcesDirectory, "Resources.resx"));

        foreach (string translation in Directory.EnumerateFiles(resourcesDirectory, "Resources.*.resx"))
        {
            HashSet<string> translated = LoadKeys(translation);
            string name = Path.GetFileName(translation);

            HashSet<string> missing = new(neutral, StringComparer.Ordinal);
            missing.ExceptWith(translated);
            HashSet<string> orphaned = new(translated, StringComparer.Ordinal);
            orphaned.ExceptWith(neutral);

            Assert.That(missing, Is.Empty, name + " is missing keys defined in Resources.resx");
            Assert.That(orphaned, Is.Empty, name + " defines keys that Resources.resx does not have");
        }
    }

    private static HashSet<string> LoadKeys(string path)
    {
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateSources(string root, string pattern)
    {
        return Directory
            .EnumerateFiles(Path.Combine(root, "src"), pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                           !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XerahS.sln")))
            {
                return directory.FullName;
            }
        }

        Assert.Ignore("Repository root not found; source-level resource checks need the checkout.");
        return string.Empty;
    }
}
