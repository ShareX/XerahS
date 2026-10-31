using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using NUnit.Framework;

namespace XerahS.Tests.Platform.Linux;

[TestFixture, NonParallelizable]
public class LinuxSessionManagementTests
{
    private static readonly Type ProgramType = Assembly.Load("XerahS").GetType("XerahS.App.Program", throwOnError: true)!;

    [Test]
    public void X11Options_AlwaysOptOutOfSessionManagement()
    {
        var options = (X11PlatformOptions)ProgramType.GetMethod("CreateX11PlatformOptions", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        Assert.Multiple(() =>
        {
            Assert.That(options.EnableSessionManagement, Is.False);
            Assert.That(options.WmClass, Is.EqualTo("xerahs"));
        });
    }

    [Test]
    public void Cleanup_RemovesBothManagedAndNativeSessionRegistration()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("libc environment regression applies to Linux.");
        string[] names = ["SESSION_MANAGER", "SM_CLIENT_ID"];
        var managed = names.Select(Environment.GetEnvironmentVariable).ToArray();
        var native = names.Select(name => Marshal.PtrToStringUTF8(GetEnv(name))).ToArray();
        try
        {
            foreach (string name in names)
            {
                SetEnv(name, "xerahs-test-session", 1);
                Environment.SetEnvironmentVariable(name, "xerahs-test-session");
            }
            ProgramType.GetMethod("ClearX11SessionManagement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            foreach (string name in names)
            {
                Assert.That(Environment.GetEnvironmentVariable(name), Is.Null, name);
                Assert.That(GetEnv(name), Is.EqualTo(IntPtr.Zero), $"Native libSM must not see {name} either.");
            }
        }
        finally
        {
            for (int i = 0; i < names.Length; i++)
            {
                Environment.SetEnvironmentVariable(names[i], managed[i]);
                if (native[i] is { } value) SetEnv(names[i], value, 1);
                else UnsetEnv(names[i]);
            }
        }
    }

    [DllImport("libc", EntryPoint = "getenv")] private static extern IntPtr GetEnv(string name);
    [DllImport("libc", EntryPoint = "setenv")] private static extern int SetEnv(string name, string value, int overwrite);
    [DllImport("libc", EntryPoint = "unsetenv")] private static extern int UnsetEnv(string name);
}
