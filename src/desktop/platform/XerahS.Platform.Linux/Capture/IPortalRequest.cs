using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XerahS.Platform.Linux.Capture;

[DBusInterface("org.freedesktop.portal.Request")]
public interface IPortalRequest : IDBusObject
{
    Task<IDisposable> WatchResponseAsync(Action<(uint response, IDictionary<string, object> results)> handler, Action<Exception>? error = null);
}
