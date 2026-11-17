using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Helpers;

/// <summary>
/// The language selector is built from satellite assemblies, so a regional culture whose resources
/// live in the neutral parent (ru-RU using ru) must not appear next to it as a second "Russian".
/// </summary>
public class LocalizationCultureListTests
{
    [Test]
    public void AvailableCulturesAreListedOnce()
    {
        IReadOnlyList<System.Globalization.CultureInfo> cultures = Localization.GetAvailableCultures();
        string[] names = cultures.Select(culture => culture.Name).ToArray();

        Assert.That(names, Is.Unique, "the language list contains duplicate cultures");
        Assert.That(names[0], Is.EqualTo("en"), "English must stay first");
    }

    [Test]
    public void RegionalVariantsCollapseOntoTheNeutralParent()
    {
        IReadOnlyList<System.Globalization.CultureInfo> cultures = Localization.GetAvailableCultures();
        HashSet<string> names = cultures.Select(culture => culture.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (System.Globalization.CultureInfo culture in cultures)
        {
            if (culture.IsNeutralCulture || culture.Name.Length == 0)
            {
                continue;
            }

            Assert.That(names.Contains(culture.Parent.Name), Is.False,
                $"{culture.Name} duplicates its neutral parent {culture.Parent.Name} in the language list");
        }
    }
}
