// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class KeyboardDeviceLocatorTests
{
    // Captured from /sys/class/input/eventN/device/capabilities on an x86_64 laptop.
    private const string KeyboardEv = "120013";
    private const string KeyboardKey = "402000000 3803078f800d001 feffffdfffefffff fffffffffffffffe";
    private const string MouseEv = "17";
    private const string MouseKey = "1f0000 0 0 0 0";
    private const string PowerButtonEv = "3";
    private const string PowerButtonKey = "10000000000000 0";

    [Test]
    public void IsKeyboard_AcceptsARealKeyboard()
    {
        Assert.That(KeyboardDeviceLocator.IsKeyboard(KeyboardEv, KeyboardKey, 64), Is.True);
    }

    [TestCase(MouseEv, MouseKey)]
    [TestCase(PowerButtonEv, PowerButtonKey)]
    [TestCase("0", "")]
    public void IsKeyboard_RejectsDevicesThatAreNotKeyboards(string ev, string key)
    {
        Assert.That(KeyboardDeviceLocator.IsKeyboard(ev, key, 64), Is.False);
    }

    [Test]
    public void IsKeyboard_RequiresKeyEvents()
    {
        // Key bits alone are not enough without EV_KEY in the event types.
        Assert.That(KeyboardDeviceLocator.IsKeyboard("1", KeyboardKey, 64), Is.False);
    }

    [Test]
    public void IsKeyboard_ReadsThirtyTwoBitKernelWords()
    {
        // Esc in the low word and only keys 32 to 44 in the next word, as a 32-bit kernel prints it.
        const string key = "1fff 2";

        Assert.Multiple(() =>
        {
            Assert.That(KeyboardDeviceLocator.IsKeyboard("3", key, 32), Is.True);
            Assert.That(KeyboardDeviceLocator.IsKeyboard("3", key, 64), Is.False);
        });
    }

    [Test]
    public void ParseBitmap_ReturnsWordsLeastSignificantFirst()
    {
        Assert.That(KeyboardDeviceLocator.ParseBitmap("ff 0 1"), Is.EqualTo(new ulong[] { 1, 0, 0xff }));
    }
}
