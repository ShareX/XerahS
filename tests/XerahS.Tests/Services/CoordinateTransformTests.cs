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
using NUnit.Framework;
using ShareX.Avalonia.Core.Services;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Tests.Services;

[TestFixture]
public class CoordinateTransformTests
{
    #region Single Monitor Tests

    [Test]
    public void PhysicalToLogical_SingleMonitor100DPI_IdentityTransform()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { monitor });

        // Act
        var physical = new PhysicalPoint(500, 300);
        var logical = transform.PhysicalToLogical(physical);
        var roundTrip = transform.LogicalToPhysical(logical);

        // Assert
        Assert.That(logical.X, Is.EqualTo(500.0));
        Assert.That(logical.Y, Is.EqualTo(300.0));
        Assert.That(roundTrip.X, Is.EqualTo(physical.X));
        Assert.That(roundTrip.Y, Is.EqualTo(physical.Y));
    }

    [Test]
    public void PhysicalToLogical_SingleMonitor150DPI_ScalesCorrectly()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 2560, 1440),
            WorkingArea = new PhysicalRectangle(0, 0, 2560, 1400),
            ScaleFactor = 1.5
        };

        var transform = new CoordinateTransform(new[] { monitor });

        // Act
        var physical = new PhysicalPoint(1500, 900);
        var logical = transform.PhysicalToLogical(physical);

        // Assert
        Assert.That(logical.X, Is.EqualTo(1000.0).Within(0.01));
        Assert.That(logical.Y, Is.EqualTo(600.0).Within(0.01));
    }

    [Test]
    public void PhysicalToLogical_SingleMonitor200DPI_ScalesCorrectly()
    {
        // Arrange - Typical 4K monitor at 200%
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "4K Display",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 3840, 2160),
            WorkingArea = new PhysicalRectangle(0, 0, 3840, 2120),
            ScaleFactor = 2.0
        };

        var transform = new CoordinateTransform(new[] { monitor });

        // Act
        var physical = new PhysicalPoint(2000, 1000);
        var logical = transform.PhysicalToLogical(physical);

        // Assert
        Assert.That(logical.X, Is.EqualTo(1000.0));
        Assert.That(logical.Y, Is.EqualTo(500.0));
    }

    #endregion

    #region Dual Monitor Tests

    [Test]
    public void PhysicalToLogical_DualMonitorSameDPI_ConvertsCorrectly()
    {
        // Arrange
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var secondary = new MonitorInfo
        {
            Id = "2",
            Name = "Secondary",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(1920, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(1920, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { primary, secondary });

        // Act - Point on primary monitor
        var physical1 = new PhysicalPoint(500, 300);
        var logical1 = transform.PhysicalToLogical(physical1);

        // Act - Point on secondary monitor
        var physical2 = new PhysicalPoint(2500, 300);
        var logical2 = transform.PhysicalToLogical(physical2);

        // Assert
        Assert.That(logical1.X, Is.EqualTo(500.0));
        Assert.That(logical1.Y, Is.EqualTo(300.0));
        Assert.That(logical2.X, Is.EqualTo(2500.0));
        Assert.That(logical2.Y, Is.EqualTo(300.0));
    }

    [Test]
    public void PhysicalToLogical_DualMonitorMixedDPI_ConvertsBoundaryCorrectly()
    {
        // Arrange
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var secondary = new MonitorInfo
        {
            Id = "2",
            Name = "Secondary",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(1920, 0, 2560, 1440),
            WorkingArea = new PhysicalRectangle(1920, 0, 2560, 1400),
            ScaleFactor = 1.5
        };

        var transform = new CoordinateTransform(new[] { primary, secondary });

        // Act: Point on boundary between monitors
        var boundaryPhysical = new PhysicalPoint(1920, 500);
        var logical = transform.PhysicalToLogical(boundaryPhysical);
        var roundTrip = transform.LogicalToPhysical(logical);

        // Assert: Should map to same or very close physical location
        Assert.That(roundTrip.X, Is.EqualTo(boundaryPhysical.X).Within(1));
        Assert.That(roundTrip.Y, Is.EqualTo(boundaryPhysical.Y).Within(1));
    }

    [Test]
    public void LogicalToPhysical_DualMonitorMixedDPI_ConvertsSecondaryCorrectly()
    {
        // Arrange
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var secondary = new MonitorInfo
        {
            Id = "2",
            Name = "Secondary",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(1920, 0, 2560, 1440),
            WorkingArea = new PhysicalRectangle(1920, 0, 2560, 1400),
            ScaleFactor = 1.5
        };

        var transform = new CoordinateTransform(new[] { primary, secondary });

        // Act: Point on secondary monitor
        // Logical (2000, 100) on secondary should map to physical
        var logical = new LogicalPoint(2000, 100);
        var physical = transform.LogicalToPhysical(logical);

        // The logical point 2000 is 80 pixels into the secondary monitor (1920 + 80)
        // With 1.5x scale, 80 logical = 120 physical
        // So physical should be 1920 + 120 = 2040
        Assert.That(physical.X, Is.EqualTo(2040).Within(2));
    }

    #endregion

    #region Negative Origin Tests

    [Test]
    public void PhysicalToLogical_NegativeOrigin_PreservesSign()
    {
        // Arrange: Monitor left of primary
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var left = new MonitorInfo
        {
            Id = "2",
            Name = "Left",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(-1920, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(-1920, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { primary, left });

        // Act
        var physical = new PhysicalPoint(-500, 300);
        var logical = transform.PhysicalToLogical(physical);

        // Assert: Negative X should be preserved
        Assert.That(logical.X, Is.LessThan(0));
        Assert.That(logical.X, Is.EqualTo(-500.0).Within(0.01));
    }

    [Test]
    public void PhysicalToLogical_NegativeOriginMixedDPI_ConvertsCorrectly()
    {
        // Arrange: High DPI monitor left of standard DPI primary
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var left = new MonitorInfo
        {
            Id = "2",
            Name = "Left 4K",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(-3840, 0, 3840, 2160),
            WorkingArea = new PhysicalRectangle(-3840, 0, 3840, 2120),
            ScaleFactor = 2.0
        };

        var transform = new CoordinateTransform(new[] { primary, left });

        // Act: Point in center of left monitor
        var physical = new PhysicalPoint(-1920, 1080);
        var logical = transform.PhysicalToLogical(physical);
        var roundTrip = transform.LogicalToPhysical(logical);

        // Assert: Round trip should be accurate
        Assert.That(roundTrip.X, Is.EqualTo(physical.X).Within(2));
        Assert.That(roundTrip.Y, Is.EqualTo(physical.Y).Within(2));
    }

    #endregion

    #region Rectangle Conversion Tests

    [Test]
    public void LogicalToPhysical_Rectangle_ConvertsCorrectly()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 2560, 1440),
            WorkingArea = new PhysicalRectangle(0, 0, 2560, 1400),
            ScaleFactor = 1.5
        };

        var transform = new CoordinateTransform(new[] { monitor });

        // Act
        var logical = new LogicalRectangle(100, 100, 200, 150);
        var physical = transform.LogicalToPhysical(logical);

        // Assert
        Assert.That(physical.X, Is.EqualTo(150).Within(2));
        Assert.That(physical.Y, Is.EqualTo(150).Within(2));
        Assert.That(physical.Width, Is.EqualTo(300).Within(2));
        Assert.That(physical.Height, Is.EqualTo(225).Within(2));
    }

    [Test]
    public void LogicalToPhysical_RectangleSpanningMonitors_ConvertsCorrectly()
    {
        // Arrange
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var secondary = new MonitorInfo
        {
            Id = "2",
            Name = "Secondary",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(1920, 0, 2560, 1440),
            WorkingArea = new PhysicalRectangle(1920, 0, 2560, 1400),
            ScaleFactor = 1.5
        };

        var transform = new CoordinateTransform(new[] { primary, secondary });

        // Act: Rectangle spanning from primary to secondary
        var logical = new LogicalRectangle(1800, 100, 300, 200);
        var physical = transform.LogicalToPhysical(logical);

        // Assert: Should convert correctly
        Assert.That(physical.X, Is.EqualTo(1800).Within(2));
        Assert.That(physical.Width, Is.GreaterThan(0));
        Assert.That(physical.Height, Is.GreaterThan(0));
    }

    #endregion

    #region Monitor Detection Tests

    [Test]
    public void FindMonitorContainingPhysical_PointOnMonitor_ReturnsCorrectMonitor()
    {
        // Arrange
        var primary = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var secondary = new MonitorInfo
        {
            Id = "2",
            Name = "Secondary",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(1920, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(1920, 0, 1920, 1040),
            ScaleFactor = 1.25
        };

        var transform = new CoordinateTransform(new[] { primary, secondary });

        // Act
        var onPrimary = transform.FindMonitorContainingPhysical(new PhysicalPoint(500, 500));
        var onSecondary = transform.FindMonitorContainingPhysical(new PhysicalPoint(2500, 500));

        // Assert
        Assert.That(onPrimary, Is.Not.Null);
        Assert.That(onPrimary!.Id, Is.EqualTo("1"));
        Assert.That(onSecondary, Is.Not.Null);
        Assert.That(onSecondary!.Id, Is.EqualTo("2"));
    }

    [Test]
    public void GetMonitorsIntersecting_SpanningRegion_ReturnsAllIntersected()
    {
        // Arrange
        var monitor1 = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var monitor2 = new MonitorInfo
        {
            Id = "2",
            Name = "Right",
            IsPrimary = false,
            Bounds = new PhysicalRectangle(1920, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(1920, 0, 1920, 1040),
            ScaleFactor = 1.25
        };

        var transform = new CoordinateTransform(new[] { monitor1, monitor2 });

        // Act: Region spanning both monitors (120px on monitor1, 180px on monitor2)
        var region = new PhysicalRectangle(1800, 100, 300, 200);
        var intersecting = transform.GetMonitorsIntersecting(region);

        // Assert
        Assert.That(intersecting, Has.Length.EqualTo(2));

        // Monitor with larger intersection should be first (monitor2 has 180px width)
        Assert.That(intersecting[0].Id, Is.EqualTo("2"));
        Assert.That(intersecting[1].Id, Is.EqualTo("1"));
    }

    #endregion

    #region Validation Tests

    [Test]
    public void ValidateCaptureRegion_ValidRegion_DoesNotThrow()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { monitor });
        var region = new PhysicalRectangle(100, 100, 640, 480);

        // Act & Assert
        Assert.DoesNotThrow(() => transform.ValidateCaptureRegion(region));
    }

    [Test]
    public void ValidateCaptureRegion_ZeroSize_Throws()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { monitor });
        var region = new PhysicalRectangle(100, 100, 0, 0);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => transform.ValidateCaptureRegion(region));
    }

    [Test]
    public void ValidateCaptureRegion_TooLarge_Throws()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { monitor });
        var region = new PhysicalRectangle(0, 0, 20000, 20000);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => transform.ValidateCaptureRegion(region));
    }

    [Test]
    public void ValidateCaptureRegion_OutsideAllMonitors_Throws()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
            WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
            ScaleFactor = 1.0
        };

        var transform = new CoordinateTransform(new[] { monitor });
        var region = new PhysicalRectangle(5000, 5000, 100, 100);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => transform.ValidateCaptureRegion(region));
    }

    #endregion

    #region Round-Trip Accuracy Tests

    [Test]
    public void TestRoundTripAccuracy_SingleMonitor_LessThan2Pixels()
    {
        // Arrange
        var monitor = new MonitorInfo
        {
            Id = "1",
            Name = "Primary",
            IsPrimary = true,
            Bounds = new PhysicalRectangle(0, 0, 2560, 1440),
            WorkingArea = new PhysicalRectangle(0, 0, 2560, 1400),
            ScaleFactor = 1.5
        };

        var transform = new CoordinateTransform(new[] { monitor });

        // Act & Assert: Test multiple points
        var testPoints = new[]
        {
            new PhysicalPoint(0, 0),
            new PhysicalPoint(1280, 720),
            new PhysicalPoint(2559, 1439),
            new PhysicalPoint(100, 200),
            new PhysicalPoint(2000, 1000)
        };

        foreach (var point in testPoints)
        {
            var error = transform.TestRoundTripAccuracy(point);
            Assert.That(error, Is.LessThan(2.0),
                $"Round-trip error for {point} is {error:F2}px (should be <2px)");
        }
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public void Constructor_NoMonitors_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoordinateTransform(Array.Empty<MonitorInfo>()));
    }

    [Test]
    public void Constructor_NullMonitors_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CoordinateTransform(null!));
    }

    [Test]
    public void VirtualDesktopBounds_TripleMonitor_CalculatesCorrectly()
    {
        // Arrange: Three monitors in complex arrangement
        var monitors = new[]
        {
            new MonitorInfo
            {
                Id = "1",
                Name = "Primary",
                IsPrimary = true,
                Bounds = new PhysicalRectangle(0, 0, 1920, 1080),
                WorkingArea = new PhysicalRectangle(0, 0, 1920, 1040),
                ScaleFactor = 1.0
            },
            new MonitorInfo
            {
                Id = "2",
                Name = "Left",
                IsPrimary = false,
                Bounds = new PhysicalRectangle(-1920, 0, 1920, 1080),
                WorkingArea = new PhysicalRectangle(-1920, 0, 1920, 1040),
                ScaleFactor = 1.0
            },
            new MonitorInfo
            {
                Id = "3",
                Name = "Above",
                IsPrimary = false,
                Bounds = new PhysicalRectangle(0, -1080, 1920, 1080),
                WorkingArea = new PhysicalRectangle(0, -1080, 1920, 1040),
                ScaleFactor = 1.0
            }
        };

        var transform = new CoordinateTransform(monitors);

        // Assert
        var bounds = transform.VirtualDesktopBounds;
        Assert.That(bounds.X, Is.EqualTo(-1920));
        Assert.That(bounds.Y, Is.EqualTo(-1080));
        Assert.That(bounds.Width, Is.EqualTo(3840)); // -1920 to 1920
        Assert.That(bounds.Height, Is.EqualTo(2160)); // -1080 to 1080
    }

    #endregion
}
