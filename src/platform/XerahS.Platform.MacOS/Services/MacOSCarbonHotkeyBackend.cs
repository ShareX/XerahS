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

using Avalonia.Threading;
using XerahS.Platform.Abstractions;
using XerahS.Platform.MacOS.Native;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS.Services
{
    internal enum CarbonRegistrationResult
    {
        /// <summary>Hotkey registered through Carbon; no Accessibility permission needed.</summary>
        Registered,

        /// <summary>Another application owns this combo (eventHotKeyExistsErr).</summary>
        Conflict,

        /// <summary>Combo cannot be served by Carbon (unmappable key or API failure); use the fallback backend.</summary>
        Unsupported
    }

    /// <summary>
    /// Global hotkey backend built on Carbon RegisterEventHotKey (XIP0078 P4).
    /// Registers hotkeys with zero TCC permissions and native suppression of the registered
    /// combo, removing the Accessibility-grant onboarding cliff that the SharpHook/CGEventTap
    /// path imposes. Hotkey events arrive on the main CFRunLoop, which Avalonia already runs.
    /// </summary>
    internal sealed class MacOSCarbonHotkeyBackend : IDisposable
    {
        private const uint HotKeySignature = 0x5853484B; // 'XSHK'

        private readonly Lock _lock = new();
        private readonly Dictionary<uint, (IntPtr HotKeyRef, HotkeyInfo Info)> _hotkeysById = new();

        // Rooted delegate: Carbon holds a raw function pointer, so the delegate must outlive registrations.
        private readonly CarbonHotkeys.EventHandlerProc _handlerProc;

        private IntPtr _handlerRef;
        private bool _handlerInstallFailed;
        private uint _nextId = 1;
        private bool _disposed;

        /// <summary>Raised on the thread the Carbon event arrives on (main run loop).</summary>
        public event Action<HotkeyInfo>? HotkeyPressed;

        /// <summary>When true, pressed combos stay suppressed system-wide but trigger no action.</summary>
        public bool IsSuspended { get; set; }

        public MacOSCarbonHotkeyBackend()
        {
            _handlerProc = HandleHotKeyEvent;
        }

        /// <summary>
        /// Attempts to register the hotkey through Carbon. Runs on the UI thread because the
        /// Carbon event handler targets the application event loop.
        /// </summary>
        public CarbonRegistrationResult TryRegister(HotkeyInfo hotkeyInfo)
        {
            if (_disposed || !CarbonHotkeys.TryMapKey(hotkeyInfo.Key, out uint keyCode))
            {
                return CarbonRegistrationResult.Unsupported;
            }

            return RunOnUIThread(() => RegisterCore(hotkeyInfo, keyCode));
        }

        public bool Unregister(HotkeyInfo hotkeyInfo)
        {
            uint? id = null;

            lock (_lock)
            {
                foreach (var pair in _hotkeysById)
                {
                    if (ReferenceEquals(pair.Value.Info, hotkeyInfo) ||
                        (pair.Value.Info.Key == hotkeyInfo.Key && pair.Value.Info.Modifiers == hotkeyInfo.Modifiers))
                    {
                        id = pair.Key;
                        break;
                    }
                }
            }

            if (id == null)
            {
                return false;
            }

            RunOnUIThread(() =>
            {
                lock (_lock)
                {
                    if (_hotkeysById.Remove(id.Value, out var entry))
                    {
                        CarbonHotkeys.UnregisterEventHotKey(entry.HotKeyRef);
                    }
                }

                return true;
            });

            return true;
        }

        public void UnregisterAll()
        {
            RunOnUIThread(() =>
            {
                lock (_lock)
                {
                    foreach (var entry in _hotkeysById.Values)
                    {
                        CarbonHotkeys.UnregisterEventHotKey(entry.HotKeyRef);
                    }

                    _hotkeysById.Clear();
                }

                return true;
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnregisterAll();

            if (_handlerRef != IntPtr.Zero)
            {
                RunOnUIThread(() =>
                {
                    CarbonHotkeys.RemoveEventHandler(_handlerRef);
                    _handlerRef = IntPtr.Zero;
                    return true;
                });
            }
        }

        private CarbonRegistrationResult RegisterCore(HotkeyInfo hotkeyInfo, uint keyCode)
        {
            if (!EnsureHandlerInstalled())
            {
                return CarbonRegistrationResult.Unsupported;
            }

            uint id;
            lock (_lock)
            {
                id = _nextId++;
            }

            var hotKeyId = new CarbonHotkeys.EventHotKeyID
            {
                Signature = HotKeySignature,
                Id = id
            };

            int status = CarbonHotkeys.RegisterEventHotKey(
                keyCode,
                CarbonHotkeys.MapModifiers(hotkeyInfo.Modifiers),
                hotKeyId,
                CarbonHotkeys.GetApplicationEventTarget(),
                0,
                out IntPtr hotKeyRef);

            if (status == CarbonHotkeys.EventHotKeyExistsErr)
            {
                DebugHelper.WriteLine($"MacOSCarbonHotkeyBackend: combo {hotkeyInfo} is owned by another application (eventHotKeyExistsErr).");
                return CarbonRegistrationResult.Conflict;
            }

            if (status != CarbonHotkeys.NoErr || hotKeyRef == IntPtr.Zero)
            {
                DebugHelper.WriteLine($"MacOSCarbonHotkeyBackend: RegisterEventHotKey failed for {hotkeyInfo} (status {status}); falling back.");
                return CarbonRegistrationResult.Unsupported;
            }

            lock (_lock)
            {
                _hotkeysById[id] = (hotKeyRef, hotkeyInfo);
            }

            DebugHelper.WriteLine($"MacOSCarbonHotkeyBackend: registered {hotkeyInfo} via Carbon (no Accessibility permission needed).");
            return CarbonRegistrationResult.Registered;
        }

        private bool EnsureHandlerInstalled()
        {
            if (_handlerRef != IntPtr.Zero)
            {
                return true;
            }

            if (_handlerInstallFailed)
            {
                return false;
            }

            var eventType = new CarbonHotkeys.EventTypeSpec
            {
                EventClass = CarbonHotkeys.EventClassKeyboard,
                EventKind = CarbonHotkeys.EventHotKeyPressed
            };

            int status = CarbonHotkeys.InstallEventHandler(
                CarbonHotkeys.GetApplicationEventTarget(),
                _handlerProc,
                1,
                ref eventType,
                IntPtr.Zero,
                out _handlerRef);

            if (status != CarbonHotkeys.NoErr || _handlerRef == IntPtr.Zero)
            {
                DebugHelper.WriteLine($"MacOSCarbonHotkeyBackend: InstallEventHandler failed (status {status}); Carbon backend disabled for this session.");
                _handlerRef = IntPtr.Zero;
                _handlerInstallFailed = true;
                return false;
            }

            return true;
        }

        private int HandleHotKeyEvent(IntPtr handlerCallRef, IntPtr eventRef, IntPtr userData)
        {
            try
            {
                int status = CarbonHotkeys.GetEventParameter(
                    eventRef,
                    CarbonHotkeys.ParamDirectObject,
                    CarbonHotkeys.TypeEventHotKeyID,
                    IntPtr.Zero,
                    (nuint)System.Runtime.InteropServices.Marshal.SizeOf<CarbonHotkeys.EventHotKeyID>(),
                    IntPtr.Zero,
                    out CarbonHotkeys.EventHotKeyID hotKeyId);

                if (status != CarbonHotkeys.NoErr || hotKeyId.Signature != HotKeySignature)
                {
                    return CarbonHotkeys.EventNotHandledErr;
                }

                HotkeyInfo? hotkeyInfo;
                lock (_lock)
                {
                    hotkeyInfo = _hotkeysById.TryGetValue(hotKeyId.Id, out var entry) ? entry.Info : null;
                }

                if (hotkeyInfo == null)
                {
                    return CarbonHotkeys.EventNotHandledErr;
                }

                if (IsSuspended)
                {
                    // Keep the combo suppressed (so it cannot start a second native capture)
                    // but do not trigger the workflow while suspended.
                    return CarbonHotkeys.NoErr;
                }

                DebugHelper.WriteLine($"MacOSCarbonHotkeyBackend: hotkey triggered: {hotkeyInfo}");
                var toRaise = hotkeyInfo;
                Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(toRaise));
                return CarbonHotkeys.NoErr;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSCarbonHotkeyBackend: hotkey event handler failed");
                return CarbonHotkeys.EventNotHandledErr;
            }
        }

        private T RunOnUIThread<T>(Func<T> action)
        {
            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    return action();
                }

                return Dispatcher.UIThread.Invoke(action);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSCarbonHotkeyBackend: UI-thread dispatch failed");
                if (typeof(T) == typeof(CarbonRegistrationResult))
                {
                    return (T)(object)CarbonRegistrationResult.Unsupported;
                }

                return default!;
            }
        }
    }
}
