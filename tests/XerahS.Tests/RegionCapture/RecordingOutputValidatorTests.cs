using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class RecordingOutputValidatorTests
{
    [TestCase("000000096D6F6F7601000000096D64617401", true)]
    [TestCase("000000096D64617401000000096D6F6F7601", true)]
    [TestCase("000000016D6F6F76000000000000001101000000096D64617401", true)]
    [TestCase("000000096D6F6F7601000000096D64617401000000206D6F6F66", true)]
    [TestCase("", false)]
    [TestCase("000000096D64617401", false)]
    [TestCase("000000FF6D6F6F7601000000096D64617401", false)]
    [TestCase("000000086D6F6F76000000086D646174", false)]
    public void Mp4_RequiresCompleteMetadataAndNonemptyMedia(string hex, bool valid)
    {
        string path = Path.Combine(Path.GetTempPath(), $"xerahs-validation-{Guid.NewGuid():N}.mp4");
        try
        {
            File.WriteAllBytes(path, Convert.FromHexString(hex));
            if (valid) Assert.DoesNotThrow(() => RecordingOutputValidator.EnsureUsable(path));
            else Assert.Throws<InvalidDataException>(() => RecordingOutputValidator.EnsureUsable(path));
        }
        finally { File.Delete(path); }
    }
}
