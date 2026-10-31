using NUnit.Framework;
using XerahS.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

[TestFixture]
public class SystemCursorRecoveryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FailedRestore_PreservesMarkerAndAllowsSuccessfulRetry(bool throws)
    {
        string marker = Path.Combine(Path.GetTempPath(), $"xerahs-cursor-{Guid.NewGuid():N}.marker");
        File.WriteAllText(marker, "crashed process");
        try
        {
            Assert.That(SystemCursorGuard.TryRestoreAndClearMarker(marker, () => throws ? throw new InvalidOperationException("native restore failed") : false), Is.False);
            Assert.That(File.ReadAllText(marker), Is.EqualTo("crashed process"));
            Assert.That(SystemCursorGuard.TryRestoreAndClearMarker(marker, () => true), Is.True);
            Assert.That(File.Exists(marker), Is.False);
        }
        finally { File.Delete(marker); }
    }
}
