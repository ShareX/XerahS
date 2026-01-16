using XerahS.Platform.Abstractions;
using System.Runtime.InteropServices;

namespace XerahS.UI.Services
{
    public class PlatformInfoService : IPlatformInfo
    {
        public PlatformType Platform
        {
            get
            {
                if (OperatingSystem.IsWindows()) return PlatformType.Windows;
                if (OperatingSystem.IsLinux()) return PlatformType.Linux;
                if (OperatingSystem.IsMacOS()) return PlatformType.MacOS;
                return PlatformType.Unknown;
            }
        }

        public string OSVersion => Environment.OSVersion.Version.ToString();
        public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
        public bool IsWindows => OperatingSystem.IsWindows();
        public bool IsLinux => OperatingSystem.IsLinux();
        public bool IsMacOS => OperatingSystem.IsMacOS();

        public bool IsWindows10OrGreater
        {
            get
            {
                if (!IsWindows) return false;
                return Environment.OSVersion.Version.Major >= 10;
            }
        }

        public bool IsWindows11OrGreater
        {
            get
            {
                if (!IsWindows) return false;
                // Windows 11 is version 10.0 with build >= 22000
                return Environment.OSVersion.Version.Major >= 10 &&
                       Environment.OSVersion.Version.Build >= 22000;
            }
        }

        public string RuntimeVersion => Environment.Version.ToString();

        public bool IsElevated
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    // TODO: Implement proper elevation check for Windows
                    return false;
                }
                // TODO: Implement for other platforms
                return false;
            }
        }
    }
}
