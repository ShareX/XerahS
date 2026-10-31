using System.Globalization;
using Avalonia.Data;
using NUnit.Framework;
using XerahS.UI.Converters;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

[TestFixture]
public class OneWayConverterConvertBackTests
{
    private static readonly global::Avalonia.Data.Converters.IValueConverter[] DoNothingConverters =
    [
        CountToInverseVisibilityConverter.Instance,
        EnumToDescriptionConverter.Instance,
        BoolToRecordingColorConverter.Instance,
        HotkeyStatusColorConverter.Instance,
        BoolToFontWeightConverter.Instance
    ];

    [TestCaseSource(nameof(DoNothingConverters))]
    public void ConvertBack_Returns_DoNothing_For_OneWayConverters(global::Avalonia.Data.Converters.IValueConverter converter)
    {
        var result = converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);

        Assert.That(result, Is.SameAs(BindingOperations.DoNothing));
    }
}
