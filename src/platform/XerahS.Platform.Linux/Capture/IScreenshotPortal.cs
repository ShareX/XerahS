using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XerahS.Platform.Linux.Capture;

[DBusInterface("org.freedesktop.portal.Screenshot")]
public interface IScreenshotPortal : IDBusObject
{
    Task<ObjectPath> ScreenshotAsync(string parentWindow, IDictionary<string, object> options);
}
