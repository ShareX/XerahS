// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using XerahS.CLI.Services;

namespace XerahS.Tests.Tools;

/// <summary>
/// xerahscli upload repairs uploader defaults before uploading. It must not replace a working
/// default the user chose: it once switched a KovaForge Docs text default to the public Paste2.
/// </summary>
public class CliUploaderDefaultRepairTests
{
    [Test]
    public void KeepsAUsableUserDefault() =>
        Assert.That(CliUploaderBootstrapper.ShouldReplaceDefault(hasDefault: true, defaultIsUsable: true, defaultIsAuto: false), Is.False);

    [TestCase(false, false, false)] // no default
    [TestCase(true, false, false)]  // default cannot upload (for example no token)
    [TestCase(true, true, true)]    // Auto only picks among others
    public void FillsAMissingUnusableOrAutoDefault(bool hasDefault, bool usable, bool auto) =>
        Assert.That(CliUploaderBootstrapper.ShouldReplaceDefault(hasDefault, usable, auto), Is.True);
}
